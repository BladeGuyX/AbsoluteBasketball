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
                    // "aro amigável": bola por dentro do aro perde a velocidade para fora e tende a cair
                    if (fl < t.RimRadius)
                    {
                        float vOut = v.X * dir.X + v.Z * dir.Z;
                        if (vOut > 0) { v.X -= dir.X * vOut * t.RimFriendliness; v.Z -= dir.Z * vOut * t.RimFriendliness; }
                        if (v.Y > 0) v.Y *= 1f - 0.5f * t.RimFriendliness;
                    }
                }
            }

            // tabela (AABB)
            if (CollideBox(ref p, ref v, g.Board[hoop], r, t.BoardBounce, hoop, ref hit, out var boardNormal))
            {
                hit.Board = true;
                // tabelinha: bateu na face da frente, dentro do quadrado → desvia para cair na cesta
                bool front = boardNormal.X * CourtGeo.HoopSign(hoop) < -0.5f;
                if (front && Mathf.Abs(p.Z - c.Z) <= g.BankHalfWidth && p.Y >= g.BankMinY && p.Y <= g.BankMaxY && t.BankAssist > 0)
                    BankAssist(ref v, p, c, t);
            }

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

    static void BankAssist(ref Vector3 v, Vector3 p, Vector3 rim, Tuning t)
    {
        float g = t.BallGravity;
        float vy = Mathf.Min(v.Y, 1.5f);
        float drop = p.Y - rim.Y;
        float disc = vy * vy + 2f * g * drop;
        if (disc < 0) return;
        float time = (vy + Mathf.Sqrt(disc)) / g;
        if (time < 0.05f) return;
        var want = new Vector3((rim.X - p.X) / time, vy, (rim.Z - p.Z) / time);
        v = v.Lerp(want, t.BankAssist);
    }

    static bool CollideBox(ref Vector3 p, ref Vector3 v, Aabb box, float r, float bounce, int hoop, ref BallHit hit, out Vector3 n)
    {
        var min = box.Position; var max = box.End;
        var q = new Vector3(Mathf.Clamp(p.X, min.X, max.X), Mathf.Clamp(p.Y, min.Y, max.Y), Mathf.Clamp(p.Z, min.Z, max.Z));
        var d = p - q;
        float dist = d.Length();
        n = Vector3.Zero;
        if (dist >= r) return false;
        n = dist > 1e-5f ? d / dist : new Vector3(-CourtGeo.HoopSign(hoop), 0, 0); // dentro: empurra para a quadra
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
        public float MaxRange;   // limite liberado pela carga
        public bool Capped;      // a câmera pede mais do que a carga libera
    }

    /// <summary>Alcance máximo liberado pela carga atual (a carga só muda o LIMITE, não a distância).</summary>
    public static float MaxRange(float charge)
    {
        var t = Tuning.I;
        return Mathf.Lerp(t.ShotRangeMin, t.ShotRangeMax, Mathf.Clamp(charge / Mathf.Max(t.ShotChargeTime, 0.01f), 0, 1));
    }

    /// <summary>Distância pedida pela inclinação da câmera: meio da tela = alcance inicial; acima = mais longe; abaixo = mais curto.</summary>
    public static float PitchDistance(float pitch)
    {
        var t = Tuning.I;
        float deg = Mathf.RadToDeg(pitch);
        if (deg >= t.ShotPitchBaseDeg)
        {
            float k = Mathf.Clamp((deg - t.ShotPitchBaseDeg) / Mathf.Max(t.ShotPitchTopDeg - t.ShotPitchBaseDeg, 0.1f), 0, 1);
            return Mathf.Lerp(t.ShotRangeMin, t.ShotRangeMax, k);
        }
        float kb = Mathf.Clamp((deg - t.ShotPitchBottomDeg) / Mathf.Max(t.ShotPitchBaseDeg - t.ShotPitchBottomDeg, 0.1f), 0, 1);
        return Mathf.Lerp(t.ShotRangeClosest, t.ShotRangeMin, kb);
    }

    /// <summary>Inverso de PitchDistance (usado por bots/piloto).</summary>
    public static float PitchForDistance(float d)
    {
        var t = Tuning.I;
        float deg;
        if (d >= t.ShotRangeMin)
            deg = Mathf.Lerp(t.ShotPitchBaseDeg, t.ShotPitchTopDeg, Mathf.Clamp((d - t.ShotRangeMin) / Mathf.Max(t.ShotRangeMax - t.ShotRangeMin, 0.01f), 0, 1));
        else
            deg = Mathf.Lerp(t.ShotPitchBottomDeg, t.ShotPitchBaseDeg, Mathf.Clamp((d - t.ShotRangeClosest) / Mathf.Max(t.ShotRangeMin - t.ShotRangeClosest, 0.01f), 0, 1));
        return Mathf.DegToRad(deg);
    }

    public static Vector3 ReleasePos(Vector3 feet, float yaw) => feet + Vector3.Up * Tuning.I.ShotReleaseHeight + Dir(yaw) * 0.25f;

    /// <summary>Assistência de direção em direção à cesta adversária.</summary>
    public static float AssistedYaw(Vector3 feet, float aimYaw, Vector3 rim)
    {
        var to = rim - feet; to.Y = 0;
        if (to.LengthSquared() < 0.01f) return aimYaw;
        float diff = WrapAngle(YawOf(to) - aimYaw);
        float max = Mathf.DegToRad(Tuning.I.ShotAimAssistMaxAngleDeg);
        if (max <= 0 || Mathf.Abs(diff) >= max) return aimYaw;
        float w = 1f - Mathf.Abs(diff) / max; // suave: forte perto do centro, zero na borda
        return aimYaw + diff * Tuning.I.ShotAimAssist * w;
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

        plan.MaxRange = MaxRange(charge);
        float wanted = PitchDistance(aimPitch);
        plan.Capped = wanted > plan.MaxRange;
        float d = Mathf.Min(wanted, plan.MaxRange);
        // assistência de distância (altura): só perto do ideal e só mirando a cesta adversária
        float yawErr = Mathf.Abs(WrapAngle(YawOf(new Vector3(rim.X - plan.Pos.X, 0, rim.Z - plan.Pos.Z)) - yaw));
        float delta = rimDist - d;
        if (yawErr < Mathf.DegToRad(t.ShotAimAssistMaxAngleDeg) && t.ShotDistAssistMeters > 0 && Mathf.Abs(delta) < t.ShotDistAssistMeters)
        {
            float w = 1f - Mathf.Abs(delta) / t.ShotDistAssistMeters;
            float nd = d + delta * t.ShotDistAssistStrength * w;
            if (nd <= plan.MaxRange) d = nd;
        }
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
