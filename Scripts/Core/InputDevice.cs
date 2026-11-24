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
        if (pad.HasValue) Set(pad.Value);
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
