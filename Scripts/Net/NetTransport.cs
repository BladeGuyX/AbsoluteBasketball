using System.Collections.Generic;

namespace AB;

public enum NetEventType { Connected, Disconnected, Data }

public struct NetEvent
{
    public NetEventType Type;
    public int Peer;        // no cliente, o host é sempre 0
    public byte[] Data;
}

/// <summary>
/// Transporte de pacotes entre host e clientes (Etapa 5). Duas implementações:
/// <see cref="SteamTransport"/> (P2P pelos relays da Steam — jogo de verdade) e <see cref="EnetTransport"/> (IP direto,
/// para testar com duas janelas no mesmo PC e para o teste automático). A sessão (<see cref="NetSession"/>) não sabe qual é.
/// </summary>
public interface INetTransport
{
    bool IsServer { get; }
    /// <summary>Lê o que chegou (conexões, desconexões e pacotes) e coloca em <paramref name="into"/>.</summary>
    void Poll(Queue<NetEvent> into);
    void Send(int peer, byte[] data, int length, bool reliable);
    void DisconnectPeer(int peer);
    void Shutdown();
    /// <summary>Ping (ms) até o peer, se o transporte souber; −1 = desconhecido.</summary>
    int Ping(int peer);
}
