using System;
using System.Collections.Generic;
using Godot;

namespace AB;

/// <summary>Sons gerados proceduralmente (placeholder até termos áudio de verdade).</summary>
public static class Sfx
{
    const int Rate = 22050;
    static readonly Dictionary<string, AudioStreamWav> Streams = new();
    static readonly Random Rng = new(1234);

    static AudioStreamWav Make(float dur, Func<float, float> f)
    {
        int n = (int)(dur * Rate);
        var data = new byte[n * 2];
        for (int i = 0; i < n; i++)
        {
            float s = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            short v = (short)(s * 32000);
            data[2 * i] = (byte)(v & 0xff);
            data[2 * i + 1] = (byte)((v >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
    }

    static float Noise() => (float)(Rng.NextDouble() * 2 - 1);

    // ---- síntese ----
    static float Bell(float t, float f) =>
        t < 0 ? 0 : (Mathf.Sin(Mathf.Tau * f * t) + 0.4f * Mathf.Sin(Mathf.Tau * 2f * f * t) + 0.12f * Mathf.Sin(Mathf.Tau * 3.01f * f * t)) * Mathf.Exp(-t * 5f) * Mathf.Min(1f, t / 0.004f);

    /// <summary>Timbre de metal/sopro: harmônicos 1/n com leve vibrato e envelope ADSR simples.</summary>
    static float Brass(float t, float f, float dur, int harmonics = 7)
    {
        if (t < 0 || t > dur) return 0;
        float vib = 1f + 0.004f * Mathf.Sin(Mathf.Tau * 5.5f * t);
        float v = 0;
        for (int n = 1; n <= harmonics; n++) v += Mathf.Sin(Mathf.Tau * f * n * vib * t) / n * (n == 1 ? 1f : 0.8f);
        float env = Mathf.Min(1f, t / 0.025f) * Mathf.Min(1f, (dur - t) / 0.06f) * (0.85f + 0.15f * Mathf.Exp(-t * 6f));
        return v * env;
    }

    static void Init()
    {
        if (Streams.Count > 0) return;
        const float Tau = Mathf.Tau;
        // drible: batida grave e curta, sem chiado
        Streams["dribble"] = Make(0.12f, t => Mathf.Sin(Tau * (85 - 160 * t) * t) * Mathf.Exp(-t * 38) * 0.8f + Noise() * Mathf.Exp(-t * 260) * 0.06f);
        Streams["bounce"] = Make(0.16f, t => Mathf.Sin(Tau * (90 - 140 * t) * t) * Mathf.Exp(-t * 26) * 0.85f + Noise() * Mathf.Exp(-t * 180) * 0.1f);
        Streams["rim"] = Make(0.5f, t => (Mathf.Sin(Tau * 620 * t) + 0.6f * Mathf.Sin(Tau * 1480 * t) + 0.3f * Mathf.Sin(Tau * 2310 * t)) * Mathf.Exp(-t * 9) * 0.32f);
        Streams["board"] = Make(0.2f, t => (Mathf.Sin(Tau * 160 * t) * 0.7f + Noise() * 0.15f) * Mathf.Exp(-t * 30));

        // rede: ruído filtrado em banda ("fsss") com ataque suave
        float lp1 = 0, lp2 = 0;
        Streams["swish"] = Make(0.5f, t =>
        {
            float n = Noise();
            lp1 += (n - lp1) * 0.22f;
            lp2 += (lp1 - lp2) * 0.03f;
            float band = lp1 - lp2;
            float env = Mathf.Min(t / 0.03f, 1f) * Mathf.Exp(-t * 7.5f);
            return band * env * 2.2f;
        });

        // cesta: arpejo de sininhos (Dó maior)
        float[] arp = { 523.25f, 659.25f, 783.99f, 1046.5f };
        Streams["score"] = Make(1.2f, t =>
        {
            float v = 0;
            for (int i = 0; i < arp.Length; i++) v += Bell(t - i * 0.075f, arp[i]);
            return v * 0.2f;
        });

        // v0.22: cesta sofrida — mesmos sininhos, descendo e em tom menor, mais graves e mais lentos
        float[] down = { 587.33f, 466.16f, 392.00f, 293.66f };
        Streams["score_bad"] = Make(1.3f, t =>
        {
            float v = 0;
            for (int i = 0; i < down.Length; i++) v += Bell(t - i * 0.1f, down[i] * (1f - 0.015f * t));
            return v * 0.19f;
        });

        // vitória: fanfarra curta (Sol-Dó-Mi-Sol + acorde final)
        (float start, float dur, float f)[] fan =
        {
            (0.00f, 0.13f, 392.00f), (0.14f, 0.13f, 523.25f), (0.28f, 0.13f, 659.25f), (0.42f, 0.30f, 783.99f),
            (0.75f, 1.05f, 523.25f), (0.75f, 1.05f, 659.25f), (0.75f, 1.05f, 783.99f), (0.75f, 1.05f, 1046.5f),
        };
        Streams["win"] = Make(1.85f, t =>
        {
            float v = 0;
            foreach (var (st, d, f) in fan) v += Brass(t - st, f, d, 6);
            return v * 0.09f;
        });

        // fim do tempo: buzina em acorde, timbre arredondado
        Streams["horn"] = Make(0.9f, t => (Brass(t, 233.08f, 0.85f, 8) + Brass(t, 293.66f, 0.85f, 8)) * 0.13f);

        // apito (falta): dois tons agudos com trinado
        Streams["whistle"] = Make(0.55f, t => Mathf.Sin(Tau * (2650f + 120f * Mathf.Sin(Tau * 38f * t)) * t)
            * Mathf.Min(1f, t / 0.02f) * Mathf.Min(1f, (0.55f - t) / 0.05f) * 0.22f);
        Streams["beep"] = Make(0.14f, t => Mathf.Sin(Tau * 660 * t) * Mathf.Min(1, (0.14f - t) * 30) * Mathf.Min(1, t / 0.005f) * 0.45f);
        Streams["go"] = Make(0.4f, t => Bell(t, 880f) * 0.5f + Bell(t - 0.06f, 1318.5f) * 0.35f);
        // enterrada (v0.21): pancada grave + estalo de metal + chiado curto
        Streams["slam"] = Make(0.7f, t => Mathf.Sin(Tau * (70 - 40 * t) * t) * Mathf.Exp(-t * 7) * 0.9f
            + (Mathf.Sin(Tau * 410 * t) + 0.5f * Mathf.Sin(Tau * 1130 * t)) * Mathf.Exp(-t * 14) * 0.25f
            + Noise() * Mathf.Exp(-t * 35) * 0.35f);
        // v0.23: roubo barrado pelo drible — "tsk" curto e agudo (raspão), baixinho
        Streams["parry"] = Make(0.16f, t => (Noise() * 0.5f + Mathf.Sin(Tau * (1900 - 3000 * t) * t) * 0.5f) * Mathf.Exp(-t * 32) * Mathf.Min(1f, t / 0.003f) * 0.55f);
        // ---- v0.26: habilidades e cutscenes ----
        Streams["zap"] = Make(0.28f, t => (Mathf.Sign(Mathf.Sin(Tau * (900 + 1400 * Mathf.Sin(Tau * 31 * t)) * t)) * 0.35f + Noise() * 0.45f)
            * Mathf.Exp(-t * 11) * Mathf.Min(1f, t / 0.004f) * 0.5f);
        float tl = 0;
        Streams["thunder"] = Make(1.4f, t =>
        {
            float n = Noise();
            tl += (n - tl) * 0.06f;
            float crack = Noise() * Mathf.Exp(-t * 30) * 0.9f;
            float rumble = tl * 3.2f * Mathf.Exp(-t * 2.4f) * (0.7f + 0.3f * Mathf.Sin(Tau * 7 * t));
            return Mathf.Clamp(crack + rumble + Mathf.Sin(Tau * (55 - 20 * t) * t) * Mathf.Exp(-t * 3) * 0.5f, -1, 1) * 0.8f;
        });
        float wl = 0;
        Streams["whoosh"] = Make(0.45f, t =>
        {
            wl += (Noise() - wl) * (0.04f + 0.25f * Mathf.Sin(Mathf.Pi * Mathf.Clamp(t / 0.45f, 0, 1)));
            return wl * Mathf.Sin(Mathf.Pi * Mathf.Clamp(t / 0.45f, 0, 1)) * 1.8f;
        });
        // v0.27: impulso do Luck — "fuump" com baque grave, rajada de ar subindo e um brilhinho de sorte
        float d1 = 0, d2 = 0;
        Streams["dash"] = Make(0.34f, t =>
        {
            float k = Mathf.Clamp(t / 0.34f, 0, 1);
            float n = Noise();
            d1 += (n - d1) * (0.06f + 0.45f * k);
            d2 += (d1 - d2) * 0.05f;
            float air = (d1 - d2) * Mathf.Min(1f, t / 0.012f) * Mathf.Exp(-t * 8.5f) * 2.4f;
            float thump = Mathf.Sin(Tau * (140 - 260 * t) * t) * Mathf.Exp(-t * 30) * 0.55f;
            float sparkle = Bell(t - 0.05f, 1760f) * 0.1f + Bell(t - 0.09f, 2349f) * 0.07f;
            return Mathf.Clamp(air + thump + sparkle, -1, 1) * 0.8f;
        });
        Streams["chime"] = Make(0.9f, t => (Bell(t, 1046.5f) + Bell(t - 0.06f, 1318.5f) * 0.8f + Bell(t - 0.12f, 1568f) * 0.7f) * 0.22f);
        Streams["fail"] = Make(0.6f, t => (Mathf.Sin(Tau * (330 - 160 * t) * t) + 0.5f * Mathf.Sin(Tau * (220 - 100 * t) * t)) * Mathf.Exp(-t * 4) * Mathf.Min(1f, t / 0.01f) * 0.35f);
        Streams["aura"] = Make(0.6f, t => (Mathf.Sin(Tau * 110 * t) * 0.6f + Mathf.Sin(Tau * 165 * t) * 0.4f + Noise() * 0.15f)
            * Mathf.Sin(Mathf.Pi * Mathf.Clamp(t / 0.6f, 0, 1)) * 0.45f);
        Streams["dice"] = Make(0.35f, t =>
        {
            float v = 0;
            for (int i = 0; i < 4; i++) { float tt = t - i * 0.07f; if (tt > 0) v += Noise() * Mathf.Exp(-tt * 120) * 0.8f + Mathf.Sin(Tau * 2200 * tt) * Mathf.Exp(-tt * 80) * 0.3f; }
            return v * 0.6f;
        });
        // cutscenes: entrada (subida + impacto), golpe, chicote de vento, choque elétrico, trovão
        Streams["cut_start"] = Make(0.9f, t => (Mathf.Sin(Tau * (120 + 500 * t * t) * t) * 0.3f * Mathf.Min(1f, t / 0.5f) * (t < 0.6f ? 1f : 0f))
            + (t > 0.6f ? (Mathf.Sin(Tau * 60 * t) * 0.9f + Noise() * 0.5f) * Mathf.Exp(-(t - 0.6f) * 9) : 0f));
        Streams["cut_hit"] = Make(0.5f, t => (Mathf.Sin(Tau * (90 - 60 * t) * t) * 0.9f + Noise() * Mathf.Exp(-t * 40) * 0.6f) * Mathf.Exp(-t * 7));
        Streams["cut_whoosh"] = Streams["whoosh"];
        Streams["cut_zap"] = Streams["zap"];
        Streams["cut_thunder"] = Streams["thunder"];
        // v0.27: sons dos menus (passar o mouse / clicar)
        Streams["tick"] = Make(0.04f, t => Mathf.Sin(Tau * 2400 * t) * Mathf.Exp(-t * 140) * 0.35f);
        Streams["click"] = Make(0.09f, t => (Mathf.Sin(Tau * (900 - 2500 * t) * t) * 0.6f + Noise() * 0.15f) * Mathf.Exp(-t * 55) * Mathf.Min(1f, t / 0.002f) * 0.5f);
        Streams["catch"] = Make(0.08f, t => Noise() * Mathf.Exp(-t * 90) * 0.25f + Mathf.Sin(Tau * 200 * t) * Mathf.Exp(-t * 50) * 0.45f);
    }

    /// <summary>Online (host): cada som tocado também vai para os clientes (junto do próximo snapshot).</summary>
    public static Action<string, Vector3, float, float, bool> Broadcast;

    /// <summary>v0.17: todo som 3D tocado (host, offline e cliente online). A cesta usa para balançar rede/aro.
    /// Nomes sem som (ex.: "fx_dunk") servem de deixa visual replicada sem mudar o protocolo.</summary>
    public static event Action<string, Vector3, float> Played;

    public static void Play3D(Node parent, string name, Vector3 pos, float volumeDb = 0f, float pitch = 1f)
    {
        Broadcast?.Invoke(name, pos, volumeDb, pitch, true);
        Played?.Invoke(name, pos, volumeDb);
        Init();
        if (parent == null || !Streams.TryGetValue(name, out var s)) return;
        var p = new AudioStreamPlayer3D { Stream = s, Bus = "SFX", VolumeDb = volumeDb, PitchScale = pitch, UnitSize = 6 };
        parent.AddChild(p);
        p.GlobalPosition = pos;
        p.Finished += p.QueueFree;
        p.Play();
    }

    /// <summary>Time do jogador desta tela (−1 = ninguém): escolhe o som de cesta feita/sofrida.</summary>
    public static int LocalTeam = -1;

    /// <summary>Toca só nesta tela (não vai aos clientes nem dispara <see cref="Played"/>) — para sons de deixas visuais.</summary>
    public static void PlayLocal3D(Node parent, string name, Vector3 pos, float volumeDb = 0f, float pitch = 1f)
    {
        Init();
        if (parent == null || !Streams.TryGetValue(name, out var s)) return;
        var p = new AudioStreamPlayer3D { Stream = s, Bus = "SFX", VolumeDb = volumeDb, PitchScale = pitch, UnitSize = 6 };
        parent.AddChild(p);
        p.GlobalPosition = pos;
        p.Finished += p.QueueFree;
        p.Play();
    }

    /// <summary>v0.27: som de interface (menus) — só nesta tela, nunca vai para a rede.</summary>
    public static void PlayUi(Node parent, string name, float volumeDb = 0f)
    {
        Init();
        if (parent == null || !parent.IsInsideTree() || !Streams.TryGetValue(name, out var s)) return;
        var p = new AudioStreamPlayer { Stream = s, Bus = "SFX", VolumeDb = volumeDb, ProcessMode = Node.ProcessModeEnum.Always };
        parent.GetTree().Root.AddChild(p);
        p.Finished += p.QueueFree;
        p.Play();
    }

    public static void Play2D(Node parent, string name, float volumeDb = 0f, float pitch = 1f)
    {
        Broadcast?.Invoke(name, Vector3.Zero, volumeDb, pitch, false);
        // "score_t0"/"score_t1" (v0.22): cada tela toca o som do ponto do SEU time ou o de cesta sofrida
        if (name.StartsWith("score_t") && name.Length == 8)
        {
            int team = name[7] - '0';
            name = LocalTeam < 0 || LocalTeam == team ? "score" : "score_bad";
        }
        Init();
        if (parent == null || !Streams.TryGetValue(name, out var s)) return;
        var p = new AudioStreamPlayer { Stream = s, Bus = "SFX", VolumeDb = volumeDb, PitchScale = pitch };
        parent.AddChild(p);
        p.Finished += p.QueueFree;
        p.Play();
    }
}
