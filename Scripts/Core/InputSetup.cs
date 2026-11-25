using System.Collections.Generic;
using Godot;

namespace AB;

public static class Act
{
    public const string MoveF = "move_forward", MoveB = "move_back", MoveL = "move_left", MoveR = "move_right";
    public const string LookL = "look_left", LookR = "look_right", LookU = "look_up", LookD = "look_down";
    public const string Sprint = "sprint", Shoot = "shoot", Pass = "pass", Jump = "jump";
    public const string Dribble = "dribble", Steal = "steal", Variation = "variation", Emote = "emote";
    public const string Ab1 = "ability_1", Ab2 = "ability_2", Ab3 = "ability_3", Ult = "ultimate"; // v0.26
    public const string Pause = "pause", ReloadTuning = "reload_tuning";

    /// <summary>Ações exibidas na tela de controles (ordem de exibição).</summary>
    public static readonly (string Action, string Label, bool PadRebindable)[] Rebindable =
    {
        (MoveF, "Mover para frente", false), (MoveB, "Mover para trás", false),
        (MoveL, "Mover para a esquerda", false), (MoveR, "Mover para a direita", false),
        (Sprint, "Correr", true), (Shoot, "Arremesso", true), (Pass, "Passe", true),
        (Jump, "Salto", true), (Dribble, "Drible", true), (Steal, "Roubar", true),
        (Variation, "Variação", true), (Emote, "Expressões (segurar)", true),
        (Ab1, "Habilidade 1", true), (Ab2, "Habilidade 2", true), (Ab3, "Habilidade 3", true), (Ult, "Ultimate", true),
    };

    /// <summary>v0.26: o que cada ação faz (aba Informações das configurações).</summary>
    public static readonly (string Action, string Desc)[] Info =
    {
        (MoveF, "Andar (frente / trás / lados). A câmera é o mouse ou o analógico direito."),
        (Sprint, "Correr (gasta estamina). Pode ser alternar em vez de segurar (aba Controles)."),
        (Shoot, "Com a bola: segure para mirar e solte para arremessar (a distância vem da altura da câmera; arremessa sozinho depois de um tempo). Também é o botão dos skill checks (enterrada, lances livres e cutscenes)."),
        (Pass, "Com a bola: toque = passe rápido para o aliado livre mais perto; segure = passe na direção da câmera."),
        (Variation, "Durante o passe: troca entre passe normal e passe alto."),
        (Jump, "Pula ao soltar (segure para pular mais alto). No ar bloqueia arremessos e passes altos. Perto da cesta adversária com a bola: enterrada. Carregando o arremesso: arremesso no pulo."),
        (Dribble, "Com a bola: drible especial (imune a roubo, gasta 1 carga). Sem a bola: roubo (bote para frente; errar deixa você lento)."),
        (Emote, "Segure para abrir a roda de expressões; aponte e solte para escolher."),
        (Ab1, "Habilidade 1 do personagem."),
        (Ab2, "Habilidade 2 do personagem."),
        (Ab3, "Habilidade 3 do personagem."),
        (Ult, "Ultimate (quando a barra dourada estiver cheia)."),
        (Pause, "Pausa (offline). Online o jogo continua."),
    };
}

/// <summary>Cria o InputMap em código (padrões + remapeamentos do jogador).</summary>
public static class InputSetup
{
    static readonly Dictionary<string, Bind> DefaultKb = new()
    {
        [Act.MoveF] = new("key", (int)Key.W), [Act.MoveB] = new("key", (int)Key.S),
        [Act.MoveL] = new("key", (int)Key.A), [Act.MoveR] = new("key", (int)Key.D),
        [Act.Sprint] = new("key", (int)Key.Shift),
        [Act.Shoot] = new("mouse", (int)MouseButton.Left),
        [Act.Pass] = new("mouse", (int)MouseButton.Right),
        [Act.Jump] = new("key", (int)Key.Space),
        [Act.Dribble] = new("key", (int)Key.E), // mesmo do roubo: drible só com a bola, roubo só sem
        [Act.Steal] = new("key", (int)Key.E),
        [Act.Variation] = new("key", (int)Key.R),
        [Act.Emote] = new("key", (int)Key.Q),
        [Act.Ab1] = new("key", (int)Key.Key1), [Act.Ab2] = new("key", (int)Key.Key2),
        [Act.Ab3] = new("key", (int)Key.Key3), [Act.Ult] = new("key", (int)Key.Key4),
    };

    static readonly Dictionary<string, Bind> DefaultPad = new()
    {
        [Act.MoveF] = new("joyaxis", (int)JoyAxis.LeftY, -1), [Act.MoveB] = new("joyaxis", (int)JoyAxis.LeftY, 1),
        [Act.MoveL] = new("joyaxis", (int)JoyAxis.LeftX, -1), [Act.MoveR] = new("joyaxis", (int)JoyAxis.LeftX, 1),
        [Act.Sprint] = new("joyaxis", (int)JoyAxis.TriggerLeft, 1),
        [Act.Shoot] = new("joyaxis", (int)JoyAxis.TriggerRight, 1),
        [Act.Pass] = new("joybutton", (int)JoyButton.RightShoulder),
        [Act.Jump] = new("joybutton", (int)JoyButton.A),
        [Act.Dribble] = new("joybutton", (int)JoyButton.X), // v0.21: drible e roubo no mesmo botão (como no teclado)
        [Act.Steal] = new("joybutton", (int)JoyButton.X),
        [Act.Variation] = new("joybutton", (int)JoyButton.Y),
        [Act.Emote] = new("joybutton", (int)JoyButton.B),   // v0.21: roda de expressões
        // v0.26: setinhas = habilidades (← 1, ↑ 2, → 3, ↓ ultimate)
        [Act.Ab1] = new("joybutton", (int)JoyButton.DpadLeft), [Act.Ab2] = new("joybutton", (int)JoyButton.DpadUp),
        [Act.Ab3] = new("joybutton", (int)JoyButton.DpadRight), [Act.Ult] = new("joybutton", (int)JoyButton.DpadDown),
    };

    public static Bind GetKb(string action) =>
        UserSettings.I.KeyboardBinds.TryGetValue(action, out var b) ? b : DefaultKb.GetValueOrDefault(action);

    public static Bind GetPad(string action) =>
        UserSettings.I.PadBinds.TryGetValue(action, out var b) ? b : DefaultPad.GetValueOrDefault(action);

    public static void ResetToDefaults()
    {
        UserSettings.I.KeyboardBinds.Clear();
        UserSettings.I.PadBinds.Clear();
    }

    /// <summary>Ações que podem dividir o mesmo botão (nunca valem ao mesmo tempo).</summary>
    static bool Compatible(string a, string b) =>
        (a == Act.Dribble && b == Act.Steal) || (a == Act.Steal && b == Act.Dribble);

    /// <summary>Define o bind e, se outra ação usava o mesmo, troca (swap) para não haver conflito.</summary>
    public static void SetBind(string action, Bind b, bool pad)
    {
        var dict = pad ? UserSettings.I.PadBinds : UserSettings.I.KeyboardBinds;
        var old = pad ? GetPad(action) : GetKb(action);
        foreach (var (other, _, _) in Act.Rebindable)
        {
            if (other == action || Compatible(action, other)) continue;
            var ob = pad ? GetPad(other) : GetKb(other);
            if (ob != null && ob.SameAs(b)) dict[other] = old?.Clone();
        }
        dict[action] = b;
    }

    /// <summary>Muda a cada Apply (remapeamento) — a barra de dicas se redesenha.</summary>
    public static int Revision { get; private set; }

    public static void Apply()
    {
        Revision++;
        void Ensure(string a, float dz = 0.25f)
        {
            if (InputMap.HasAction(a)) InputMap.ActionEraseEvents(a);
            else InputMap.AddAction(a, dz);
        }

        foreach (var (a, _, _) in Act.Rebindable)
        {
            Ensure(a);
            var kb = GetKb(a); if (kb != null) InputMap.ActionAddEvent(a, ToEvent(kb));
            var pad = GetPad(a); if (pad != null) InputMap.ActionAddEvent(a, ToEvent(pad));
        }
        Ensure(Act.LookL, 0.15f); InputMap.ActionAddEvent(Act.LookL, ToEvent(new Bind("joyaxis", (int)JoyAxis.RightX, -1)));
        Ensure(Act.LookR, 0.15f); InputMap.ActionAddEvent(Act.LookR, ToEvent(new Bind("joyaxis", (int)JoyAxis.RightX, 1)));
        Ensure(Act.LookU, 0.15f); InputMap.ActionAddEvent(Act.LookU, ToEvent(new Bind("joyaxis", (int)JoyAxis.RightY, -1)));
        Ensure(Act.LookD, 0.15f); InputMap.ActionAddEvent(Act.LookD, ToEvent(new Bind("joyaxis", (int)JoyAxis.RightY, 1)));
        Ensure(Act.Pause);
        InputMap.ActionAddEvent(Act.Pause, ToEvent(new Bind("key", (int)Key.Escape)));
        InputMap.ActionAddEvent(Act.Pause, ToEvent(new Bind("joybutton", (int)JoyButton.Start)));
        Ensure(Act.ReloadTuning);
        InputMap.ActionAddEvent(Act.ReloadTuning, ToEvent(new Bind("key", (int)Key.F5)));
    }

    public static InputEvent ToEvent(Bind b)
    {
        InputEvent e = b.Kind switch
        {
            "mouse" => new InputEventMouseButton { ButtonIndex = (MouseButton)b.Code },
            "joybutton" => new InputEventJoypadButton { ButtonIndex = (JoyButton)b.Code },
            "joyaxis" => new InputEventJoypadMotion { Axis = (JoyAxis)b.Code, AxisValue = b.Axis >= 0 ? 1f : -1f },
            _ => new InputEventKey { PhysicalKeycode = (Key)b.Code },
        };
        e.Device = -1; // todos os dispositivos
        return e;
    }

    /// <summary>Converte um evento capturado na tela de remapeamento. pad=true aceita só controle.</summary>
    public static Bind FromEvent(InputEvent e, bool pad)
    {
        if (!pad)
        {
            if (e is InputEventKey k && k.Pressed && !k.Echo)
            {
                var code = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode;
                return new Bind("key", (int)code);
            }
            if (e is InputEventMouseButton m && m.Pressed && m.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown))
                return new Bind("mouse", (int)m.ButtonIndex);
        }
        else
        {
            if (e is InputEventJoypadButton jb && jb.Pressed) return new Bind("joybutton", (int)jb.ButtonIndex);
            if (e is InputEventJoypadMotion jm && Mathf.Abs(jm.AxisValue) > 0.6f)
                return new Bind("joyaxis", (int)jm.Axis, Mathf.Sign(jm.AxisValue));
        }
        return null;
    }

    public static string Describe(Bind b)
    {
        if (b == null) return "—";
        switch (b.Kind)
        {
            case "mouse":
                return (MouseButton)b.Code switch
                {
                    MouseButton.Left => "Mouse Esquerdo", MouseButton.Right => "Mouse Direito",
                    MouseButton.Middle => "Mouse Meio", MouseButton.Xbutton1 => "Mouse 4", MouseButton.Xbutton2 => "Mouse 5",
                    _ => "Mouse " + b.Code,
                };
            case "joybutton":
                return (JoyButton)b.Code switch
                {
                    JoyButton.A => "A", JoyButton.B => "B", JoyButton.X => "X", JoyButton.Y => "Y",
                    JoyButton.LeftShoulder => "LB", JoyButton.RightShoulder => "RB",
                    JoyButton.Back => "View", JoyButton.Start => "Menu", JoyButton.Guide => "Xbox",
                    JoyButton.LeftStick => "LS", JoyButton.RightStick => "RS",
                    JoyButton.DpadUp => "D-Pad ↑", JoyButton.DpadDown => "D-Pad ↓",
                    JoyButton.DpadLeft => "D-Pad ←", JoyButton.DpadRight => "D-Pad →",
                    _ => "Botão " + b.Code,
                };
            case "joyaxis":
                return (JoyAxis)b.Code switch
                {
                    JoyAxis.TriggerLeft => "LT", JoyAxis.TriggerRight => "RT",
                    JoyAxis.LeftX => b.Axis < 0 ? "Analógico E ←" : "Analógico E →",
                    JoyAxis.LeftY => b.Axis < 0 ? "Analógico E ↑" : "Analógico E ↓",
                    JoyAxis.RightX => b.Axis < 0 ? "Analógico D ←" : "Analógico D →",
                    JoyAxis.RightY => b.Axis < 0 ? "Analógico D ↑" : "Analógico D ↓",
                    _ => "Eixo " + b.Code,
                };
            default:
                return OS.GetKeycodeString((Key)b.Code);
        }
    }
}
