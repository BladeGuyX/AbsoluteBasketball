using Godot;

namespace AB;

/// <summary>
/// v0.26: camada 2D das cutscenes: barras de cinema, vinheta, linhas de velocidade (estilo anime), clarão, "impact frame"
/// (tela em alto contraste por um instante) e o título da ultimate entrando de lado numa faixa inclinada.
/// </summary>
public partial class CutOverlay : Control
{
    public float Bars;          // 0..1 (altura das barras)
    public float Vignette;      // 0..1
    public float Speed;         // 0..1 (linhas de velocidade)
    public float ImpactFrame;   // s restantes do quadro de impacto
    public string Title = "";
    public float TitleT = -1;   // s desde o começo da cutscene (título aparece no início)
    public Color Accent = Colors.Red;
    Color _flashCol = Colors.White;
    float _flash;
    readonly RandomNumberGenerator _rng = new();
    float _lineSeedT;
    readonly float[] _lineA = new float[48];
    readonly float[] _lineL = new float[48];

    public void Flash(Color c, float a) { _flashCol = c; _flash = Mathf.Max(_flash, a); }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_flash > 0) _flash = Mathf.Max(0, _flash - dt * 2.6f);
        if (ImpactFrame > 0) ImpactFrame -= dt;
        _lineSeedT -= dt;
        if (_lineSeedT <= 0)
        {
            _lineSeedT = 0.05f;
            for (int i = 0; i < _lineA.Length; i++) { _lineA[i] = _rng.RandfRange(0, Mathf.Tau); _lineL[i] = _rng.RandfRange(0.25f, 0.55f); }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var sz = Size;
        var c = sz / 2f;
        float diag = c.Length();

        // vinheta escura nas bordas
        if (Vignette > 0.01f)
        {
            int rings = 10;
            for (int i = 0; i < rings; i++)
            {
                float k = i / (float)rings;
                float w = diag * 0.09f;
                DrawArc(c, diag * (1.02f - k * 0.45f), 0, Mathf.Tau, 64, new Color(0, 0, 0, Vignette * 0.09f * (1f - k)), w, false);
            }
        }

        // linhas de velocidade (saem das bordas para o centro)
        if (Speed > 0.01f)
        {
            int n = (int)(_lineA.Length * Mathf.Clamp(Speed, 0, 1));
            for (int i = 0; i < n; i++)
            {
                var d = new Vector2(Mathf.Cos(_lineA[i]), Mathf.Sin(_lineA[i]));
                var p0 = c + d * diag * 1.05f;
                var p1 = c + d * diag * (1.05f - _lineL[i]);
                DrawLine(p0, p1, new Color(1, 1, 1, 0.55f * Speed), 2f + (i % 3), true);
            }
        }

        // quadro de impacto: tudo escuro com a cor de destaque por cima (bem rápido)
        if (ImpactFrame > 0)
        {
            DrawRect(new Rect2(Vector2.Zero, sz), new Color(0, 0, 0, 0.55f));
            DrawRect(new Rect2(Vector2.Zero, sz), Accent with { A = 0.25f });
        }

        // clarão
        if (_flash > 0.01f) DrawRect(new Rect2(Vector2.Zero, sz), _flashCol with { A = Mathf.Clamp(_flash, 0, 1) * 0.85f });

        // barras de cinema
        float bh = sz.Y * 0.11f * Mathf.Clamp(Bars, 0, 1);
        if (bh > 0.5f)
        {
            DrawRect(new Rect2(0, 0, sz.X, bh), Colors.Black);
            DrawRect(new Rect2(0, sz.Y - bh, sz.X, bh), Colors.Black);
            DrawRect(new Rect2(0, bh - 3, sz.X, 3), Accent with { A = 0.8f });
            DrawRect(new Rect2(0, sz.Y - bh, sz.X, 3), Accent with { A = 0.8f });
        }

        // título: faixa inclinada que entra pela esquerda, para e sai pela direita
        if (TitleT >= 0 && TitleT < 1.7f && Title != "")
        {
            float t = TitleT;
            float inK = Mathf.Clamp(t / 0.22f, 0, 1), outK = Mathf.Clamp((t - 1.35f) / 0.3f, 0, 1);
            float ease = 1f - Mathf.Pow(1f - inK, 3f);
            float x = Mathf.Lerp(-sz.X, 0, ease) + outK * outK * sz.X * 1.2f;
            float y = sz.Y * 0.68f;
            float h = sz.Y * 0.12f;
            var band = new[] { new Vector2(x - 40, y), new Vector2(x + sz.X * 0.62f, y), new Vector2(x + sz.X * 0.62f - h * 0.5f, y + h), new Vector2(x - 40 - h * 0.5f, y + h) };
            DrawColoredPolygon(band, new Color(0.04f, 0.04f, 0.06f, 0.92f));
            DrawRect(new Rect2(x - 40, y - 5, sz.X * 0.62f + 40, 5), Accent);
            var f = ThemeDB.FallbackFont;
            int fs = (int)(h * 0.62f);
            var pos = new Vector2(x + sz.X * 0.04f, y + h * 0.72f);
            DrawStringOutline(f, pos + new Vector2(4, 4), Title, HorizontalAlignment.Left, -1, fs, 10, Accent with { A = 0.9f });
            DrawString(f, pos, Title, HorizontalAlignment.Left, -1, fs, Colors.White);
        }
    }
}
