using System.Collections.Generic;
using Godot;

namespace AB;

/// <summary>
/// Transporte por IP direto (ENet da Godot, usado só como "cano" de pacotes — sem RPC/SceneMultiplayer).
/// Para testar com duas janelas no mesmo PC (127.0.0.1) ou em rede local, e no teste automático --net-smoke-*.
/// </summary>
public sealed class EnetTransport : INetTransport
{
    public const int DefaultPort = 27960;
    readonly ENetMultiplayerPeer _peer = new();
    readonly Queue<NetEvent> _pending = new();
    public bool IsServer { get; }

    EnetTransport(bool server)
    {
        IsServer = server;
        _peer.PeerConnected += id => _pending.Enqueue(new NetEvent { Type = NetEventType.Connected, Peer = Map((int)id) });
        _peer.PeerDisconnected += id => _pending.Enqueue(new NetEvent { Type = NetEventType.Disconnected, Peer = Map((int)id) });
    }

    /// <summary>No cliente, o servidor do ENet é o peer 1 → vira 0 (convenção da sessão).</summary>
    int Map(int enetId) => !IsServer && enetId == 1 ? 0 : enetId;

    /// <summary>Hospeda. loopbackOnly = só 127.0.0.1 (teste automático: não abre o firewall).</summary>
    public static EnetTransport Host(int port, int maxClients, bool loopbackOnly, out string error)
    {
        var t = new EnetTransport(true);
        if (loopbackOnly) t._peer.SetBindIP("127.0.0.1");
        var err = t._peer.CreateServer(port, maxClients);
        error = err == Error.Ok ? null : $"Não deu para abrir a porta {port} ({err}).";
        return err == Error.Ok ? t : null;
    }

    public static EnetTransport Join(string address, int port, out string error)
    {
        var t = new EnetTransport(false);
        var err = t._peer.CreateClient(address, port);
        error = err == Error.Ok ? null : $"Não deu para conectar em {address}:{port} ({err}).";
        return err == Error.Ok ? t : null;
    }

    public void Poll(Queue<NetEvent> into)
    {
        _peer.Poll();
        while (_pending.Count > 0) into.Enqueue(_pending.Dequeue());
        while (_peer.GetAvailablePacketCount() > 0)
        {
            int from = Map(_peer.GetPacketPeer());
            var data = _peer.GetPacket();
            into.Enqueue(new NetEvent { Type = NetEventType.Data, Peer = from, Data = data });
        }
        // cliente que não conseguiu conectar
        if (!IsServer && _peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected && !_reportedFail)
        {
            _reportedFail = true;
            into.Enqueue(new NetEvent { Type = NetEventType.Disconnected, Peer = 0 });
        }
    }
    bool _reportedFail;

    public void Send(int peer, byte[] data, int length, bool reliable)
    {
        if (_peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected) return;
        _peer.TransferMode = reliable ? MultiplayerPeer.TransferModeEnum.Reliable : MultiplayerPeer.TransferModeEnum.Unreliable;
        _peer.TransferChannel = 0; // canal padrão (o ENet separa confiável/não confiável sozinho)
        _peer.SetTargetPeer(IsServer ? peer : 1);
        if (length == data.Length) _peer.PutPacket(data);
        else
        {
            var copy = new byte[length];
            System.Array.Copy(data, copy, length);
            _peer.PutPacket(copy);
        }
    }

    public void DisconnectPeer(int peer)
    {
        if (IsServer) _peer.DisconnectPeer(peer);
    }

    public void Shutdown()
    {
        _peer.Close();
    }

    public int Ping(int peer)
    {
        var p = _peer.GetPeer(IsServer ? peer : 1);
        return p == null ? -1 : (int)p.GetStatistic(ENetPacketPeer.PeerStatistic.RoundTripTime);
    }
}
