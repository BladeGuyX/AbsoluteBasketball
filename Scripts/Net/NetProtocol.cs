using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

namespace AB;

/// <summary>Tipos de mensagem. C→H = cliente para host; H→C = host para clientes.</summary>
public enum Msg : byte
{
    Hello = 1,      // C→H: versão + nome
    Welcome,        // H→C: seu NetId (ou recusa com motivo)
    Lobby,          // H→C: configurações + membros
    SetTeam,        // C→H: quero ir para o time X
    Start,          // H→C: começa a partida (configurações, escalação, tuning do host)
    Input,          // C→H: input do tick (não confiável; apertos por contador)
    Snapshot,       // H→C: estado da partida (não confiável)
    Hud,            // H→C: mensagem/contagem/fim de jogo (confiável)
    BackToLobby,    // H→C: volta todo mundo para o lobby
    Reject,         // H→C: recusado (versão diferente, cheio, partida em andamento)
    SetCharacter,   // C→H: v0.26: escolhi o personagem X (tela de seleção no lobby)
    Cheat,          // C→H: v0.27: trapaça do modo de teste (comando + 2 números)
    PlayerAdded,    // H→C: v0.27: entrou um jogador no meio da partida (bot do modo de teste)
    PlayerRemoved,  // H→C: v0.27: saiu um jogador (bot removido no modo de teste)
}

/// <summary>Configurações da partida online (o host escolhe no lobby).</summary>
public sealed class OnlineSettings
{
    public CourtSize Size = CourtSize.Small;
    public WinMode Mode = WinMode.Points;
    public int TargetScore = 21;
    public float TimeMinutes = 5f;
    public int TeamSize = 2;          // jogadores por time; vagas vazias viram bots
    public int BotDifficulty = 1;
    public bool Abilities = true;     // v0.26: false = modo sem habilidades
    public bool TestMode;             // v0.27: modo de teste (partida infinita + trapaças)

    public void Write(NetWriter w)
    {
        w.U8((byte)Size); w.U8((byte)Mode); w.I16((short)TargetScore); w.F(TimeMinutes); w.U8((byte)TeamSize); w.U8((byte)BotDifficulty);
        w.Bool(Abilities); w.Bool(TestMode);
    }

    public static OnlineSettings Read(NetReader r) => new()
    {
        Size = (CourtSize)r.U8(), Mode = (WinMode)r.U8(), TargetScore = r.I16(), TimeMinutes = r.F(), TeamSize = r.U8(), BotDifficulty = r.U8(),
        Abilities = r.Bool(), TestMode = r.Bool(),
    };

    public OnlineSettings Clone() => (OnlineSettings)MemberwiseClone();
}

public sealed class LobbyMember
{
    public int NetId;       // host = 0
    public string Name;
    public int Team;
    public int Character;   // v0.23
}

/// <summary>Uma vaga na partida (mesma ordem no host e nos clientes → mesmos Player.Id).</summary>
public sealed class RosterEntry
{
    public string Name;
    public int Team;
    public int NetId = -1;  // −1 = bot
    public int Character;   // v0.23: personagem (Characters.All)
    public bool IsBot => NetId < 0;
}

public static class NetProtocol
{
    /// <summary>Mude sempre que o formato das mensagens mudar (cliente e host precisam da mesma versão).</summary>
    public const int Version = 9; // v0.37: Iron (estado novo das habilidades no snapshot). v8 = v0.35: CutKind.UltPose (pose da ultimate). v7: // v0.27: modo de teste (trapaças, jogadores entrando/saindo), personagem no snapshot, enterrada com a Presença
}

/// <summary>Escreve mensagens binárias (little endian). Reaproveita o buffer.</summary>
public sealed class NetWriter
{
    readonly MemoryStream _ms = new(1024);
    readonly BinaryWriter _w;
    public NetWriter() { _w = new BinaryWriter(_ms, Encoding.UTF8, true); }

    public NetWriter Begin(Msg m) { _ms.SetLength(0); _w.Write((byte)m); return this; }
    public byte[] Buffer => _ms.GetBuffer();
    public int Length => (int)_ms.Length;

    public void U8(byte v) => _w.Write(v);
    public void Bool(bool v) => _w.Write(v);
    public void I16(short v) => _w.Write(v);
    public void U16(ushort v) => _w.Write(v);
    public void I32(int v) => _w.Write(v);
    public void F(float v) => _w.Write(v);
    public void Str(string s) => _w.Write(s ?? "");
    public void V3(Vector3 v) { _w.Write(v.X); _w.Write(v.Y); _w.Write(v.Z); }
    public void V2(Vector2 v) { _w.Write(v.X); _w.Write(v.Y); }
}

public sealed class NetReader
{
    readonly BinaryReader _r;
    public NetReader(byte[] data) { _r = new BinaryReader(new MemoryStream(data), Encoding.UTF8); }
    public byte U8() => _r.ReadByte();
    public bool Bool() => _r.ReadBoolean();
    public short I16() => _r.ReadInt16();
    public ushort U16() => _r.ReadUInt16();
    public int I32() => _r.ReadInt32();
    public float F() => _r.ReadSingle();
    public string Str() => _r.ReadString();
    public Vector3 V3() => new(_r.ReadSingle(), _r.ReadSingle(), _r.ReadSingle());
    public Vector2 V2() => new(_r.ReadSingle(), _r.ReadSingle());
}

/// <summary>Input recebido de um cliente. Apertos chegam como contadores (pacote perdido não perde o aperto).</summary>
public sealed class RemoteInput
{
    public Vector2 Move;
    public float AimYaw, AimPitch;
    public Btn Held;
    public readonly byte[] Counters = new byte[NetInputCounter.Buttons.Length];
    readonly byte[] _consumed = new byte[NetInputCounter.Buttons.Length];
    public int Packets;
    public int LastSeq = -1;
    public float ShootSkillT = -1;   // SkillT do último aperto de arremesso
    public byte EmoteSeq, EmoteId;   // v0.21: expressão (contador: pacote perdido não perde a escolha)
    byte _emoteConsumed;
    public byte TargetSeq, TargetId, TargetSlot;   // v0.26: alvo escolhido no menu (mesmo esquema do contador)
    byte _targetConsumed;
    public float Lateness = 0.25f;   // quanto (s) o cliente está atrás do host: tick que ele desenha → chegada aqui

    /// <summary>Input deste tick: apertos = contadores que mudaram desde o último tick.</summary>
    public PlayerInput Take()
    {
        var inp = new PlayerInput { Move = Move, AimYaw = AimYaw, AimPitch = AimPitch, Held = Held, SkillT = ShootSkillT };
        for (int i = 0; i < Counters.Length; i++)
            if (Counters[i] != _consumed[i]) { inp.Pressed |= NetInputCounter.Buttons[i]; _consumed[i] = Counters[i]; }
        if (EmoteSeq != _emoteConsumed) { _emoteConsumed = EmoteSeq; inp.Emote = EmoteId; }
        if (TargetSeq != _targetConsumed) { _targetConsumed = TargetSeq; inp.Target = TargetId; inp.TargetSlot = TargetSlot; }
        return inp;
    }
}

/// <summary>Cliente: conta os apertos de cada botão (manda os contadores em todo pacote).</summary>
public sealed class NetInputCounter
{
    public static readonly Btn[] Buttons = { Btn.Sprint, Btn.Shoot, Btn.Pass, Btn.Jump, Btn.Dribble, Btn.Steal, Btn.Variation, Btn.Ab1, Btn.Ab2, Btn.Ab3, Btn.Ult };
    public readonly byte[] Counters = new byte[Buttons.Length];
    public int Seq;

    public void Count(PlayerInput inp)
    {
        for (int i = 0; i < Buttons.Length; i++) if (inp.WasPressed(Buttons[i])) Counters[i]++;
        Seq++;
    }
}

/// <summary>Som tocado no host, repetido nos clientes (vai junto do snapshot).</summary>
public struct NetSound
{
    public string Name;
    public Vector3 Pos;
    public float Db, Pitch;
    public bool Is3D;
}

/// <summary>Um snapshot recebido pelo cliente.</summary>
public sealed class NetSnapshot
{
    public int Tick;
    public MatchPhase Phase;
    public float PhaseTimer, Clock;
    public int Score0, Score1, Winner, Possession;
    public byte TestFlags;   // v0.27: 1 = sem recarga, 2 = estamina infinita
    public bool Overtime;
    // lances livres
    public int FtShooter, FtAttempt, FtFlashPlayer;
    public float FtT, FtDur, FtPressT, FtFlash;
    public bool FtPressed, FtFlashOk, FtChecking;
    // bola
    public BallState BallState;
    public int BallHolder, BallThrowTeam, BallLastThrower, BallPassTarget;
    public Vector3 BallPos, BallVel;
    public float BallFlightTime;
    public byte BallAura;
    public readonly List<(int Id, Player.NetState S)> Players = new();
}
