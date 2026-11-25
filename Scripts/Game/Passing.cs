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
    public static Vector3 Velocity(Player from, Player to, bool high, out Vector3 releasePos) =>
        Velocity(from, to, high, out releasePos, out _);

    /// <summary>Idem, devolvendo também o tempo de voo até o ponto de recepção previsto.</summary>
    public static Vector3 Velocity(Player from, Player to, bool high, out Vector3 releasePos, out float flightTime)
    {
        var t = Tuning.I;
        float g = t.BallGravity;
        var target = to.GlobalPosition + Vector3.Up * t.PassReceiveHeight;
        var toVel = new Vector3(to.Velocity.X, 0, to.Velocity.Z);
        releasePos = ReleasePos(from, target);
        Vector3 vel = Vector3.Zero;
        flightTime = 0;
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
            flightTime = time;
            target = to.GlobalPosition + toVel * time + Vector3.Up * t.PassReceiveHeight;
            releasePos = ReleasePos(from, target);
        }
        return vel;
    }

    /// <summary>
    /// O passe de 'from' para 'to' chega nele? Simula a trajetória exatamente como a recepção de verdade
    /// (<see cref="Ball.CanBeCaughtBy"/> + <c>Match.TryPickup</c>): o recebedor anda até o ponto previsto, aliados no caminho
    /// não atrapalham (pegariam a bola), adversário a menos de <c>PassLaneRadius</c> da bola e mais perto dela que o
    /// recebedor bloqueia, assim como tabela/poste. (v0.13: antes só aceitava se a bola passasse perto da posição ATUAL
    /// do recebedor — com ele andando a simulação nunca "chegava" e o passe rápido falhava mesmo de perto.)
    /// </summary>
    public static bool LaneClear(Player from, Player to, bool high)
    {
        var t = Tuning.I;
        var M = from.M;
        var v0 = Velocity(from, to, high, out var p0, out float flight);
        var toPos = to.GlobalPosition;
        var toVel = new Vector3(to.Velocity.X, 0, to.Velocity.Z);
        float g = t.BallGravity;
        float catchR = t.PickupRadius + t.BallRadius;
        const float dt = 1f / 60f;
        int steps = Mathf.CeilToInt((flight + 0.25f) / dt);
        for (int i = 1; i <= steps; i++)
        {
            float time = i * dt;
            var pos = p0 + v0 * time + Vector3.Down * (0.5f * g * time * time);
            float vy = v0.Y - g * time;
            if (pos.Y < 0) return false;
            for (int h = 0; h < 2; h++)
            {
                if (M.Geo.Board[h].Grow(t.BallRadius).HasPoint(pos)) return false;
                var pb = M.Geo.PoleBase[h];
                if (pos.Y < M.Geo.PoleHeight && new Vector2(pos.X - pb.X, pos.Z - pb.Z).Length() < 0.6f) return false;
            }
            bool rising = high && vy > 0; // passe alto subindo: ninguém pega, só o toco no ar

            // recebedor (andando até o ponto previsto)
            var rp = toPos + toVel * Mathf.Min(time, flight);
            var rr = pos - rp;
            float rd = new Vector2(rr.X, rr.Z).Length();
            bool recvCan = !rising && rd <= catchR && rr.Y >= -0.3f && rr.Y <= t.CatchHeight;

            foreach (var q in M.Players)
            {
                if (q == from || q == to || q.Team == from.Team) continue;
                var rel = pos - q.GlobalPosition;
                float hd = new Vector2(rel.X, rel.Z).Length();
                if (q.Airborne && hd < t.BlockRadius + t.BallRadius && rel.Y > -0.2f && rel.Y < t.BlockReach) return false;
                if (rising) continue;
                if (hd < t.PassLaneRadius && rel.Y > -0.3f && rel.Y < t.CatchHeight && (!recvCan || hd < rd)) return false;
            }
            if (recvCan) return true;
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

    /// <summary>Passe carregado: bola lançada na direção da câmera (yaw + inclinação + elevação), sem alvo.</summary>
    public static Vector3 AimedVelocity(float aimYaw, float aimPitch, bool high)
    {
        var t = Tuning.I;
        float lift = high ? t.AimHighPassLiftDeg : t.AimPassLiftDeg;
        float elev = Mathf.Clamp(aimPitch + Mathf.DegToRad(lift), Mathf.DegToRad(-30f), Mathf.DegToRad(80f));
        float speed = high ? t.AimHighPassSpeed : t.AimPassSpeed;
        return ShotMath.Dir(aimYaw) * (speed * Mathf.Cos(elev)) + Vector3.Up * (speed * Mathf.Sin(elev));
    }
}
