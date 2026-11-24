using System;
using Godot;

namespace AB;

[Flags]
public enum Btn : uint
{
    None = 0,
    Sprint = 1 << 0,
    Shoot = 1 << 1,
    Pass = 1 << 2,
    Jump = 1 << 3,
    Dribble = 1 << 4,
    Steal = 1 << 5,
    Variation = 1 << 6,
}

/// <summary>
/// Input de um tick, independente da origem (humano local, bot ou cliente remoto).
/// O servidor simula tudo a partir disso — base para o multiplayer.
/// </summary>
public struct PlayerInput
{
    public Vector2 Move;   // direção no mundo (X, Z), magnitude ≤ 1
    public float AimYaw;   // para onde a câmera/olhar aponta
    public float AimPitch;
    public Btn Held;
    public Btn Pressed;    // apertado neste tick
    /// <summary>
    /// Online: no aperto do arremesso, onde estava o ponteiro do skill check NA TELA do cliente (s; −1 = não informado).
    /// O host julga por isso — sem isso o atraso da rede fazia o cliente errar todo skill check.
    /// </summary>
    public float SkillT;

    public readonly bool IsHeld(Btn b) => (Held & b) != 0;
    public readonly bool WasPressed(Btn b) => (Pressed & b) != 0;
}
