using System;
using Godot;

namespace AB;

/// <summary>
/// Último dispositivo usado (Etapa 6): teclado/mouse ou controle. A HUD troca os ícones dos botões na hora
/// e a vibração só acontece usando o controle. Alimentado pelo GameRoot._Input.
/// </summary>
public static class InputDevice
{
    public static bool Gamepad { get; private set; }
    public static event Action Changed;

    public static void Feed(InputEvent e)
    {
        bool? pad = e switch
        {
            InputEventJoypadButton => true,
            InputEventJoypadMotion m when Mathf.Abs(m.AxisValue) > 0.45f => true,
            InputEventKey k when k.Pressed => false,
            InputEventMouseButton mb when mb.Pressed => false,
            InputEventMouseMotion mm when mm.Relative.Length() > 3f => false,
            _ => null,
        };
        if (pad == true && _padEvents++ == 0) GD.Print($"[PAD] primeiro evento do controle: {e.AsText()} (device {e.Device})");
        if (pad.HasValue) Set(pad.Value);
    }

    /// <summary>Texto de diagnóstico: controles conectados e se algum botão já chegou ao jogo.</summary>
    public static string PadReport()
    {
        var ids = Input.GetConnectedJoypads();
        string list = ids.Count == 0 ? "nenhum" : string.Join(", ", System.Linq.Enumerable.Select(ids, id => $"{Input.GetJoyName(id)} (#{id})"));
        string s = $"Controles detectados: {list}. {(_padEvents > 0 ? "Botões chegando ao jogo: sim." : "Nenhum botão do controle chegou ao jogo ainda.")}";
        if (_padEvents == 0 && SteamService.Initialized)
            s += " Se o controle não responde: na Steam, Biblioteca → Spacewar → Propriedades → Controle → \"Desativar Steam Input\" e abra o jogo de novo.";
        return s;
    }

    static int _padEvents;

    /// <summary>Loga controles conectados/desconectados (vai para o arquivo de log).</summary>
    public static void LogPads()
    {
        foreach (int id in Input.GetConnectedJoypads())
            GD.Print($"[PAD] conectado #{id}: {Input.GetJoyName(id)} guid={Input.GetJoyGuid(id)}");
        Input.Singleton.JoyConnectionChanged += (id, connected) =>
            GD.Print($"[PAD] #{id} {(connected ? "conectou" : "desconectou")}: {Input.GetJoyName((int)id)}");
    }

    public static void Set(bool pad)
    {
        if (pad == Gamepad) return;
        Gamepad = pad;
        Changed?.Invoke();
    }

    /// <summary>Nome curto do atalho de uma ação no dispositivo atual (para os ícones da HUD).</summary>
    public static string Glyph(string action)
    {
        if (action == Act.Pause) return Gamepad ? "Menu" : "Esc";
        var b = Gamepad ? InputSetup.GetPad(action) : InputSetup.GetKb(action);
        if (b == null) return "—";
        if (b.Kind == "mouse")
            return (MouseButton)b.Code switch
            {
                MouseButton.Left => "Esq.", MouseButton.Right => "Dir.", MouseButton.Middle => "Meio",
                _ => InputSetup.Describe(b),
            };
        if (b.Kind == "joyaxis")
            return (JoyAxis)b.Code switch { JoyAxis.TriggerLeft => "LT", JoyAxis.TriggerRight => "RT", _ => InputSetup.Describe(b) };
        if (b.Kind == "key")
            return (Key)b.Code switch { Key.Space => "Espaço", Key.Shift => "Shift", Key.Ctrl => "Ctrl", Key.Alt => "Alt", Key.Tab => "Tab", _ => InputSetup.Describe(b) };
        return InputSetup.Describe(b);
    }
}
