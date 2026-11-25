using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// v0.26: menu para escolher um jogador adversário (Luck: "Não me Decepciona"; Bolt: ultimate "Duelo").
/// Abre ao apertar a habilidade (se estiver pronta). Escolher: mouse para os lados, analógico esquerdo ou setinhas ←/→ no
/// controle (no teclado dá para continuar andando). Confirmar: arremesso ou salto (ou clicar). Cancelar: passe ou a mesma
/// tecla de novo. Fecha sozinho depois de alguns segundos. A escolha vai no input (<see cref="PlayerInput.Target"/>).
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
    int _slot = -1, _sel;
    float _open, _mouseX, _anim, _navCd;
    List<Player> _list = new();

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
        if (e is InputEventMouseMotion mm)
        {
            _mouseX += mm.ScreenRelative.X;
            if (Mathf.Abs(_mouseX) > 70f) { Move(_mouseX > 0 ? 1 : -1); _mouseX = 0; }
        }
    }

    void Move(int d)
    {
        if (_list.Count == 0) return;
        _sel = (_sel + d + _list.Count) % _list.Count;
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
                _slot = s; _sel = 0; _open = 0; _mouseX = 0; IsOpen = true;
                break;
            }
        }
        else if (IsOpen)
        {
            _open += dt;
            if (_navCd > 0) _navCd -= dt;
            if (!can || _open > 7f || Input.IsActionJustPressed(Act.Pass) || (_open > 0.1f && Input.IsActionJustPressed(SlotActions[_slot]))) Close();
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
                if (_open > 0.08f && (Input.IsActionJustPressed(Act.Shoot) || Input.IsActionJustPressed(Act.Jump)) && _list.Count > 0)
                {
                    _chosen = (byte)_list[_sel].Id; _chosenSlot = (byte)_slot;
                    Close();
                }
            }
        }
        Current = IsOpen && _list.Count > 0 ? _list[Mathf.Clamp(_sel, 0, _list.Count - 1)] : null;
        _anim = Mathf.MoveToward(_anim, IsOpen ? 1f : 0f, dt * 9f);
        Visible = _anim > 0.001f;
        if (Visible) QueueRedraw();
    }

    void Close() { IsOpen = false; _slot = Mathf.Max(_slot, 0); _blockUntil = Time.GetTicksMsec() + 180; }

    static ulong _blockUntil;
    /// <summary>Aberto (ou acabou de fechar): os botões de ação não vão para o jogador (o aperto que confirmou não vira arremesso).</summary>
    public static bool Blocking => IsOpen || Time.GetTicksMsec() < _blockUntil;

    public override void _Draw()
    {
        var lp = M?.LocalPlayer;
        if (lp == null || _list.Count == 0) return;
        float s = Size.Y / 1080f;
        var f = ThemeDB.FallbackFont;
        var ab = Characters.Get(lp.CharacterId).Abilities[Mathf.Clamp(_slot, 0, 3)];
        float a = _anim;
        float cw = 230 * s, ch = 92 * s, gap = 16 * s;
        float total = _list.Count * cw + (_list.Count - 1) * gap;
        float x0 = Size.X / 2 - total / 2, y0 = Size.Y * 0.16f;
        string head = ab.Name + ": escolha um adversário";
        int hs = (int)(30 * s);
        var hsz = f.GetStringSize(head, HorizontalAlignment.Left, -1, hs);
        DrawStringOutline(f, new Vector2(Size.X / 2 - hsz.X / 2, y0 - 18 * s), head, HorizontalAlignment.Left, -1, hs, 8, new Color(0, 0, 0, 0.85f * a));
        DrawString(f, new Vector2(Size.X / 2 - hsz.X / 2, y0 - 18 * s), head, HorizontalAlignment.Left, -1, hs, new Color(1, 1, 1, a));
        for (int i = 0; i < _list.Count; i++)
        {
            var q = _list[i];
            bool sel = i == _sel;
            var r = new Rect2(x0 + i * (cw + gap), y0 + (sel ? -6 * s : 0), cw, ch);
            DrawRect(r, new Color(0.06f, 0.07f, 0.11f, 0.85f * a));
            DrawRect(r, (sel ? new Color("ffd23f") : Palette.Team[q.Team]) with { A = a }, false, sel ? 4 * s : 2 * s);
            var cd = Characters.Get(q.CharacterId);
            DrawCircle(r.Position + new Vector2(38 * s, ch / 2), 24 * s, cd.Color with { A = a });
            string n = q.PlayerName.Length > 14 ? q.PlayerName[..14] : q.PlayerName;
            DrawString(f, r.Position + new Vector2(72 * s, 38 * s), n, HorizontalAlignment.Left, -1, (int)(22 * s), new Color(1, 1, 1, a));
            float dist = (q.GlobalPosition - lp.GlobalPosition).Length();
            DrawString(f, r.Position + new Vector2(72 * s, 68 * s), $"{cd.Name} · {dist:0} m", HorizontalAlignment.Left, -1, (int)(17 * s), new Color(0.75f, 0.8f, 0.9f, a));
        }
        string hint = InputDevice.Gamepad ? "← → escolher · " + InputDevice.Glyph(Act.Shoot) + " confirmar · " + InputDevice.Glyph(Act.Pass) + " cancelar"
                                          : "mouse para os lados escolhe · " + InputDevice.Glyph(Act.Shoot) + " confirma · " + InputDevice.Glyph(Act.Pass) + " cancela";
        int fs = (int)(18 * s);
        var sz = f.GetStringSize(hint, HorizontalAlignment.Left, -1, fs);
        DrawStringOutline(f, new Vector2(Size.X / 2 - sz.X / 2, y0 + ch + 34 * s), hint, HorizontalAlignment.Left, -1, fs, 6, new Color(0, 0, 0, 0.8f * a));
        DrawString(f, new Vector2(Size.X / 2 - sz.X / 2, y0 + ch + 34 * s), hint, HorizontalAlignment.Left, -1, fs, new Color(1, 1, 1, a));
    }
}
