using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;

namespace AB;

/// <summary>
/// Transporte pela Steam (SteamNetworkingSockets P2P, pelos relays da Steam — sem abrir portas).
/// O host escuta; o cliente conecta no Steam ID do dono do lobby. Mesmo desenho do Just Fall.
/// </summary>
public sealed class SteamTransport : INetTransport
{
    const int MaxMessagesPerPoll = 256;

    readonly Callback<SteamNetConnectionStatusChangedCallback_t> _status;
    HSteamListenSocket _listen = HSteamListenSocket.Invalid;
    HSteamNetPollGroup _group = HSteamNetPollGroup.Invalid;
    HSteamNetConnection _server = HSteamNetConnection.Invalid;
    readonly Dictionary<int, HSteamNetConnection> _clients = new();
    readonly Queue<NetEvent> _pending = new();
    readonly IntPtr[] _msgs = new IntPtr[MaxMessagesPerPoll];
    byte[] _buf = new byte[2048];
    public bool IsServer { get; }

    SteamTransport(bool server)
    {
        IsServer = server;
        _status = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatus);
    }

    public static SteamTransport Host(out string error)
    {
        error = null;
        if (!SteamService.Initialized) { error = SteamService.Error ?? "Steam indisponível."; return null; }
        var t = new SteamTransport(true);
        t._listen = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
        t._group = SteamNetworkingSockets.CreatePollGroup();
        if (t._listen == HSteamListenSocket.Invalid) { error = "Não deu para abrir a conexão da Steam."; t.Shutdown(); return null; }
        return t;
    }

    public static SteamTransport Join(ulong hostSteamId, out string error)
    {
        error = null;
        if (!SteamService.Initialized) { error = SteamService.Error ?? "Steam indisponível."; return null; }
        var t = new SteamTransport(false);
        var id = new SteamNetworkingIdentity();
        id.SetSteamID64(hostSteamId);
        t._server = SteamNetworkingSockets.ConnectP2P(ref id, 0, 0, null);
        if (t._server == HSteamNetConnection.Invalid) { error = "Não deu para conectar no host pela Steam."; t.Shutdown(); return null; }
        return t;
    }

    // O handle da Steam é um uint grande: convertido para int podia virar NEGATIVO, e NetId < 0 = bot na escalação
    // (o amigo virava bot e o cliente assistia o host). Cada conexão ganha um id pequeno e positivo.
    readonly Dictionary<uint, int> _ids = new();
    int _nextId = 1;
    int Key(HSteamNetConnection c)
    {
        if (!_ids.TryGetValue(c.m_HSteamNetConnection, out int id)) _ids[c.m_HSteamNetConnection] = id = _nextId++;
        return id;
    }

    void OnStatus(SteamNetConnectionStatusChangedCallback_t s)
    {
        var c = s.m_hConn;
        switch (s.m_info.m_eState)
        {
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                if (IsServer && s.m_info.m_hListenSocket != HSteamListenSocket.Invalid)
                {
                    if (SteamNetworkingSockets.AcceptConnection(c) == EResult.k_EResultOK) SteamNetworkingSockets.SetConnectionPollGroup(c, _group);
                    else SteamNetworkingSockets.CloseConnection(c, 0, "Accept failed", false);
                }
                break;
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                if (IsServer)
                {
                    _clients[Key(c)] = c;
                    _pending.Enqueue(new NetEvent { Type = NetEventType.Connected, Peer = Key(c) });
                }
                else if (c == _server) _pending.Enqueue(new NetEvent { Type = NetEventType.Connected, Peer = 0 });
                break;
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                SteamNetworkingSockets.CloseConnection(c, 0, null, false);
                if (IsServer)
                {
                    if (_clients.Remove(Key(c))) _pending.Enqueue(new NetEvent { Type = NetEventType.Disconnected, Peer = Key(c) });
                }
                else if (c == _server)
                {
                    _server = HSteamNetConnection.Invalid;
                    _pending.Enqueue(new NetEvent { Type = NetEventType.Disconnected, Peer = 0 });
                }
                break;
        }
    }

    public void Poll(Queue<NetEvent> into)
    {
        while (_pending.Count > 0) into.Enqueue(_pending.Dequeue());
        int n = IsServer
            ? (_group != HSteamNetPollGroup.Invalid ? SteamNetworkingSockets.ReceiveMessagesOnPollGroup(_group, _msgs, MaxMessagesPerPoll) : 0)
            : (_server != HSteamNetConnection.Invalid ? SteamNetworkingSockets.ReceiveMessagesOnConnection(_server, _msgs, MaxMessagesPerPoll) : 0);
        for (int i = 0; i < n; i++)
        {
            var m = SteamNetworkingMessage_t.FromIntPtr(_msgs[i]);
            var data = new byte[m.m_cbSize];
            Marshal.Copy(m.m_pData, data, 0, m.m_cbSize);
            int from = IsServer ? Key(m.m_conn) : 0;
            SteamNetworkingMessage_t.Release(_msgs[i]);
            into.Enqueue(new NetEvent { Type = NetEventType.Data, Peer = from, Data = data });
        }
    }

    public void Send(int peer, byte[] data, int length, bool reliable)
    {
        HSteamNetConnection c;
        if (IsServer) { if (!_clients.TryGetValue(peer, out c)) return; }
        else { c = _server; if (c == HSteamNetConnection.Invalid) return; }
        int flags = reliable ? Constants.k_nSteamNetworkingSend_ReliableNoNagle : Constants.k_nSteamNetworkingSend_UnreliableNoNagle;
        if (_buf.Length < length) _buf = new byte[Math.Max(length, _buf.Length * 2)];
        Buffer.BlockCopy(data, 0, _buf, 0, length);
        var h = GCHandle.Alloc(_buf, GCHandleType.Pinned);
        try { SteamNetworkingSockets.SendMessageToConnection(c, h.AddrOfPinnedObject(), (uint)length, flags, out _); }
        finally { h.Free(); }
    }

    public void DisconnectPeer(int peer)
    {
        if (_clients.TryGetValue(peer, out var c))
        {
            SteamNetworkingSockets.CloseConnection(c, 0, "Desconectado pelo host", true);
            _clients.Remove(peer);
        }
    }

    public int Ping(int peer)
    {
        var c = IsServer ? (_clients.TryGetValue(peer, out var x) ? x : HSteamNetConnection.Invalid) : _server;
        if (c == HSteamNetConnection.Invalid) return -1;
        var st = new SteamNetConnectionRealTimeStatus_t();
        var lanes = new SteamNetConnectionRealTimeLaneStatus_t();
        return SteamNetworkingSockets.GetConnectionRealTimeStatus(c, ref st, 0, ref lanes) == EResult.k_EResultOK ? Math.Max(st.m_nPing, 0) : -1;
    }

    public void Shutdown()
    {
        if (SteamService.Initialized)
        {
            foreach (var c in _clients.Values) SteamNetworkingSockets.CloseConnection(c, 0, "O host saiu", true);
            if (_server != HSteamNetConnection.Invalid) SteamNetworkingSockets.CloseConnection(_server, 0, "Saiu", true);
            if (_listen != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(_listen);
            if (_group != HSteamNetPollGroup.Invalid) SteamNetworkingSockets.DestroyPollGroup(_group);
        }
        _clients.Clear();
        _server = HSteamNetConnection.Invalid;
        _listen = HSteamListenSocket.Invalid;
        _group = HSteamNetPollGroup.Invalid;
        _status?.Dispose();
    }
}
