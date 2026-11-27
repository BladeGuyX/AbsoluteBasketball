using Godot;

namespace AB;

/// <summary>
/// v0.26: um skill check das cutscenes (mesma regra da enterrada/lance livre): o ponteiro sai em T = −prep, chega na zona
/// certa em T = Dur; vale o aperto do arremesso a até Window/2 de Dur. Online: o host julga pelo SkillT que o cliente
/// manda (onde o ponteiro estava na tela dele) e espera o atraso da rede (grace) antes de dar como perdido.
/// </summary>
public sealed class SkillCheckState
{
    public bool Active, Done, Ok, Pressed;
    public float T, Dur, Window, PressT, Flash, Since;
    /// <summary>Muda a cada skill check novo (o cliente sabe se o resultado que ele mostra é deste ou do anterior).</summary>
    public int Seq;
    static int _seqGen;

    public void Reset() { Active = Done = Ok = Pressed = false; T = Dur = PressT = Flash = Since = 0; Window = 0.1f; }

    public void Start(float dur, float window, float prep)
    {
        Active = true; Done = false; Ok = false; Pressed = false;
        T = -Mathf.Max(prep, 0f); Dur = Mathf.Max(dur, 0.08f); Window = Mathf.Max(window, 0.01f); PressT = 0; Since = 0;
        Seq = ++_seqGen & 0x7fff; // v0.43: único entre A e B (antes cada um contava o seu e o número de um batia com o do outro)
    }

    /// <summary>Host: avança e julga o aperto do jogador.</summary>
    public void Tick(float dt, in PlayerInput inp, float grace)
    {
        if (Flash > 0) Flash -= dt;
        if (Done) Since += dt;
        if (!Active) return;
        // v0.46: quem joga na máquina do host viu o ponteiro de ANTES deste tick (o aperto só é lido agora): julga por ele.
        // Antes o aperto valia um tick depois do que estava na tela, e com a janela do Duelo menor que um tick, ponteiro
        // dentro da zona na tela virava erro.
        float shown = T;
        T += dt;
        float half = Window * 0.5f;
        if (T >= 0 && !Pressed && inp.WasPressed(Btn.Shoot))
        {
            Pressed = true;
            PressT = inp.SkillT >= 0 ? Mathf.Min(inp.SkillT, T) : Mathf.Max(shown, 0f);
        }
        if (Pressed) Resolve(Mathf.Abs(PressT - Dur) <= half);
        else if (T >= Dur + half + grace) Resolve(false);
    }

    void Resolve(bool ok)
    {
        Active = false; Done = true; Ok = ok; Since = 0;
        Flash = Mathf.Max(Tuning.I.SkillResultHold, 0.001f);
    }

    /// <summary>v0.46 (cliente): o ponteiro anda a cada tick entre um snapshot e outro (antes pulava ~30 vezes por segundo e
    /// podia pular a zona inteira no fim do Duelo — o aperto mandava a posição velha).</summary>
    public void ClientAdvance(float dt) { if (Active && !Pressed) T += dt; }

    public void Write(NetWriter w)
    {
        w.U8((byte)((Active ? 1 : 0) | (Done ? 2 : 0) | (Ok ? 4 : 0) | (Pressed ? 8 : 0)));
        w.F(T); w.F(Dur); w.F(Window); w.F(PressT); w.F(Flash); w.U16((ushort)(Seq & 0xffff));
    }

    public void Read(NetReader r)
    {
        int f = r.U8();
        int oldSeq = Seq; float oldT = T; bool wasActive = Active;
        Active = (f & 1) != 0; Done = (f & 2) != 0; Ok = (f & 4) != 0; Pressed = (f & 8) != 0;
        T = r.F(); Dur = r.F(); Window = r.F(); PressT = r.F(); Flash = r.F(); Seq = r.U16();
        // v0.46: mesmo skill check e o ponteiro local já um pouco à frente → não volta para trás (ponteiro sem tremer)
        if (wasActive && Active && !Pressed && Seq == oldSeq && oldT > T && oldT - T < 0.15f) T = oldT;
    }
}
