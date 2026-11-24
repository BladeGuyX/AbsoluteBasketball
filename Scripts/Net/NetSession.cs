using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Steamworks;

namespace AB;

public enum NetRole { None, Host, Client }

/// <summary>
/// Sessão online (Etapa 5). Host autoritativo: o host simula tudo (o <see cref="Match"/> de sempre); os clientes mandam
/// <see cref="PlayerInput"/> a cada tick e recebem snapshots (~30/s), que interpolam. Vagas vazias viram bots no host.
/// Lobby (membros, times, configurações) é mantido pelo host e espelhado nos clientes. Transporte: Steam ou IP direto.
/// Fica como filho do GameRoot (antes da partida na árvore → recebe os inputs antes do tick da partida).
/// </summary>
public partial class NetSession : Node
{
    public static NetSession I { get; private set; }
    public const int MaxPlayers = 10;
    const int SnapshotEveryTicks = 2;

    public NetRole Role { get; private set; }
    public bool Active => Role != NetRole.None;
    public bool IsHost => Role == NetRole.Host;
    public bool IsClient => Role == NetRole.Client;
    public bool ViaSteam { get; private set; }
    public bool Connected { get; private set; }     // cliente: já recebeu o Welcome
    public bool InMatch { get; private set; }
    public int MyNetId { get; private set; }
    public string MyName { get; private set; } = "Jogador";
    public OnlineSettings Settings { get; private set; } = new();
    public readonly List<LobbyMember> Members = new();
    public SteamLobby Steam { get; private set; }
    public string DirectAddress { get; private set; } // IP direto: endereço mostrado no lobby

    /// <summary>Lobby mudou (membros, times, configurações) — a tela do lobby se redesenha.</summary>
    public event Action LobbyChanged;

    INetTransport _t;
    readonly Queue<NetEvent> _events = new();
    readonly NetWriter _w = new();
    readonly Dictionary<int, RemoteInput> _inputs = new();
    readonly HashSet<int> _pendingHello = new();
    readonly NetInputCounter _counter = new();
    readonly List<NetSound> _sounds = new();
    int _nextNetId = 1;
    int _hostTick;
    List<RosterEntry> _roster;
    Match _match;
    string _savedTuning;

    // estatísticas (teste automático)
    public int SnapshotsReceived, InputsSent;

    public override void _Ready()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;
        if (SteamService.Initialized)
        {
            Steam = new SteamLobby();
            Steam.Created += OnSteamLobbyCreated;
            Steam.JoinedHost += OnSteamJoinedHost;
            Steam.Failed += msg => { GameRoot.I.Toast(msg); };
            Steam.JoinRequested += lobby =>
            {
                // convite aceito com o jogo aberto: sai do que estiver e entra
                Leave(toMenu: false);
                GameRoot.I.ShowOnlineMenu("Entrando no lobby do amigo...");
                Steam.Join(lobby);
            };
            // aberto pela Steam com "Entrar no jogo": +connect_lobby <id>
            var args = OS.GetCmdlineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong id))
                    CallDeferred(MethodName.JoinSteamLobbyById, id.ToString());
        }
    }

    void JoinSteamLobbyById(string id)
    {
        if (Steam == null || !ulong.TryParse(id, out ulong v)) return;
        GameRoot.I.ShowOnlineMenu("Entrando no lobby do amigo...");
        Steam.Join(new CSteamID(v));
    }

    // =====================================================================
    // Criar / entrar / sair

    public void HostSteam(bool isPublic)
    {
        if (Steam == null) { GameRoot.I.Toast(SteamService.Error ?? "Steam indisponível."); return; }
        Leave(toMenu: false);
        Steam.Create(isPublic);
    }

    void OnSteamLobbyCreated()
    {
        var t = SteamTransport.Host(out string err);
        if (t == null) { Steam.Leave(); GameRoot.I.Toast(err); return; }
        BeginHost(t, SteamService.LocalName ?? "Host", viaSteam: true);
    }

    public void JoinSteamByCode(string code)
    {
        if (Steam == null) { GameRoot.I.Toast(SteamService.Error ?? "Steam indisponível."); return; }
        Leave(toMenu: false);
        if (!Steam.JoinByCode(code)) GameRoot.I.Toast("Código inválido.");
    }

    public void JoinSteamLobby(CSteamID lobby)
    {
        if (Steam == null) return;
        Leave(toMenu: false);
        Steam.Join(lobby);
    }

    void OnSteamJoinedHost(ulong hostId)
    {
        var t = SteamTransport.Join(hostId, out string err);
        if (t == null) { Steam.Leave(); GameRoot.I.Toast(err); return; }
        BeginClient(t, SteamService.LocalName ?? "Jogador", viaSteam: true);
    }

    public bool HostDirect(int port, string name, bool loopbackOnly = false)
    {
        Leave(toMenu: false);
        var t = EnetTransport.Host(port, MaxPlayers - 1, loopbackOnly, out string err);
        if (t == null) { GameRoot.I.Toast(err); return false; }
        DirectAddress = $"{LocalIp()}:{port}";
        BeginHost(t, name, viaSteam: false);
        return true;
    }

    public bool JoinDirect(string address, int port, string name)
    {
        Leave(toMenu: false);
        var t = EnetTransport.Join(address, port, out string err);
        if (t == null) { GameRoot.I.Toast(err); return false; }
        DirectAddress = $"{address}:{port}";
        BeginClient(t, name, viaSteam: false);
        return true;
    }

    static string LocalIp()
    {
        foreach (var a in IP.GetLocalAddresses())
            if (a.Contains('.') && !a.StartsWith("127.") && !a.StartsWith("169.254.")) return a;
        return "127.0.0.1";
    }

    void BeginHost(INetTransport t, string name, bool viaSteam)
    {
        _t = t;
        Role = NetRole.Host;
        ViaSteam = viaSteam;
        Connected = true;
        MyNetId = 0;
        MyName = name;
        _nextNetId = 1;
        Members.Clear();
        Members.Add(new LobbyMember { NetId = 0, Name = name, Team = 0 });
        GD.Print($"[NET] hospedando ({(viaSteam ? "Steam" : "IP " + DirectAddress)})");
        GameRoot.I.ShowLobby();
        LobbyChanged?.Invoke();
    }

    void BeginClient(INetTransport t, string name, bool viaSteam)
    {
        _t = t;
        Role = NetRole.Client;
        ViaSteam = viaSteam;
        Connected = false;
        MyName = name;
        Members.Clear();
        GD.Print($"[NET] conectando ({(viaSteam ? "Steam" : "IP " + DirectAddress)})");
    }

    /// <summary>Sai da sessão (fecha a conexão, sai do lobby da Steam). toMenu = volta para o menu principal.</summary>
    public void Leave(bool toMenu = true, string reason = null)
    {
        bool was = Active;
        _t?.Shutdown();
        _t = null;
        if (Steam != null && Steam.InLobby) Steam.Leave();
        Role = NetRole.None;
        Connected = false;
        InMatch = false;
        Members.Clear();
        _inputs.Clear();
        _pendingHello.Clear();
        _roster = null;
        _match = null;
        Sfx.Broadcast = null;
        if (_savedTuning != null) { Tuning.Load(); _savedTuning = null; } // cliente: volta para o tuning local
        if (was) GD.Print("[NET] saiu da sessão" + (reason != null ? ": " + reason : ""));
        if (toMenu && was)
        {
            GameRoot.I.ShowMainMenu();
            if (reason != null) GameRoot.I.Toast(reason, 6f);
        }
    }

    // =====================================================================
    // Laço

    public override void _Process(double delta) => SteamService.Pump();

    public override void _PhysicsProcess(double delta)
    {
        if (_t == null) return;
        _t.Poll(_events);
        while (_events.Count > 0 && _t != null)
        {
            var e = _events.Dequeue();
            try { Handle(e); }
            catch (Exception ex) { GD.PushError($"[NET] mensagem inválida de {e.Peer}: {ex.Message}"); }
        }
    }

    void Handle(NetEvent e)
    {
        switch (e.Type)
        {
            case NetEventType.Connected:
                if (IsHost) _pendingHello.Add(e.Peer);
                else
                {
                    _w.Begin(Msg.Hello); _w.I32(NetProtocol.Version); _w.Str(MyName);
                    SendToHost(true);
                }
                break;
            case NetEventType.Disconnected:
                if (IsHost) RemoveMember(e.Peer);
                else Leave(toMenu: true, reason: Connected ? "A conexão com o host caiu (ou o host saiu)." : "Não deu para conectar no host.");
                break;
            case NetEventType.Data:
                if (e.Data == null || e.Data.Length == 0) return;
                var r = new NetReader(e.Data);
                var m = (Msg)r.U8();
                if (IsHost) HostMessage(e.Peer, m, r);
                else ClientMessage(m, r);
                break;
        }
    }

    // =====================================================================
    // Host

    void HostMessage(int peer, Msg m, NetReader r)
    {
        switch (m)
        {
            case Msg.Hello:
            {
                int ver = r.I32();
                string name = Clean(r.Str());
                if (ver != NetProtocol.Version) { Reject(peer, "Versão do jogo diferente da do host. Atualizem os dois para a mesma build."); return; }
                if (InMatch) { Reject(peer, "A partida já começou. Espere voltar para o lobby."); return; }
                if (Members.Count >= MaxPlayers) { Reject(peer, "O lobby está cheio."); return; }
                _pendingHello.Remove(peer);
                if (peer <= 0) { Reject(peer, "Erro de conexão (id inválido)."); return; }
                int t0 = Members.Count(x => x.Team == 0), t1 = Members.Count(x => x.Team == 1);
                var mem = new LobbyMember { NetId = peer, Name = UniqueName(name), Team = t0 <= t1 ? 0 : 1 };
                Members.Add(mem);
                _w.Begin(Msg.Welcome); _w.I32(peer);
                Send(peer, true);
                GD.Print($"[NET] {mem.Name} entrou (id {peer})");
                BroadcastLobby();
                break;
            }
            case Msg.SetTeam:
            {
                int team = r.U8() == 1 ? 1 : 0;
                var mem = Members.FirstOrDefault(x => x.NetId == peer);
                if (mem != null && !InMatch) { mem.Team = team; BroadcastLobby(); }
                break;
            }
            case Msg.Input:
            {
                int seq = r.I32();
                if (!_inputs.TryGetValue(peer, out var ri)) _inputs[peer] = ri = new RemoteInput();
                var move = r.V2(); float yaw = r.F(), pitch = r.F(); var held = (Btn)r.U16();
                var counters = new byte[NetInputCounter.Buttons.Length];
                for (int i = 0; i < counters.Length; i++) counters[i] = r.U8();
                float skillT = r.F();
                int renderTick = r.I32();
                if (seq <= ri.LastSeq) break;
                if (renderTick > 0)
                {
                    float late = Mathf.Max(0, (_hostTick - renderTick) / 60f);
                    // sobe na hora, desce devagar (aguenta picos de atraso)
                    ri.Lateness = late > ri.Lateness ? late : Mathf.Lerp(ri.Lateness, late, 0.02f);
                } // pacote velho fora de ordem
                ri.LastSeq = seq;
                ri.Move = move.LimitLength(1f); ri.AimYaw = yaw; ri.AimPitch = pitch; ri.Held = held;
                Array.Copy(counters, ri.Counters, counters.Length);
                ri.ShootSkillT = skillT;
                ri.Packets++;
                break;
            }
        }
    }

    static string Clean(string s)
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) s = "Jogador";
        return s.Length > 20 ? s[..20] : s;
    }

    string UniqueName(string name)
    {
        string n = name;
        for (int k = 2; Members.Any(x => x.Name == n); k++) n = $"{name} {k}";
        return n;
    }

    void Reject(int peer, string why)
    {
        _w.Begin(Msg.Reject); _w.Str(why);
        Send(peer, true);
        GD.Print($"[NET] recusado {peer}: {why}");
    }

    void RemoveMember(int peer)
    {
        _pendingHello.Remove(peer);
        var mem = Members.FirstOrDefault(x => x.NetId == peer);
        if (mem != null)
        {
            Members.Remove(mem);
            GD.Print($"[NET] {mem.Name} saiu");
            if (InMatch && _match != null && IsInstanceValid(_match)) _match.OnRemoteLeft(peer, mem.Name);
        }
        _inputs.Remove(peer);
        if (mem == null) return;
        BroadcastLobby();
    }

    public void BroadcastLobby()
    {
        if (!IsHost) return;
        _w.Begin(Msg.Lobby);
        Settings.Write(_w);
        _w.U8((byte)Members.Count);
        foreach (var mem in Members) { _w.I32(mem.NetId); _w.Str(mem.Name); _w.U8((byte)mem.Team); }
        SendToAll(true);
        LobbyChanged?.Invoke();
    }

    public void HostSetSettings(OnlineSettings s)
    {
        if (!IsHost) return;
        Settings = s.Clone();
        BroadcastLobby();
    }

    /// <summary>Troca de time (host: direto; cliente: pede ao host).</summary>
    public void SetMyTeam(int team)
    {
        if (IsHost)
        {
            var me = Members.FirstOrDefault(x => x.NetId == 0);
            if (me != null) { me.Team = team; BroadcastLobby(); }
        }
        else if (IsClient)
        {
            _w.Begin(Msg.SetTeam); _w.U8((byte)team);
            SendToHost(true);
        }
    }

    /// <summary>Escalação: humanos de cada time + bots até completar o tamanho do time (limitado pela quadra).</summary>
    List<RosterEntry> BuildRoster()
    {
        int perTeamMax = Mathf.Max(1, CourtGeo.MaxPlayers(Settings.Size) / 2);
        var list = new List<RosterEntry>();
        for (int team = 0; team < 2; team++)
        {
            var humans = Members.Where(x => x.Team == team).ToList();
            int size = Mathf.Clamp(Mathf.Max(Settings.TeamSize, humans.Count), 1, Mathf.Max(perTeamMax, humans.Count));
            foreach (var h in humans) list.Add(new RosterEntry { Name = h.Name, Team = team, NetId = h.NetId });
            for (int i = humans.Count, b = 1; i < size; i++, b++) list.Add(new RosterEntry { Name = $"Bot {(team == 0 ? "Azul" : "Laranja")} {b}", Team = team });
        }
        return list;
    }

    /// <summary>Host: começa a partida para todos.</summary>
    public void HostStartMatch()
    {
        if (!IsHost) return;
        _roster = BuildRoster();
        InMatch = true;
        Steam?.SetInGame(true);
        string tuning = Tuning.ToJson();
        _w.Begin(Msg.Start);
        Settings.Write(_w);
        _w.U8((byte)_roster.Count);
        foreach (var e in _roster) { _w.Str(e.Name); _w.U8((byte)e.Team); _w.I32(e.NetId); }
        _w.Str(tuning);
        SendToAll(true);
        foreach (var ri in _inputs.Values) ri.LastSeq = -1;
        _hostTick = 0;
        Sfx.Broadcast = QueueSound;
        GD.Print($"[NET] partida começou: {_roster.Count} jogadores ({_roster.Count(x => !x.IsBot)} humanos)");
        GameRoot.I.StartMatch(MakeConfig(NetRole.Host));
    }

    MatchConfig MakeConfig(NetRole role) => new()
    {
        Size = Settings.Size, Mode = Settings.Mode, TargetScore = Settings.TargetScore, TimeMinutes = Settings.TimeMinutes,
        BotDifficulty = Settings.BotDifficulty, Net = role, Roster = _roster, MyNetId = MyNetId, SmokeNet = SmokeNet, ScreenshotPath = SmokeShot,
    };

    /// <summary>Host: volta todos para o lobby (fim da partida).</summary>
    public void HostBackToLobby()
    {
        if (!IsHost) return;
        InMatch = false;
        Sfx.Broadcast = null;
        Steam?.SetInGame(false);
        _w.Begin(Msg.BackToLobby);
        SendToAll(true);
        GameRoot.I.ShowLobby();
        BroadcastLobby();
    }

    public void RegisterMatch(Match m) => _match = m;

    /// <summary>Host: input do jogador remoto para este tick (sem pacote = parado).</summary>
    public PlayerInput TakeInput(int netId) => _inputs.TryGetValue(netId, out var ri) ? ri.Take() : default;
    public int InputPackets(int netId) => _inputs.TryGetValue(netId, out var ri) ? ri.Packets : 0;

    void QueueSound(string name, Vector3 pos, float db, float pitch, bool is3D)
    {
        if (_sounds.Count < 48) _sounds.Add(new NetSound { Name = name, Pos = pos, Db = db, Pitch = pitch, Is3D = is3D });
    }

    /// <summary>Host: chamado no fim de cada tick da partida; manda snapshot a cada 2 ticks (~30/s).</summary>
    public void HostAfterTick(Match m)
    {
        if (!IsHost || !InMatch) return;
        _hostTick++;
        if (_hostTick % SnapshotEveryTicks != 0 || Members.Count <= 1) { if (Members.Count <= 1) _sounds.Clear(); return; }
        var w = _w.Begin(Msg.Snapshot);
        w.I32(_hostTick);
        w.U8((byte)m.Phase); w.F(m.PhaseTimer); w.I16((short)m.Score[0]); w.I16((short)m.Score[1]); w.F(m.Clock); w.Bool(m.Overtime);
        w.U8((byte)(m.Winner + 1)); w.U8((byte)m.Possession);
        w.U8(Id(m.FtShooter)); w.U8((byte)m.FtAttempt); w.F(m.FtT); w.F(m.FtDur); w.Bool(m.FtPressed); w.F(m.FtPressT); w.F(m.FtFlash);
        w.Bool(m.FtFlashOk); w.U8(Id(m.FtFlashPlayer)); w.Bool(m.FtChecking);
        var b = m.Ball;
        w.U8((byte)b.State); w.U8(Id(b.Holder)); w.U8((byte)(b.ThrowTeam + 1)); w.U8(Id(b.LastThrower)); w.U8(Id(b.PassTarget));
        w.V3(b.GlobalPosition); w.V3(b.Vel); w.F(b.FlightTime);
        w.U8((byte)m.Players.Count);
        foreach (var p in m.Players) { w.U8((byte)p.Id); Player.WriteNet(w, p.CaptureNet()); }
        w.U8((byte)_sounds.Count);
        foreach (var s in _sounds) { w.Str(s.Name); w.V3(s.Pos); w.F(s.Db); w.F(s.Pitch); w.Bool(s.Is3D); }
        _sounds.Clear();
        SendToAll(false);
    }

    static byte Id(Player p) => (byte)(p != null && IsInstanceValid(p) ? p.Id : 0);

    /// <summary>Host: mensagem da HUD para os clientes (contagem, placar, prorrogação, fim).</summary>
    public void HostHud(int kind, string text, float dur, Color color, bool small, int winner)
    {
        if (!IsHost || !InMatch) return;
        _w.Begin(Msg.Hud); _w.U8((byte)kind); _w.Str(text); _w.F(dur); _w.F(color.R); _w.F(color.G); _w.F(color.B); _w.F(color.A);
        _w.Bool(small); _w.I16((short)winner);
        SendToAll(true);
    }

    // =====================================================================
    // Cliente

    void ClientMessage(Msg m, NetReader r)
    {
        switch (m)
        {
            case Msg.Welcome:
                MyNetId = r.I32();
                Connected = true;
                GD.Print($"[NET] conectado como id {MyNetId}");
                GameRoot.I.ShowLobby();
                break;
            case Msg.Reject:
                Leave(toMenu: true, reason: r.Str());
                break;
            case Msg.Lobby:
            {
                Settings = OnlineSettings.Read(r);
                Members.Clear();
                int n = r.U8();
                for (int i = 0; i < n; i++) Members.Add(new LobbyMember { NetId = r.I32(), Name = r.Str(), Team = r.U8() });
                LobbyChanged?.Invoke();
                break;
            }
            case Msg.Start:
            {
                Settings = OnlineSettings.Read(r);
                int n = r.U8();
                _roster = new List<RosterEntry>();
                for (int i = 0; i < n; i++) _roster.Add(new RosterEntry { Name = r.Str(), Team = r.U8(), NetId = r.I32() });
                string tuning = r.Str();
                _savedTuning ??= "local";
                Tuning.ApplyJson(tuning); // valores do host (não salva no arquivo)
                InMatch = true;
                _counter.Seq = 0;
                GD.Print($"[NET] partida começou: {_roster.Count} jogadores");
                GameRoot.I.StartMatch(MakeConfig(NetRole.Client));
                break;
            }
            case Msg.Snapshot:
                if (_match == null || !IsInstanceValid(_match)) break;
                SnapshotsReceived++;
                _match.ClientSnapshot(ReadSnapshot(r));
                break;
            case Msg.Hud:
            {
                int kind = r.U8(); string text = r.Str(); float dur = r.F();
                var col = new Color(r.F(), r.F(), r.F(), r.F()); bool small = r.Bool(); int winner = r.I16();
                if (_match != null && IsInstanceValid(_match)) _match.ClientHud(kind, text, dur, col, small, winner);
                break;
            }
            case Msg.BackToLobby:
                InMatch = false;
                GameRoot.I.ShowLobby();
                break;
        }
    }

    NetSnapshot ReadSnapshot(NetReader r)
    {
        var s = new NetSnapshot
        {
            Tick = r.I32(), Phase = (MatchPhase)r.U8(), PhaseTimer = r.F(), Score0 = r.I16(), Score1 = r.I16(), Clock = r.F(), Overtime = r.Bool(),
            Winner = r.U8() - 1, Possession = r.U8(),
            FtShooter = r.U8(), FtAttempt = r.U8(), FtT = r.F(), FtDur = r.F(), FtPressed = r.Bool(), FtPressT = r.F(), FtFlash = r.F(),
            FtFlashOk = r.Bool(), FtFlashPlayer = r.U8(), FtChecking = r.Bool(),
            BallState = (BallState)r.U8(), BallHolder = r.U8(), BallThrowTeam = r.U8() - 1, BallLastThrower = r.U8(), BallPassTarget = r.U8(),
            BallPos = r.V3(), BallVel = r.V3(), BallFlightTime = r.F(),
        };
        int n = r.U8();
        for (int i = 0; i < n; i++) { int id = r.U8(); s.Players.Add((id, Player.ReadNet(r))); }
        int ns = r.U8();
        for (int i = 0; i < ns; i++)
        {
            var snd = new NetSound { Name = r.Str(), Pos = r.V3(), Db = r.F(), Pitch = r.F(), Is3D = r.Bool() };
            if (_match != null && IsInstanceValid(_match))
            {
                if (snd.Is3D) Sfx.Play3D(_match, snd.Name, snd.Pos, snd.Db, snd.Pitch);
                else Sfx.Play2D(_match, snd.Name, snd.Db, snd.Pitch);
            }
        }
        return s;
    }

    /// <summary>Cliente: manda o input deste tick (não confiável; apertos vão como contadores).</summary>
    public void SendInput(PlayerInput inp)
    {
        if (!IsClient || !Connected) return;
        _counter.Count(inp);
        _w.Begin(Msg.Input); _w.I32(_counter.Seq); _w.V2(inp.Move); _w.F(inp.AimYaw); _w.F(inp.AimPitch); _w.U16((ushort)inp.Held);
        foreach (var c in _counter.Counters) _w.U8(c);
        if (inp.WasPressed(Btn.Shoot)) _lastSkillT = inp.SkillT;
        _w.F(_lastSkillT); // vai em todo pacote (se um se perder, o próximo leva)
        _w.I32(ClientRenderTick);
        SendToHost(false);
        InputsSent++;
    }

    public int PingToHost() => _t?.Ping(0) ?? -1;
    float _lastSkillT = -1;
    /// <summary>Cliente: tick do host que está sendo desenhado agora (o Match atualiza).</summary>
    public int ClientRenderTick;

    /// <summary>Host: tempo extra (s) para esperar o aperto do skill check de um cliente (ping + atraso de desenho do cliente).</summary>
    public float SkillGrace(int netId)
    {
        float late = _inputs.TryGetValue(netId, out var ri) ? ri.Lateness : 0.3f;
        return Mathf.Clamp(late + 0.06f, 0.1f, 0.8f);
    }

    // =====================================================================

    void Send(int peer, bool reliable) => _t?.Send(peer, _w.Buffer, _w.Length, reliable);
    void SendToHost(bool reliable) => _t?.Send(0, _w.Buffer, _w.Length, reliable);
    void SendToAll(bool reliable)
    {
        if (_t == null) return;
        foreach (var mem in Members) if (mem.NetId != 0) _t.Send(mem.NetId, _w.Buffer, _w.Length, reliable);
    }

    // ---- teste automático (--net-smoke-host / --net-smoke-client) ----
    public bool SmokeNet;
    public string SmokeShot;   // --net-shot <png>: o cliente salva prints da partida
}
