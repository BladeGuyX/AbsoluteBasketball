using Godot;

namespace AB;

/// <summary>
/// v0.26: ícones das 3 habilidades e da ultimate no rodapé, pequenos, com a tecla/botão embaixo. Em recarga: o ícone fica
/// escuro e uma rodela vai enchendo até ficar pronta. Ultimate: anel dourado com a carga (brilha quando cheia).
/// Habilidade ativa (Olho do Rei, Presença, modo apostador...): anel branco que esvazia com o tempo restante.
/// Some no modo sem habilidades.
/// </summary>
public partial class AbilityBar : Control
{
    public Match M;
    static readonly string[] Actions = { Act.Ab1, Act.Ab2, Act.Ab3, Act.Ult };

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var p = M?.LocalPlayer;
        if (p == null || !M.AbilitiesOn || M.Cut.Active) return;
        var def = Characters.Get(p.CharacterId);
        float s = Mathf.Clamp(Size.Y / 90f, 0.6f, 1.6f);
        float r = 24f * s, ru = 30f * s, gap = 14f * s;
        float total = 3 * (2 * r) + 2 * ru + 3 * gap;
        float x = Size.X / 2f - total / 2f;
        float y = Size.Y / 2f - 4f * s;
        var f = ThemeDB.FallbackFont;
        for (int i = 0; i < 4; i++)
        {
            float rr = i == 3 ? ru : r;
            var c = new Vector2(x + rr, y - (i == 3 ? 4 * s : 0));
            x += rr * 2 + gap;
            var ab = def.Abilities[i];
            bool ready = p.Ab.Ready(i) && CanUse(p, i);
            AbilityIcons.Draw(this, ab.Icon, c, rr, ab.Color, ready ? 1f : 0.55f);
            if (i < 3 && p.Ab.Cd[i] > 0)
            {
                // recarga: rodela escura que vai sumindo (enchendo o ícone) no sentido horário
                float frac = p.Ab.CdFrac(i);
                DrawCircle(c, rr * 0.9f, new Color(0, 0, 0, 0.35f));
                float a0 = -Mathf.Pi / 2;
                var pts = new Vector2[34];
                pts[0] = c;
                for (int k = 0; k <= 32; k++) { float a = a0 + Mathf.Tau * (1f - frac) + Mathf.Tau * frac * k / 32f; pts[k + 1] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr * 0.9f; }
                if (frac > 0.01f) DrawColoredPolygon(pts, new Color(0.02f, 0.03f, 0.06f, 0.6f));
                DrawArc(c, rr * 1.02f, a0, a0 + Mathf.Tau * (1f - frac), 40, new Color(1, 1, 1, 0.85f), 2.5f * s, true);
                string sec = Mathf.CeilToInt(p.Ab.Cd[i]).ToString();
                var ssz = f.GetStringSize(sec, HorizontalAlignment.Left, -1, (int)(17 * s));
                DrawStringOutline(f, c + new Vector2(-ssz.X / 2, 6 * s), sec, HorizontalAlignment.Left, -1, (int)(17 * s), 5, new Color(0, 0, 0, 0.9f));
                DrawString(f, c + new Vector2(-ssz.X / 2, 6 * s), sec, HorizontalAlignment.Left, -1, (int)(17 * s), Colors.White);
            }
            if (i == 3)
            {
                float u = Mathf.Clamp(p.Ab.Ult, 0, 1);
                var gold = new Color("ffd23f");
                DrawArc(c, rr * 1.08f, 0, Mathf.Tau, 48, new Color(0, 0, 0, 0.55f), 6 * s, true);
                if (u > 0.001f) DrawArc(c, rr * 1.08f, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * u, 48, gold, 4 * s, true);
                if (u >= 0.999f)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() * 0.008f);
                    DrawArc(c, rr * (1.2f + 0.06f * pulse), 0, Mathf.Tau, 48, gold with { A = 0.35f + 0.4f * pulse }, 3 * s, true);
                }
            }
            float act = ActiveFrac(p, i);
            if (act > 0) DrawArc(c, rr * 1.2f, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * act, 40, new Color(1, 1, 1, 0.95f), 3 * s, true);
            // tecla/botão
            string g = InputDevice.Glyph(Actions[i]);
            int fs = (int)(13 * s);
            var gsz = f.GetStringSize(g, HorizontalAlignment.Left, -1, fs);
            var gp = c + new Vector2(-gsz.X / 2, rr + 17 * s);
            var box = new Rect2(gp.X - 5 * s, gp.Y - fs, gsz.X + 10 * s, fs + 6 * s);
            DrawRect(box, new Color(0.08f, 0.1f, 0.15f, 0.85f));
            DrawString(f, gp, g, HorizontalAlignment.Left, -1, fs, Colors.White);
        }
    }

    /// <summary>Condições extras (com/sem bola etc.) só para escurecer o ícone; o host decide de verdade.</summary>
    static bool CanUse(Player p, int slot)
    {
        return (p.CharacterId, slot) switch
        {
            (Characters.King, 2) or (Characters.King, 3) or (Characters.Luck, 0) or (Characters.Bolt, 0) or (Characters.Bolt, 3) => p.HasBall,
            (Characters.Bolt, 2) => !p.HasBall,
            _ => true,
        };
    }

    static float ActiveFrac(Player p, int slot)
    {
        var t = Tuning.I; var a = p.Ab;
        return (p.CharacterId, slot) switch
        {
            (Characters.King, 0) => a.EyeT > 0 ? a.EyeT / Mathf.Max(t.KingEyeDuration, 0.1f) : a.EyeArmed ? 1f : 0f,
            (Characters.King, 1) => Mathf.Max(0, a.PresenceT) / Mathf.Max(t.KingPresenceDuration, 0.1f),
            (Characters.Luck, 1) => Mathf.Max(0, a.BetT) / Mathf.Max(t.LuckBetDuration, 0.1f),
            (Characters.Luck, 3) => Mathf.Clamp(Mathf.Max(0, a.ModeT) / Mathf.Max(t.LuckModeDuration, 0.1f), 0, 1),
            _ => 0f,
        };
    }
}
