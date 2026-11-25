using Godot;

namespace AB;

public struct BallHit
{
    public bool Floor, Rim, Board, Wall, Pole;
    public int ScoredHoop; // -1 = nenhuma
    public int UpThroughHoop; // -1 = nenhuma; passou SUBINDO por dentro do aro (cesta por baixo não vale)
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

    /// <param name="assistHoop">Cesta onde valem o aro amigável e a tabelinha (só arremessos na cesta adversária); −1 = física pura.</param>
    public static BallHit Simulate(ref Vector3 p, ref Vector3 v, float dt, CourtGeo g, int assistHoop = -1)
    {
        var total = new BallHit { ScoredHoop = -1, UpThroughHoop = -1 };
        float h = dt / Substeps;
        for (int i = 0; i < Substeps; i++)
        {
            var r = Step(ref p, ref v, h, g, assistHoop);
            total.Floor |= r.Floor; total.Rim |= r.Rim; total.Board |= r.Board;
            total.Wall |= r.Wall; total.Pole |= r.Pole;
            total.Impact = Mathf.Max(total.Impact, r.Impact);
            if (r.ScoredHoop >= 0) total.ScoredHoop = r.ScoredHoop;
            if (r.UpThroughHoop >= 0) total.UpThroughHoop = r.UpThroughHoop;
        }
        return total;
    }

    public static BallHit Step(ref Vector3 p, ref Vector3 v, float dt, CourtGeo g, int assistHoop = -1)
    {
        var t = Tuning.I;
        var hit = new BallHit { ScoredHoop = -1, UpThroughHoop = -1 };
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
                    if (fl < t.RimRadius && hoop == assistHoop)
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
                if (front && Mathf.Abs(p.Z - c.Z) <= g.BankHalfWidth && p.Y >= g.BankMinY && p.Y <= g.BankMaxY && t.BankAssist > 0 && hoop == assistHoop)
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
            // passou subindo pelo aro (por baixo): a descida seguinte não vale
            if (prev.Y < c.Y && p.Y >= c.Y && v.Y > 0)
            {
                float k = (c.Y - prev.Y) / Mathf.Max(p.Y - prev.Y, 1e-6f);
                var cross = prev.Lerp(p, k);
                if (new Vector2(cross.X - c.X, cross.Z - c.Z).Length() < t.RimRadius + r) hit.UpThroughHoop = hoop;
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
        public float Range;      // alcance máximo deste arremesso (v0.26: Precisão / Olho do Rei)
        public float Power => Mathf.Clamp(Distance / Mathf.Max(Range > 0 ? Range : Tuning.I.ShotRangeMax, 0.1f), 0, 1); // "força" = distância usada
    }

    static float R(float rangeMax) => rangeMax > 0 ? rangeMax : Tuning.I.ShotRangeMax;

    /// <summary>
    /// Distância pedida pela inclinação da câmera: meio da tela = ShotRangeMin; acima = mais longe até <paramref name="rangeMax"/>
    /// (Precisão do personagem); abaixo = mais curto. v0.26: com <paramref name="extendMax"/> (Olho do Rei), subir a câmera ALÉM
    /// da inclinação do alcance máximo vai até esse alcance maior — a mira normal não muda.
    /// </summary>
    public static float PitchDistance(float pitch, float rangeMax = -1, float extendMax = -1)
    {
        var t = Tuning.I;
        float max = R(rangeMax);
        float deg = Mathf.RadToDeg(pitch);
        if (deg >= t.ShotPitchBaseDeg)
        {
            if (extendMax > max && deg > t.ShotPitchTopDeg)
            {
                float ke = Mathf.Clamp((deg - t.ShotPitchTopDeg) / Mathf.Max(t.CameraPitchMaxDeg - t.ShotPitchTopDeg, 0.1f), 0, 1);
                return Mathf.Lerp(max, extendMax, ke);
            }
            float k = Mathf.Clamp((deg - t.ShotPitchBaseDeg) / Mathf.Max(t.ShotPitchTopDeg - t.ShotPitchBaseDeg, 0.1f), 0, 1);
            return Mathf.Lerp(t.ShotRangeMin, max, k);
        }
        float kb = Mathf.Clamp((deg - t.ShotPitchBottomDeg) / Mathf.Max(t.ShotPitchBaseDeg - t.ShotPitchBottomDeg, 0.1f), 0, 1);
        return Mathf.Lerp(t.ShotRangeClosest, t.ShotRangeMin, kb);
    }

    /// <summary>Inverso de PitchDistance (usado por bots/piloto).</summary>
    public static float PitchForDistance(float d, float rangeMax = -1, float extendMax = -1)
    {
        var t = Tuning.I;
        float max = R(rangeMax);
        float deg;
        if (extendMax > max && d > max)
            deg = Mathf.Lerp(t.ShotPitchTopDeg, t.CameraPitchMaxDeg, Mathf.Clamp((d - max) / Mathf.Max(extendMax - max, 0.01f), 0, 1));
        else if (d >= t.ShotRangeMin)
            deg = Mathf.Lerp(t.ShotPitchBaseDeg, t.ShotPitchTopDeg, Mathf.Clamp((d - t.ShotRangeMin) / Mathf.Max(max - t.ShotRangeMin, 0.01f), 0, 1));
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
    public static ShotPlan Plan(Vector3 feet, int team, float aimYaw, float aimPitch, CourtGeo g, float rangeMax = -1, float extendMax = -1)
    {
        var t = Tuning.I;
        float max = Mathf.Max(R(rangeMax), extendMax);
        var rim = g.RimCenter[CourtGeo.AttackHoop(team)];
        var plan = new ShotPlan { Range = max };

        float yaw = AssistedYaw(feet, aimYaw, rim);
        plan.Pos = ReleasePos(feet, yaw);
        float dh = rim.Y - plan.Pos.Y;
        float rimDist = new Vector2(rim.X - plan.Pos.X, rim.Z - plan.Pos.Z).Length();

        float d = Mathf.Min(PitchDistance(aimPitch, rangeMax, extendMax), max);
        // assistência de distância (altura): só perto do ideal e só mirando a cesta adversária
        float yawErr = Mathf.Abs(WrapAngle(YawOf(new Vector3(rim.X - plan.Pos.X, 0, rim.Z - plan.Pos.Z)) - yaw));
        float delta = rimDist - d;
        if (yawErr < Mathf.DegToRad(t.ShotAimAssistMaxAngleDeg) && t.ShotDistAssistMeters > 0 && Mathf.Abs(delta) < t.ShotDistAssistMeters)
        {
            float w = 1f - Mathf.Abs(delta) / t.ShotDistAssistMeters;
            float nd = d + delta * t.ShotDistAssistStrength * w;
            if (nd <= max) d = nd;
        }
        plan.Distance = d;
        // o ângulo segue a distância absoluta (mesma curva para todos os personagens)
        float k = Mathf.Clamp(d / Mathf.Max(t.ShotRangeMax, 0.1f), 0, 1);
        plan.Vel = Ballistic(yaw, d, dh, Mathf.DegToRad(Mathf.Lerp(t.ShotAngleNearDeg, t.ShotAngleFarDeg, k)));
        return plan;
    }

    /// <summary>
    /// v0.26: arremesso "perfeito" direto no aro (Arremesso Arriscado do Luck). <paramref name="missOffset"/> &gt; 0 = mira esse
    /// tanto para o lado do aro (bate e sai).
    /// </summary>
    public static ShotPlan Direct(Vector3 feet, int team, CourtGeo g, float missOffset, float side)
    {
        var t = Tuning.I;
        var rim = g.RimCenter[CourtGeo.AttackHoop(team)];
        var flat = (rim - feet) with { Y = 0 };
        float yaw = YawOf(flat.LengthSquared() > 1e-4f ? flat : Vector3.Forward);
        var plan = new ShotPlan { Range = t.ShotRangeMax };
        plan.Pos = ReleasePos(feet, yaw);
        var target = rim;
        if (missOffset > 0)
        {
            var dir = ((rim - plan.Pos) with { Y = 0 }).Normalized();
            target += new Vector3(-dir.Z, 0, dir.X) * (side * missOffset) - dir * (t.RimRadius * 0.4f);
        }
        var to = (target - plan.Pos) with { Y = 0 };
        float d = to.Length();
        plan.Distance = d;
        float k = Mathf.Clamp(d / Mathf.Max(t.ShotRangeMax, 0.1f), 0, 1);
        plan.Vel = Ballistic(YawOf(to), d, target.Y - plan.Pos.Y, Mathf.DegToRad(Mathf.Lerp(t.ShotAngleNearDeg, t.ShotAngleFarDeg, k)));
        return plan;
    }

    /// <summary>v0.26: desvia um arremesso já planejado para bater no aro e sair (Presença Implacável).</summary>
    public static ShotPlan Jinx(ShotPlan plan, int team, CourtGeo g, float offset, float side)
    {
        var t = Tuning.I;
        var rim = g.RimCenter[CourtGeo.AttackHoop(team)];
        var dir = ((rim - plan.Pos) with { Y = 0 });
        if (dir.LengthSquared() < 1e-4f) return plan;
        dir = dir.Normalized();
        var target = rim + new Vector3(-dir.Z, 0, dir.X) * (side * offset) - dir * (t.RimRadius * 0.3f);
        var to = (target - plan.Pos) with { Y = 0 };
        float d = to.Length();
        float k = Mathf.Clamp(d / Mathf.Max(t.ShotRangeMax, 0.1f), 0, 1);
        plan.Vel = Ballistic(YawOf(to), d, target.Y - plan.Pos.Y, Mathf.DegToRad(Mathf.Lerp(t.ShotAngleNearDeg, t.ShotAngleFarDeg, k)));
        plan.Distance = d;
        return plan;
    }
}
