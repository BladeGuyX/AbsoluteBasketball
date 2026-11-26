using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// v0.26: menu para escolher um jogador adversário (Luck: "Não me Decepciona"; Bolt: ultimate "Duelo").
/// v0.27: virou um painel com o cursor do mouse solto — passe o mouse em cima do adversário e clique nele. No controle (ou nas
/// setinhas ←/→ do teclado) dá para navegar e confirmar com arremesso/salto. Cancelar: passe, Esc, clicar fora, o botão
/// "Cancelar" ou a mesma tecla de novo. Fecha sozinho depois de alguns segundos. A escolha vai no input
/// (<see cref="PlayerInput.Target"/>).
/// </summary>
public partial class TargetPicker : Control
{
    public static bool IsOpen { get; private set; }
    /// <summary>Quem está selecionado agora (a setinha amarela em cima dele).</summary>
    public static Player Current { get; private set; }
    static byte _chosen, _chosenSlot;

    /// <summary>Alvo escolhido desde o último tick (0 = nada). Lido pelo LocalInputReader.</summary>
    public static byte TakeChosen(out byte slot)
    {
        var c = _chosen; slot = _chosenSlot;
        _chosen = 0;
        return c;
    }

    public Match M;
    int _slot = -1, _sel, _hover = -1;
    float _open, _anim, _navCd;
    List<Player> _list = new();
    readonly List<Rect2> _cards = new();
    Rect2 _cancel, _panel;
    bool _hoverCancel;

    const float AutoClose = 12f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        UiKit.FullRect(this);
        IsOpen = false; Current = null;
    }

    public override void _ExitTree() { IsOpen = false; Current = null; }

    static readonly string[] SlotActions = { Act.Ab1, Act.Ab2, Act.Ab3, Act.Ult };

    public override void _Input(InputEvent e)
    {
        if (!IsOpen) return;
        if (e is InputEventMouseMotion)
        {
            UpdateHover();
            if (_hover >= 0) _sel = _hover;
        }
        else if (e is InputEventMouseButton { Pressed: true } mb && _open > 0.08f)
        {
            GetViewport().SetInputAsHandled();
            UpdateHover();
            if (mb.ButtonIndex == MouseButton.Right) { Close(); return; }
            if (mb.ButtonIndex != MouseButton.Left) return;
            if (_hover >= 0) Choose(_hover);
            else if (_hoverCancel || !_panel.HasPoint(GetLocalMousePosition())) Close();
        }
        else if (e is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }

    void UpdateHover()
    {
        var m = GetLocalMousePosition();
        _hover = -1;
        for (int i = 0; i < _cards.Count; i++) if (_cards[i].HasPoint(m)) { _hover = i; break; }
        _hoverCancel = _cancel.HasPoint(m);
    }

    void Move(int d)
    {
        if (_list.Count == 0) return;
        _sel = (_sel + d + _list.Count) % _list.Count;
    }

    void Choose(int i)
    {
        if (i < 0 || i >= _list.Count) return;
        _chosen = (byte)_list[i].Id; _chosenSlot = (byte)_slot;
        Sfx.Play2D(this, "beep", -14, 1.4f);
        Close();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var lp = M?.LocalPlayer;
        bool can = lp != null && GodotObject.IsInstanceValid(lp) && !M.InputBlocked && M.AbilitiesOn && M.CanAct && GetWindow().HasFocus();
        if (!IsOpen && can && !EmoteWheel.IsOpen)
        {
            for (int s = 0; s < 4; s++)
            {
                if (!Characters.NeedsTarget(lp.CharacterId, s) || !Input.IsActionJustPressed(SlotActions[s])) continue;
                if (!lp.Ab.Ready(s) || (s == 3 && !lp.HasBall)) continue;
                _list = M.Players.Where(q => q.Team != lp.Team).OrderBy(q => q.GlobalPosition.DistanceSquaredTo(lp.GlobalPosition)).ToList();
                if (_list.Count == 0) continue;
                _slot = s; _sel = 0; _hover = -1; _open = 0; IsOpen = true;
                break;
            }
        }
        else if (IsOpen)
        {
            _open += dt;
            if (_navCd > 0) _navCd -= dt;
            _list.RemoveAll(q => !GodotObject.IsInstanceValid(q));
            if (!can || _list.Count == 0 || _open > AutoClose || Input.IsActionJustPressed(Act.Pass) || (_open > 0.1f && Input.IsActionJustPressed(SlotActions[_slot]))) Close();
            else
            {
                if (InputDevice.Gamepad)
                {
                    float x = Input.GetAxis(Act.MoveL, Act.MoveR);
                    if (Mathf.Abs(x) > 0.6f && _navCd <= 0) { Move(x > 0 ? 1 : -1); _navCd = 0.22f; }
                    else if (Mathf.Abs(x) < 0.3f) _navCd = 0;
                }
                if (Input.IsActionJustPressed(Act.Ab1)) Move(-1);
                if (Input.IsActionJustPressed(Act.Ab3)) Move(1);
                if (_open > 0.08f && InputDevice.Gamepad && (Input.IsActionJustPressed(Act.Shoot) || Input.IsActionJustPressed(Act.Jump))) Choose(_sel);
                else if (_open > 0.08f && Input.IsActionJustPressed(Act.Jump)) Choose(_sel); // teclado: Espaço confirma a seleção
            }
        }
        Current = IsOpen && _list.Count > 0 ? _list[Mathf.Clamp(_sel, 0, _list.Count - 1)] : null;
        _anim = Mathf.MoveToward(_anim, IsOpen ? 1f : 0f, dt * 9f);
        Visible = _anim > 0.001f;
        if (Visible) QueueRedraw();
    }

    /// <summary>Abre na hora (print de teste).</summary>
    public void OpenFor(int slot)
    {
        var lp = M?.LocalPlayer;
        if (lp == null) return;
        _list = M.Players.Where(q => q.Team != lp.Team).OrderBy(q => q.GlobalPosition.DistanceSquaredTo(lp.GlobalPosition)).ToList();
        _slot = slot; _sel = 0; _hover = 1; _open = 0; IsOpen = _list.Count > 0;
    }

    void Close() { IsOpen = false; _slot = Mathf.Max(_slot, 0); _hover = -1; _blockUntil = Time.GetTicksMsec() + 180; }

    static ulong _blockUntil;
    /// <summary>Aberto (ou acabou de fechar): os botões de ação não vão para o jogador (o aperto que confirmou não vira arremesso).</summary>
    public static bool Blocking => IsOpen || Time.GetTicksMsec() < _blockUntil;

    public override void _Draw()
    {
        var lp = M?.LocalPlayer;
        _cards.Clear();
        if (lp == null || _list.Count == 0 || !GodotObject.IsInstanceValid(lp)) return;
        float s = Size.Y / 1080f;
        var f = UiKit.Font;
        var fb = UiKit.FontBold;
        var ab = Characters.Get(lp.CharacterId).Abilities[Mathf.Clamp(_slot, 0, 3)];
        float a = _anim;
        float pop = 0.92f + 0.08f * a;

        // fundo escurecido
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.02f, 0.03f, 0.07f, 0.45f * a));

        int n = _list.Count;
        int perRow = Mathf.Min(n, 5);
        int rows = (n + perRow - 1) / perRow;
        float cw = 250 * s * pop, ch = 300 * s * pop, gap = 22 * s;
        float pw = perRow * cw + (perRow - 1) * gap + 80 * s;
        float ph = rows * ch + (rows - 1) * gap + 230 * s;
        var center = Size / 2 + new Vector2(0, -20 * s);
        _panel = new Rect2(center - new Vector2(pw, ph) / 2, new Vector2(pw, ph));
        UiKit.DrawPanel(this, _panel, ab.Color, a, 26 * s);

        // título com o ícone da habilidade
        float ty = _panel.Position.Y + 58 * s;
        AbilityIcons.Draw(this, ab.Icon, new Vector2(_panel.Position.X + 64 * s, ty), 30 * s, ab.Color, a);
        DrawString(fb, new Vector2(_panel.Position.X + 110 * s, ty - 4 * s), ab.Name, HorizontalAlignment.Left, -1, (int)(34 * s), new Color(1, 1, 1, a));
        DrawString(f, new Vector2(_panel.Position.X + 112 * s, ty + 26 * s), "Clique no adversário escolhido", HorizontalAlignment.Left, -1, (int)(20 * s), new Color(0.75f, 0.82f, 0.95f, a));

        float y0 = _panel.Position.Y + 120 * s;
        for (int i = 0; i < n; i++)
        {
            int row = i / perRow, col = i % perRow;
            int inRow = Mathf.Min(perRow, n - row * perRow);
            float rowW = inRow * cw + (inRow - 1) * gap;
            var q = _list[i];
            bool sel = i == _sel;
            bool hov = i == _hover;
            float lift = (sel ? 8 : 0) * s;
            var r = new Rect2(center.X - rowW / 2 + col * (cw + gap), y0 + row * (ch + gap) - lift, cw, ch);
            _cards.Add(r);
            var cd = Characters.Get(q.CharacterId);
            var team = Palette.Team[q.Team];
            // sombra + cartão
            DrawStyled(r with { Position = r.Position + new Vector2(0, 6 * s) }, new Color(0, 0, 0, 0.35f * a), 18 * s);
            DrawStyled(r, (sel ? new Color(0.16f, 0.19f, 0.28f) : new Color(0.11f, 0.13f, 0.2f)) with { A = 0.97f * a }, 18 * s);
            // faixa da cor do personagem no topo
            DrawStyled(new Rect2(r.Position, new Vector2(cw, 120 * s)), cd.Color.Darkened(0.35f) with { A = a }, 18 * s, bottom: false);
            // "retrato": círculo do personagem com a inicial
            var pc = r.Position + new Vector2(cw / 2, 70 * s);
            DrawCircle(pc, 50 * s, new Color(0.06f, 0.07f, 0.1f, a));
            DrawCircle(pc, 45 * s, cd.Color with { A = a });
            DrawArc(pc, 47 * s, 0, Mathf.Tau, 48, team.Lightened(0.2f) with { A = a }, 4 * s, true);
            string ini = cd.Name[..1];
            int isz = (int)(52 * s);
            var isize = fb.GetStringSize(ini, HorizontalAlignment.Left, -1, isz);
            DrawString(fb, pc + new Vector2(-isize.X / 2, isz * 0.36f), ini, HorizontalAlignment.Left, -1, isz, new Color(1, 1, 1, a));
            // nome e personagem
            string nm = q.PlayerName.Length > 16 ? q.PlayerName[..16] : q.PlayerName;
            int ns = (int)(26 * s);
            var nsz = fb.GetStringSize(nm, HorizontalAlignment.Left, -1, ns);
            DrawString(fb, new Vector2(r.Position.X + cw / 2 - nsz.X / 2, r.Position.Y + 170 * s), nm, HorizontalAlignment.Left, -1, ns, team.Lightened(0.35f) with { A = a });
            float dist = (q.GlobalPosition - lp.GlobalPosition).Length();
            string sub = $"{cd.Name} · {dist:0} m";
            int ss = (int)(19 * s);
            var ssz = f.GetStringSize(sub, HorizontalAlignment.Left, -1, ss);
            DrawString(f, new Vector2(r.Position.X + cw / 2 - ssz.X / 2, r.Position.Y + 202 * s), sub, HorizontalAlignment.Left, -1, ss, new Color(0.72f, 0.78f, 0.9f, a));
            // estrelas resumidas (precisão e salto) não cabem — mostra a cor do time numa barrinha
            DrawStyled(new Rect2(r.Position.X + 24 * s, r.Position.Y + ch - 64 * s, cw - 48 * s, 40 * s), (hov || sel ? new Color("ffd23f") : team) with { A = (hov || sel ? 1f : 0.85f) * a }, 12 * s);
            string act = hov || sel ? "ESCOLHER" : Palette.TeamName[q.Team];
            int bs = (int)(20 * s);
            var bsz = fb.GetStringSize(act, HorizontalAlignment.Left, -1, bs);
            DrawString(fb, new Vector2(r.Position.X + cw / 2 - bsz.X / 2, r.Position.Y + ch - 37 * s), act, HorizontalAlignment.Left, -1, bs, (hov || sel ? Palette.Ink : Colors.White) with { A = a });
            // contorno de seleção
            if (sel || hov) DrawStyledBorder(r.Grow(3 * s), new Color("ffd23f") with { A = a }, 20 * s, 4 * s);
        }

        // cancelar + dica
        float by = _panel.End.Y - 72 * s;
        _cancel = new Rect2(center.X - 110 * s, by, 220 * s, 48 * s);
        DrawStyled(_cancel, (_hoverCancel ? new Color(0.3f, 0.33f, 0.42f) : new Color(0.2f, 0.23f, 0.3f)) with { A = a }, 14 * s);
        int cs = (int)(22 * s);
        var csz = fb.GetStringSize("Cancelar", HorizontalAlignment.Left, -1, cs);
        DrawString(fb, new Vector2(center.X - csz.X / 2, by + 32 * s), "Cancelar", HorizontalAlignment.Left, -1, cs, new Color(1, 1, 1, a));
        string hint = InputDevice.Gamepad ? "← → escolher · " + InputDevice.Glyph(Act.Shoot) + " confirmar · " + InputDevice.Glyph(Act.Pass) + " cancelar"
                                          : "clique para escolher · Esc ou botão direito cancela";
        int fs = (int)(17 * s);
        var sz = f.GetStringSize(hint, HorizontalAlignment.Left, -1, fs);
        DrawString(f, new Vector2(center.X - sz.X / 2, _panel.End.Y - 8 * s), hint, HorizontalAlignment.Left, -1, fs, new Color(0.7f, 0.76f, 0.88f, a));
    }

    void DrawStyled(Rect2 r, Color c, float radius, bool bottom = true)
    {
        var sb = new StyleBoxFlat { BgColor = c, AntiAliasing = true };
        sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = (int)radius;
        sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = bottom ? (int)radius : 0;
        DrawStyleBox(sb, r);
    }

    void DrawStyledBorder(Rect2 r, Color c, float radius, float w)
    {
        var sb = new StyleBoxFlat { DrawCenter = false, BorderColor = c, AntiAliasing = true };
        sb.SetBorderWidthAll((int)Mathf.Max(1, w));
        sb.SetCornerRadiusAll((int)radius);
        DrawStyleBox(sb, r);
    }
}
