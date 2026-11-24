using System;
using System.Collections.Generic;
using Godot;
using Steamworks;

namespace AB;

/// <summary>
/// Lobby da Steam: criar (só amigos ou público), procurar públicos, entrar por convite / "Entrar no jogo" / código,
/// convidar amigos (direto pela Steam, sem overlay, ou pela janela do overlay). O lobby só diz quem é o host;
/// o jogo em si passa pelo <see cref="SteamTransport"/>. Adaptado do Just Fall.
/// </summary>
public sealed class SteamLobby
{
    const string GameKey = "game";
    const string GameValue = "AbsoluteBasketball"; // o AppID 480 é compartilhado por muitos jogos de teste: lista só os nossos
    const string NameKey = "name";
    const string InGameKey = "ingame";
    const string VersionKey = "ver";

    public struct LobbyInfo { public CSteamID Id; public string Name; public int Members; }
    public struct FriendInfo { public CSteamID Id; public string Name; public bool Online; public bool InThisGame; }

    readonly Callback<LobbyEnter_t> _entered;
    readonly Callback<GameLobbyJoinRequested_t> _joinRequested;
    readonly Callback<GameRichPresenceJoinRequested_t> _richJoin;
    readonly CallResult<LobbyCreated_t> _created;
    readonly CallResult<LobbyMatchList_t> _list;
    readonly HashSet<ulong> _invited = new();
    bool _public;

    public CSteamID Current { get; private set; } = CSteamID.Nil;
    public bool InLobby => Current != CSteamID.Nil;
    public bool IsOwner => InLobby && SteamMatchmaking.GetLobbyOwner(Current) == SteamUser.GetSteamID();
    public bool Searching { get; private set; }
    public List<LobbyInfo> Found { get; } = new();

    /// <summary>Lobby criado por nós: começar a hospedar.</summary>
    public event Action Created;
    /// <summary>Entramos no lobby de outra pessoa: conectar no dono (host). Argumento = Steam ID do host.</summary>
    public event Action<ulong> JoinedHost;
    public event Action<string> Failed;
    /// <summary>Convite aceito / "Entrar no jogo" com o jogo aberto: a sessão sai do que estiver e entra.</summary>
    public event Action<CSteamID> JoinRequested;
    public event Action ListUpdated;

    public SteamLobby()
    {
        _entered = Callback<LobbyEnter_t>.Create(OnEntered);
        _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(r =>
        {
            GD.Print($"[STEAM] convite aceito (lobby {r.m_steamIDLobby}, de {r.m_steamIDFriend})");
            RequestJoin(r.m_steamIDLobby);
        });
        _richJoin = Callback<GameRichPresenceJoinRequested_t>.Create(r =>
        {
            GD.Print($"[STEAM] pedido para entrar '{r.m_rgchConnect}' de {r.m_steamIDFriend}");
            if (TryParseConnect(r.m_rgchConnect, out var lobby)) RequestJoin(lobby);
        });
        _created = CallResult<LobbyCreated_t>.Create(OnCreated);
        _list = CallResult<LobbyMatchList_t>.Create(OnList);
    }

    public static bool TryParseConnect(string connect, out CSteamID lobby)
    {
        lobby = CSteamID.Nil;
        if (string.IsNullOrEmpty(connect)) return false;
        var parts = connect.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length - 1; i++)
            if (parts[i] == "+connect_lobby" && ulong.TryParse(parts[i + 1], out ulong id)) { lobby = new CSteamID(id); return true; }
        return false;
    }

    public void RequestJoin(CSteamID lobby)
    {
        if (JoinRequested != null) JoinRequested(lobby);
        else Join(lobby);
    }

    public void Create(bool isPublic)
    {
        Leave();
        _public = isPublic;
        _created.Set(SteamMatchmaking.CreateLobby(isPublic ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeFriendsOnly, NetSession.MaxPlayers));
    }

    void OnCreated(LobbyCreated_t r, bool ioFailure)
    {
        if (ioFailure || r.m_eResult != EResult.k_EResultOK)
        {
            Failed?.Invoke($"Não deu para criar o lobby ({r.m_eResult}).");
            return;
        }
        Current = new CSteamID(r.m_ulSteamIDLobby);
        _invited.Clear();
        SteamMatchmaking.SetLobbyData(Current, GameKey, GameValue);
        SteamMatchmaking.SetLobbyData(Current, NameKey, $"Lobby de {SteamFriends.GetPersonaName()}");
        SteamMatchmaking.SetLobbyData(Current, InGameKey, "0");
        SteamMatchmaking.SetLobbyData(Current, VersionKey, NetProtocol.Version.ToString());
        SetPresence();
        GD.Print($"[STEAM] lobby {Current} criado ({(_public ? "público" : "só amigos")})");
        Created?.Invoke();
    }

    public void Join(CSteamID lobby)
    {
        if (lobby == Current) return;
        Leave();
        SteamMatchmaking.JoinLobby(lobby);
    }

    void OnEntered(LobbyEnter_t e)
    {
        var lobby = new CSteamID(e.m_ulSteamIDLobby);
        if (e.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Failed?.Invoke("Não deu para entrar no lobby (cheio, fechado ou não existe mais).");
            return;
        }
        Current = lobby;
        SetPresence();
        var owner = SteamMatchmaking.GetLobbyOwner(lobby);
        GD.Print($"[STEAM] entrou no lobby {lobby} (dono {owner})");
        if (owner == SteamUser.GetSteamID()) return; // nosso lobby: já estamos hospedando
        if (SteamMatchmaking.GetLobbyData(lobby, InGameKey) == "1")
        {
            Leave();
            Failed?.Invoke("Essa partida já começou.");
            return;
        }
        JoinedHost?.Invoke(owner.m_SteamID);
    }

    public void Leave()
    {
        if (InLobby && SteamService.Initialized) SteamMatchmaking.LeaveLobby(Current);
        Current = CSteamID.Nil;
        SetPresence();
    }

    /// <summary>Enquanto num lobby, amigos veem "Entrar no jogo" em você na Steam (carrega o lobby).</summary>
    void SetPresence()
    {
        if (!SteamService.Initialized) return;
        if (InLobby)
        {
            SteamFriends.SetRichPresence("connect", $"+connect_lobby {Current.m_SteamID}");
            SteamFriends.SetRichPresence("status", "No lobby do Absolute Basketball");
        }
        else SteamFriends.ClearRichPresence();
    }

    /// <summary>Host: partida começou → ninguém mais entra (e some da lista).</summary>
    public void SetInGame(bool inGame)
    {
        if (!IsOwner) return;
        SteamMatchmaking.SetLobbyData(Current, InGameKey, inGame ? "1" : "0");
        SteamMatchmaking.SetLobbyJoinable(Current, !inGame);
    }

    public bool OverlayAvailable => SteamService.Initialized && SteamUtils.IsOverlayEnabled();
    public void OpenOverlayInvite() { if (InLobby) SteamFriends.ActivateGameOverlayInviteDialog(Current); }

    /// <summary>Amigos: jogando este jogo primeiro, depois online, depois offline; por nome.</summary>
    public List<FriendInfo> Friends()
    {
        var list = new List<FriendInfo>();
        if (!SteamService.Initialized) return list;
        var flags = EFriendFlags.k_EFriendFlagImmediate;
        int count = SteamFriends.GetFriendCount(flags);
        var app = SteamUtils.GetAppID();
        for (int i = 0; i < count; i++)
        {
            var id = SteamFriends.GetFriendByIndex(i, flags);
            var state = SteamFriends.GetFriendPersonaState(id);
            bool playing = SteamFriends.GetFriendGamePlayed(id, out var game) && game.m_gameID.AppID() == app;
            list.Add(new FriendInfo { Id = id, Name = SteamFriends.GetFriendPersonaName(id), Online = state != EPersonaState.k_EPersonaStateOffline, InThisGame = playing });
        }
        list.Sort((x, y) =>
        {
            static int Rank(FriendInfo f) => f.InThisGame ? 0 : f.Online ? 1 : 2;
            int r = Rank(x).CompareTo(Rank(y));
            return r != 0 ? r : string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase);
        });
        return list;
    }

    /// <summary>Convida direto pela Steam (aparece no chat do amigo), sem precisar do overlay.</summary>
    public bool Invite(CSteamID friend)
    {
        if (!InLobby) return false;
        bool a = SteamMatchmaking.InviteUserToLobby(Current, friend);
        bool b = SteamFriends.InviteUserToGame(friend, $"+connect_lobby {Current.m_SteamID}");
        GD.Print($"[STEAM] convidou {friend} (lobby {a}, jogo {b})");
        if (!a && !b) return false;
        _invited.Add(friend.m_SteamID);
        return true;
    }

    public bool WasInvited(CSteamID friend) => _invited.Contains(friend.m_SteamID);

    /// <summary>Código do lobby: o amigo digita no menu para entrar (quando o convite não chega).</summary>
    public string Code => InLobby ? Current.m_SteamID.ToString() : "";

    public bool JoinByCode(string code)
    {
        if (!ulong.TryParse(code.Trim(), out ulong id) || id == 0) return false;
        Join(new CSteamID(id));
        return true;
    }

    public void Search()
    {
        Searching = true;
        Found.Clear();
        SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameValue, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListStringFilter(InGameKey, "0", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        _list.Set(SteamMatchmaking.RequestLobbyList());
    }

    void OnList(LobbyMatchList_t r, bool ioFailure)
    {
        Searching = false;
        Found.Clear();
        if (!ioFailure)
            for (int i = 0; i < r.m_nLobbiesMatching; i++)
            {
                var id = SteamMatchmaking.GetLobbyByIndex(i);
                Found.Add(new LobbyInfo { Id = id, Name = SteamMatchmaking.GetLobbyData(id, NameKey), Members = SteamMatchmaking.GetNumLobbyMembers(id) });
            }
        ListUpdated?.Invoke();
    }
}
