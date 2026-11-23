using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace AB;

/// <summary>Um atalho salvo. Kind: key | mouse | joybutton | joyaxis.</summary>
public sealed class Bind
{
    public string Kind = "key";
    public int Code;
    public float Axis; // sinal do eixo para joyaxis (+1/-1)

    public Bind() { }
    public Bind(string kind, int code, float axis = 0) { Kind = kind; Code = code; Axis = axis; }
    public bool SameAs(Bind o) => o != null && o.Kind == Kind && o.Code == Code && Math.Sign(o.Axis) == Math.Sign(Axis);
    public Bind Clone() => new(Kind, Code, Axis);
}

/// <summary>Configurações do jogador (user://settings.json). Diferente do Tuning (gameplay).</summary>
public sealed class UserSettings
{
    public static UserSettings I { get; private set; } = new();

    public float MasterVolume = 0.8f;
    public float SfxVolume = 1.0f;
    public float MusicVolume = 0.7f;
    public bool Fullscreen = true;
    public float MouseSensitivity = 1.0f;
    public float StickSensitivity = 1.0f;
    public bool InvertY = false;
    public bool SprintToggle = false;
    public Dictionary<string, Bind> KeyboardBinds = new();
    public Dictionary<string, Bind> PadBinds = new();

    static readonly JsonSerializerOptions JsonOpts = new() { IncludeFields = true, WriteIndented = true };
    static string FilePath => ProjectSettings.GlobalizePath("user://settings.json");

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                I = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FilePath), JsonOpts) ?? new UserSettings();
        }
        catch (Exception e)
        {
            GD.PushWarning("settings.json inválido, usando padrão: " + e.Message);
            I = new UserSettings();
        }
    }

    public static void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(I, JsonOpts)); }
        catch (Exception e) { GD.PushWarning("Falha ao salvar settings: " + e.Message); }
    }

    public static void EnsureAudioBuses()
    {
        foreach (var name in new[] { "SFX", "Music" })
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;
            AudioServer.AddBus();
            int idx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(idx, name);
            AudioServer.SetBusSend(idx, "Master");
        }
    }

    public void ApplyAudio()
    {
        SetBus("Master", MasterVolume);
        SetBus("SFX", SfxVolume);
        SetBus("Music", MusicVolume);
    }

    static void SetBus(string name, float v)
    {
        int i = AudioServer.GetBusIndex(name);
        if (i < 0) return;
        AudioServer.SetBusMute(i, v <= 0.001f);
        AudioServer.SetBusVolumeDb(i, Mathf.LinearToDb(Mathf.Max(v, 0.001f)));
    }

    public void ApplyVideo()
    {
        if (DisplayServer.GetName() == "headless") return;
        if (Fullscreen)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            return;
        }
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
        // o redimensionamento só funciona depois que o modo terminou de trocar
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.CreateTimer(0.1, true, false, true).Timeout += () =>
        {
            if (Fullscreen) return;
            if (DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Windowed)
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            var usable = DisplayServer.ScreenGetUsableRect();
            var size = new Vector2I((int)(usable.Size.X * 0.8f), (int)(usable.Size.Y * 0.8f));
            DisplayServer.WindowSetSize(size);
            DisplayServer.WindowSetPosition(usable.Position + (usable.Size - size) / 2);
        };
    }
}
