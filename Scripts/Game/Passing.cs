using Godot;

namespace AB;

/// <summary>Regras de passe: alvos válidos, corredor livre e trajetória (normal e alta).</summary>
public static class Passing
{
    public const float ReleaseHeight = 1.35f;

    public static Vector3 ReleasePos(Player from, Vector3 toward)
    {
        var d = toward - from.GlobalPosition; d.Y = 0;
        var dir = d.LengthSquared() > 0.01f ? d.Normalized() : from.Forward;
        return from.GlobalPosition + Vector3.Up * ReleaseHeight + dir * 0.45f;
    }

    /// <summary>Calcula a velocidade do passe (com antecipação do movimento do recebedor).</summary>
    public static Vector3 Velocity(Player from, Player to, bool high, out Vector3 releasePos)
    {
        var t = Tuning.I;
        float g = t.BallGravity;
        var target = to.GlobalPosition + Vector3.Up * t.PassReceiveHeight;
        var toVel = new Vector3(to.Velocity.X, 0, to.Velocity.Z);
        releasePos = ReleasePos(from, target);
        Vector3 vel = Vector3.Zero;
        for (int iter = 0; iter < 2; iter++) // 2 iterações de antecipação bastam
        {
            var flat = new Vector3(target.X - releasePos.X, 0, target.Z - releasePos.Z);
            float d = Mathf.Max(flat.Length(), 0.1f);
            float dh = target.Y - releasePos.Y;
            float time;
            float vy;
            if (!high)
            {
                time = d / t.PassSpeed;
                vy = dh / time + g * time / 2f;
            }
            else
            {
                float apex = Mathf.Max(t.HighPassApex, Mathf.Max(releasePos.Y, target.Y) + 0.5f);
                vy = Mathf.Sqrt(2f * g * (apex - releasePos.Y));
                float disc = vy * vy - 2f * g * dh;
                time = (vy + Mathf.Sqrt(Mathf.Max(disc, 0))) / g;
            }
            vel = flat / d * (d / time) + Vector3.Up * vy;
            target = to.GlobalPosition + toVel * time + Vector3.Up * t.PassReceiveHeight;
            releasePos = ReleasePos(from, target);
        }
        return vel;
    }

    /// <summary>O passe de 'from' para 'to' tem o caminho livre?</summary>
    public static bool LaneClear(Player from, Player to, bool high)
    {
        var t = Tuning.I;
        var M = from.M;
        var vel = Velocity(from, to, high, out var pos);
        // simula a trajetória (sem colisões da bola) e testa jogadores e estruturas
        const float dt = 1f / 30f;
        var target = to.GlobalPosition;
        for (int i = 0; i < 120; i++)
        {
            vel.Y -= t.BallGravity * dt;
            pos += vel * dt;
            var flatToTarget = new Vector2(target.X - pos.X, target.Z - pos.Z).Length();
            if (flatToTarget < 0.6f) return true;
            if (pos.Y < 0) return false;
            foreach (var q in M.Players)
            {
                if (q == from || q == to) continue;
                var rel = pos - q.GlobalPosition;
                float hd = new Vector2(rel.X, rel.Z).Length();
                float top = q.Airborne ? t.BlockReach : 2.0f;
                if (hd < (high ? t.BlockRadius + t.BallRadius : t.PassLaneRadius) && rel.Y > -0.2f && rel.Y < top) return false;
            }
            for (int h = 0; h < 2; h++)
            {
                var b = M.Geo.Board[h].Grow(t.BallRadius);
                if (b.HasPoint(pos)) return false;
                var pb = M.Geo.PoleBase[h];
                if (pos.Y < M.Geo.PoleHeight && new Vector2(pos.X - pb.X, pos.Z - pb.Z).Length() < 0.6f) return false;
            }
        }
        return false;
    }

    static bool InRange(Player from, Player to) =>
        new Vector2(to.GlobalPosition.X - from.GlobalPosition.X, to.GlobalPosition.Z - from.GlobalPosition.Z).Length() <= Tuning.I.PassRange;

    /// <summary>Toque rápido: aliado livre mais próximo.</summary>
    public static Player Nearest(Player from, bool high)
    {
        Player best = null;
        float bestD = float.MaxValue;
        foreach (var q in from.M.Players)
        {
            if (q == from || q.Team != from.Team || !q.CanCatch || !InRange(from, q)) continue;
            float d = from.GlobalPosition.DistanceSquaredTo(q.GlobalPosition);
            if (d < bestD && LaneClear(from, q, high)) { bestD = d; best = q; }
        }
        return best;
    }

    /// <summary>Segurando: aliado mais na direção da câmera.</summary>
    public static Player Aimed(Player from, float aimYaw, bool high)
    {
        Player best = null;
        float bestA = Mathf.DegToRad(Tuning.I.PassAimMaxAngleDeg);
        foreach (var q in from.M.Players)
        {
            if (q == from || q.Team != from.Team || !q.CanCatch || !InRange(from, q)) continue;
            var to = q.GlobalPosition - from.GlobalPosition; to.Y = 0;
            float a = Mathf.Abs(ShotMath.WrapAngle(ShotMath.YawOf(to) - aimYaw));
            if (a < bestA && LaneClear(from, q, high)) { bestA = a; best = q; }
        }
        return best;
    }
}
