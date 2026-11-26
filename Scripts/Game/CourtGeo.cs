using Godot;

namespace AB;

public enum CourtSize { Small, Medium, Large }

/// <summary>
/// Geometria lógica da quadra (sem nós). Convenção:
/// cesta 0 fica em -X, cesta 1 em +X. Time 0 ataca a cesta 1, time 1 ataca a cesta 0.
/// </summary>
public sealed class CourtGeo
{
    public CourtSize Size;
    public float Length, Width, ThreePt, HalfL, HalfW, HoopInset, Margin;
    public float MinX, MaxX, MinZ, MaxZ; // limites de jogo (paredes invisíveis)
    public readonly Vector3[] RimCenter = new Vector3[2];
    public readonly Aabb[] Board = new Aabb[2];
    public readonly Vector3[] PoleBase = new Vector3[2];
    public float PoleRadius = 0.16f, PoleHeight;
    /// <summary>Quadrado da tabela (tabelinha): metade da largura e faixa de altura.</summary>
    public float BankHalfWidth, BankMinY, BankMaxY;

    public static int MaxPlayers(CourtSize s) => s switch { CourtSize.Small => 6, CourtSize.Medium => 8, _ => 10 };
    public static string Name(CourtSize s) => s switch { CourtSize.Small => "Pequena", CourtSize.Medium => "Média", _ => "Grande" };

    public static CourtGeo Build(CourtSize size)
    {
        var t = Tuning.I;
        var (len, wid, three) = size switch
        {
            CourtSize.Small => (t.CourtSmallLength, t.CourtSmallWidth, t.ThreePtSmall),
            CourtSize.Medium => (t.CourtMediumLength, t.CourtMediumWidth, t.ThreePtMedium),
            _ => (t.CourtLargeLength, t.CourtLargeWidth, t.ThreePtLarge),
        };
        var g = Create(len, wid, three, t.HoopInset, t.BoundsMargin);
        g.Size = size;
        return g;
    }

    /// <summary>Geometria a partir de medidas livres (cena Court.tscn no editor usa os [Export] dela).</summary>
    public static CourtGeo Create(float length, float width, float threePt, float hoopInset, float margin)
    {
        var t = Tuning.I;
        var g = new CourtGeo { Length = length, Width = width, ThreePt = threePt, HoopInset = hoopInset, Margin = margin };
        g.HalfL = g.Length / 2f;
        g.HalfW = g.Width / 2f;
        g.MinX = -g.HalfL - margin; g.MaxX = g.HalfL + margin;
        g.MinZ = -g.HalfW - margin; g.MaxZ = g.HalfW + margin;

        float boardThick = 0.08f, boardW = t.BoardWidth, boardH = t.BoardHeight;
        float boardBottom = t.HoopHeight - t.RimRadius * 0.5f;
        g.PoleHeight = boardBottom + boardH;
        g.BankHalfWidth = t.RimRadius * 1.6f;
        g.BankMinY = t.HoopHeight + 0.05f;
        g.BankMaxY = Mathf.Min(t.HoopHeight + t.RimRadius * 2.4f, boardBottom + boardH - 0.05f);
        for (int h = 0; h < 2; h++)
        {
            float s = HoopSign(h);
            g.RimCenter[h] = new Vector3(s * (g.HalfL - hoopInset), t.HoopHeight, 0);
            float bx = s * (g.HalfL - hoopInset + t.RimRadius + 0.25f + boardThick / 2f);
            var bc = new Vector3(bx, boardBottom + boardH / 2f, 0);
            var he = new Vector3(boardThick / 2f, boardH / 2f, boardW / 2f);
            g.Board[h] = new Aabb(bc - he, he * 2f);
            g.PoleBase[h] = new Vector3(s * (g.HalfL + 0.8f), 0, 0);
        }
        return g;
    }

    public static float HoopSign(int hoop) => hoop == 1 ? 1f : -1f;
    public static int AttackHoop(int team) => team == 0 ? 1 : 0;
    public static int DefendHoop(int team) => team == 0 ? 0 : 1;
    /// <summary>Time que marca ponto quando a bola entra nesta cesta.</summary>
    public static int ScoringTeamForHoop(int hoop) => hoop == 1 ? 0 : 1;

    /// <summary>A posição (pés) está fora da linha de 3 em relação à cesta?</summary>
    public bool IsThree(Vector3 pos, int hoop)
    {
        var c = RimCenter[hoop];
        float dx = pos.X - c.X, dz = pos.Z - c.Z;
        if (dx * HoopSign(hoop) > 0) return Mathf.Abs(dz) > ThreePt; // atrás da cesta (cantos retos)
        return Mathf.Sqrt(dx * dx + dz * dz) > ThreePt;
    }

    /// <summary>v0.37: Muralha de Aço do Iron de pé na linha de 3 desta cesta (atualizado pelo Match a cada tick).</summary>
    public readonly bool[] Wall = new bool[2];

    /// <summary>"Distância" do ponto até a cesta medida como a linha de 3 (= ThreePt em cima da linha; menor = dentro):
    /// raio até o aro na frente dela, |dz| atrás (cantos retos até a linha de fundo). grad = direção para fora.</summary>
    public float ThreeDist(Vector3 p, int hoop, out Vector3 grad)
    {
        var c = RimCenter[hoop];
        float dx = p.X - c.X, dz = p.Z - c.Z;
        if (dx * HoopSign(hoop) > 0) { grad = new Vector3(0, 0, dz >= 0 ? 1 : -1); return Mathf.Abs(dz); }
        float l = Mathf.Sqrt(dx * dx + dz * dz);
        grad = l > 1e-4f ? new Vector3(dx / l, 0, dz / l) : new Vector3(-HoopSign(hoop), 0, 0);
        return l;
    }

    public Vector3 ClampInside(Vector3 p, float margin = 0.4f) =>
        new(Mathf.Clamp(p.X, MinX + margin, MaxX - margin), p.Y, Mathf.Clamp(p.Z, MinZ + margin, MaxZ - margin));
}
