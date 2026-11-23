using Godot;

namespace AB;

public struct BallHit
{
    public bool Floor, Rim, Board, Wall, Pole;
    public int ScoredHoop; // -1 = nenhuma
    public float Impact;   // maior velocidade de impacto (para volume do som)
    public bool Any => Floor || Rim || Board || Wall || Pole;
}

/// <summary>
/// Física própria da bola (determinística, sem RigidBody) — usada pelo servidor E pelo
/// indicador de trajetória, garantindo que a prévia bate com o voo real.
/// </summary>
public static class BallPhys
{
    public const int Substeps = 4;

    public static BallHit Simulate(ref Vector3 p, ref Vector3 v, float dt, CourtGeo g)
    {
        var total = new BallHit { ScoredHoop = -1 };
        float h = dt / Substeps;
        for (int i = 0; i < Substeps; i++)
        {
            var r = Step(ref p, ref v, h, g);
            total.Floor |= r.Floor; total.Rim |= r.Rim; total.Board |= r.Board;
            total.Wall |= r.Wall; total.Pole |= r.Pole;
            total.Impact = Mathf.Max(total.Impact, r.Impact);
            if (r.ScoredHoop >= 0) total.ScoredHoop = r.ScoredHoop;
        }
        return total;
    }

    public static BallHit Step(ref Vector3 p, ref Vector3 v, float dt, CourtGeo g)
    {
        var t = Tuning.I;
        var hit = new BallHit { ScoredHoop = -1 };
        float r = t.BallRadius;
        var prev = p;

        v.Y -= t.BallGravity * dt;
        p += v * dt;

        // chão
        if (p.Y < r)
        {
            p.Y = r;
            if (v.Y < 0)
            {
                float imp = -v.Y;
                if (imp > 0.7f)
                {
                    v.Y = imp * t.FloorBounce;
                    v.X *= t.BounceFriction; v.Z *= t.BounceFriction;
                    hit.Floor = true; hit.Impact = Mathf.Max(hit.Impact, imp);
                }
                else v.Y = 0;
            }
        }
        // rolando
        if (p.Y <= r + 0.0005f && Mathf.Abs(v.Y) < 0.001f)
        {
            var hv = new Vector2(v.X, v.Z);
            float sp = hv.Length();
            if (sp > 0)
            {
                float ns = Mathf.Max(0, sp - t.RollDecel * dt);
                hv *= ns / sp;
                v.X = hv.X; v.Z = hv.Y;
            }
        }

        // paredes invisíveis
        if (p.X < g.MinX + r) { p.X = g.MinX + r; if (v.X < 0) { hit.Impact = Mathf.Max(hit.Impact, -v.X); v.X = -v.X * t.WallBounce; hit.Wall = true; } }
        if (p.X > g.MaxX - r) { p.X = g.MaxX - r; if (v.X > 0) { hit.Impact = Mathf.Max(hit.Impact, v.X); v.X = -v.X * t.WallBounce; hit.Wall = true; } }
        if (p.Z < g.MinZ + r) { p.Z = g.MinZ + r; if (v.Z < 0) { hit.Impact = Mathf.Max(hit.Impact, -v.Z); v.Z = -v.Z * t.WallBounce; hit.Wall = true; } }
        if (p.Z > g.MaxZ - r) { p.Z = g.MaxZ - r; if (v.Z > 0) { hit.Impact = Mathf.Max(hit.Impact, v.Z); v.Z = -v.Z * t.WallBounce; hit.Wall = true; } }

        for (int hoop = 0; hoop < 2; hoop++)
        {
            // aro (toro): ponto mais próximo no círculo
            var c = g.RimCenter[hoop];
            var rel = p - c;
            var flat = new Vector3(rel.X, 0, rel.Z);
            float fl = flat.Length();
            var dir = fl > 1e-4f ? flat / fl : Vector3.Right;
            var closest = c + dir * t.RimRadius;
            var d = p - closest;
            float dist = d.Length();
            const float tube = 0.035f;
            if (dist < r + tube && dist > 1e-5f)
            {
                var n = d / dist;
                p = closest + n * (r + tube);
                float vn = v.Dot(n);
                if (vn < 0)
                {
                    hit.Impact = Mathf.Max(hit.Impact, -vn);
                    v -= (1f + t.RimBounce) * vn * n;
                    v *= 0.97f;
                    hit.Rim = true;
                }
            }

            // tabela (AABB)
            if (CollideBox(ref p, ref v, g.Board[hoop], r, t.BoardBounce, hoop, ref hit)) hit.Board = true;

            // poste (cilindro vertical)
            var pb = g.PoleBase[hoop];
            if (p.Y < g.PoleHeight)
            {
                var pd = new Vector3(p.X - pb.X, 0, p.Z - pb.Z);
                float pl = pd.Length();
                if (pl < r + g.PoleRadius && pl > 1e-5f)
                {
                    var n = pd / pl;
                    p += n * (r + g.PoleRadius - pl);
                    float vn = v.Dot(n);
                    if (vn < 0) { v -= (1f + t.WallBounce) * vn * n; hit.Pole = true; hit.Impact = Mathf.Max(hit.Impact, -vn); }
                }
            }

            // cesta: cruzou o plano do aro descendo, dentro do aro
            if (prev.Y > c.Y && p.Y <= c.Y && v.Y < 0)
            {
                float k = (prev.Y - c.Y) / Mathf.Max(prev.Y - p.Y, 1e-6f);
                var cross = prev.Lerp(p, k);
                float hd = new Vector2(cross.X - c.X, cross.Z - c.Z).Length();
                if (hd < t.RimRadius - r * (1f - t.ScoreTolerance)) hit.ScoredHoop = hoop;
            }
        }
        return hit;
    }

    static bool CollideBox(ref Vector3 p, ref Vector3 v, Aabb box, float r, float bounce, int hoop, ref BallHit hit)
    {
        var min = box.Position; var max = box.End;
        var q = new Vector3(Mathf.Clamp(p.X, min.X, max.X), Mathf.Clamp(p.Y, min.Y, max.Y), Mathf.Clamp(p.Z, min.Z, max.Z));
        var d = p - q;
        float dist = d.Length();
        if (dist >= r) return false;
        Vector3 n = dist > 1e-5f ? d / dist : new Vector3(-CourtGeo.HoopSign(hoop), 0, 0); // dentro: empurra para a quadra
        p = q + n * r;
        float vn = v.Dot(n);
        if (vn < 0)
        {
            hit.Impact = Mathf.Max(hit.Impact, -vn);
            v -= (1f + bounce) * vn * n;
        }
        return true;
    }
}

/// <summary>Matemática do arremesso — compartilhada entre jogador, prévia de trajetória e bots.</summary>
public static class ShotMath
{
    public static Vector3 Dir(float yaw) => new(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw));
    public static float YawOf(Vector3 dir) => Mathf.Atan2(-dir.X, -dir.Z);

    public static float WrapAngle(float a)
    {
        a = Mathf.PosMod(a + Mathf.Pi, Mathf.Tau);
        return a - Mathf.Pi;
    }

    public struct ShotPlan
    {
        public Vector3 Pos, Vel;
        public float Distance;   // distância horizontal até cruzar a altura do aro
        public bool Close;       // bandeja (dentro da área de perto da cesta adversária)
    }

    /// <summary>Alcance máximo liberado pela carga atual.</summary>
    public static float MaxRange(float charge)
    {
        var t = Tuning.I;
        return Mathf.Lerp(t.ShotRangeMin, t.ShotRangeMax, Mathf.Clamp(charge / Mathf.Max(t.ShotChargeTime, 0.01f), 0, 1));
    }

    /// <summary>0..1: quanto do alcance carregado a inclinação da câmera libera.</summary>
    public static float PitchFactor(float pitch)
    {
        var t = Tuning.I;
        float lo = Mathf.DegToRad(t.ShotPitchMinDeg), hi = Mathf.DegToRad(t.ShotPitchFullDeg);
        float k = hi > lo ? Mathf.Clamp((pitch - lo) / (hi - lo), 0, 1) : 1f;
        return Mathf.Lerp(t.ShotPitchMinRangeFactor, 1f, k);
    }

    public static Vector3 ReleasePos(Vector3 feet, float yaw) => feet + Vector3.Up * Tuning.I.ShotReleaseHeight + Dir(yaw) * 0.25f;

    /// <summary>Assistência de direção em direção à cesta adversária.</summary>
    public static float AssistedYaw(Vector3 feet, float aimYaw, Vector3 rim)
    {
        var to = rim - feet; to.Y = 0;
        if (to.LengthSquared() < 0.01f) return aimYaw;
        float diff = WrapAngle(YawOf(to) - aimYaw);
        if (Mathf.Abs(diff) > Mathf.DegToRad(Tuning.I.ShotAimAssistMaxAngleDeg)) return aimYaw;
        return aimYaw + diff * Tuning.I.ShotAimAssist;
    }

    /// <summary>Velocidade para a bola cruzar a altura dh (descendo) a uma distância horizontal d.</summary>
    public static Vector3 Ballistic(float yaw, float d, float dh, float angle)
    {
        d = Mathf.Max(d, 0.05f);
        // ângulo mínimo para a bola DESCER no alvo com inclinação >= ShotMinEntryAngleDeg (tanβ = tanθ − 2·dh/d)
        float minAngle = Mathf.Atan(Mathf.Tan(Mathf.DegToRad(Tuning.I.ShotMinEntryAngleDeg)) + 2f * dh / d);
        float th = Mathf.Clamp(Mathf.Max(angle, minAngle), 0.1f, Mathf.DegToRad(80f));
        float c = Mathf.Cos(th);
        float denom = 2f * c * c * (d * Mathf.Tan(th) - dh);
        float v = denom > 0.0001f ? Mathf.Sqrt(Tuning.I.BallGravity * d * d / denom) : 1f;
        return Dir(yaw) * (v * c) + Vector3.Up * (v * Mathf.Sin(th));
    }

    /// <summary>Planeja o arremesso de um jogador (usado no arremesso real, na prévia e pelos bots).</summary>
    public static ShotPlan Plan(Vector3 feet, int team, float charge, float aimYaw, float aimPitch, CourtGeo g)
    {
        var t = Tuning.I;
        var rim = g.RimCenter[CourtGeo.AttackHoop(team)];
        var toRim = rim - feet; toRim.Y = 0;
        var plan = new ShotPlan { Close = toRim.Length() <= t.CloseShotRadius };

        float yaw = plan.Close ? YawOf(toRim) : AssistedYaw(feet, aimYaw, rim);
        plan.Pos = ReleasePos(feet, yaw);
        float dh = rim.Y - plan.Pos.Y;
        float rimDist = new Vector2(rim.X - plan.Pos.X, rim.Z - plan.Pos.Z).Length();

        if (plan.Close)
        {
            plan.Distance = rimDist;
            plan.Vel = Ballistic(yaw, rimDist, dh, Mathf.DegToRad(t.CloseShotAngleDeg));
            return plan;
        }

        float d = MaxRange(charge) * PitchFactor(aimPitch);
        float yawErr = Mathf.Abs(WrapAngle(YawOf(new Vector3(rim.X - plan.Pos.X, 0, rim.Z - plan.Pos.Z)) - yaw));
        if (yawErr < Mathf.DegToRad(t.ShotAimAssistMaxAngleDeg) && Mathf.Abs(d - rimDist) <= t.ShotDistAssistMeters)
            d = Mathf.Lerp(d, rimDist, t.ShotDistAssistStrength);
        plan.Distance = d;
        float k = Mathf.Clamp(d / Mathf.Max(t.ShotRangeMax, 0.1f), 0, 1);
        plan.Vel = Ballistic(yaw, d, dh, Mathf.DegToRad(Mathf.Lerp(t.ShotAngleNearDeg, t.ShotAngleFarDeg, k)));
        return plan;
    }

    /// <summary>Carga necessária para alcançar a distância d com a câmera no alcance total. NaN se impossível.</summary>
    public static float ChargeForDistance(float d)
    {
        var t = Tuning.I;
        if (d > t.ShotRangeMax + 0.01f) return float.NaN;
        float k = Mathf.Clamp((d - t.ShotRangeMin) / Mathf.Max(t.ShotRangeMax - t.ShotRangeMin, 0.01f), 0, 1);
        return k * t.ShotChargeTime;
    }
}
