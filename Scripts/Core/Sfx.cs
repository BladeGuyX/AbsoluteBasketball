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

    public static void Play2D(Node parent, string name, float volumeDb = 0f, float pitch = 1f)
    {
        Broadcast?.Invoke(name, Vector3.Zero, volumeDb, pitch, false);
        Init();
        if (parent == null || !Streams.TryGetValue(name, out var s)) return;
        var p = new AudioStreamPlayer { Stream = s, Bus = "SFX", VolumeDb = volumeDb, PitchScale = pitch };
        parent.AddChild(p);
        p.Finished += p.QueueFree;
        p.Play();
    }
}
