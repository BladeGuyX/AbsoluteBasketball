using Godot;

namespace AB;

/// <summary>Estado que o rig precisa para animar (calculado pelo Player a cada tick).</summary>
public struct RigState
{
    public float Yaw;
    public float Speed;        // velocidade horizontal (m/s)
    public bool HasBall;
    public bool Charging;
    public float Charge;       // segundos carregando
    public float Release;      // 1 → 0 durante o "follow-through" do arremesso
    public Vector3 BallPos;    // posição da bola quando está na mão
    public bool PassCharging;
    public float PassRelease;  // 1 → 0 depois de passar
    public bool JumpCharging;
    public bool Airborne;
    public bool Dashing;       // bote do roubo
    public bool Slowed;        // penalidade do roubo
    public bool Landing;       // travado após pousar
    public float BallSide;     // +1 direita, −1 esquerda
    public bool Dribbling;     // drible especial (agachado, quique rápido)
    public bool Dunking;       // voo da enterrada/tapinha (braços esticados para cima)
    public bool Stunned;       // atordoado depois de errar a enterrada
}

/// <summary>
/// Boneco placeholder com animação procedural (passada, balanço, braços com IK simples até a bola,
/// agachamento na carga, pulinho no arremesso). Será trocado pelo modelo animado do Blender (Etapa 7).
/// </summary>
public sealed class PlayerRig
{
    public readonly Node3D Root;
    readonly Node3D _upper, _legL, _legR, _armL, _armR;
    float _stride;
    float _crouch;
    float _lean;

    const float StrideLen = 0.8f;      // metros por meio ciclo
    const float ArmLen = 0.66f;

    public PlayerRig(Node3D parent, int team)
    {
        Root = new Node3D { Name = "Visual" };
        parent.AddChild(Root);
        var jersey = Mat.ToonOutlined(Palette.Team[team]);
        var dark = Mat.ToonOutlined(Palette.TeamDark[team]);
        var skin = Mat.ToonOutlined(Palette.Skin);
        var shoe = Mat.ToonOutlined(new Color("f5f5f5"));
        var ink = Mat.Unshaded(Palette.Ink);

        _legL = Leg(-0.13f, skin, shoe);
        _legR = Leg(0.13f, skin, shoe);

        _upper = new Node3D { Position = new Vector3(0, 0.6f, 0) };
        Root.AddChild(_upper);
        _upper.AddChild(Mat.MeshNode(new CylinderMesh { TopRadius = 0.29f, BottomRadius = 0.31f, Height = 0.3f }, dark, new Vector3(0, 0.06f, 0)));
        _upper.AddChild(Mat.MeshNode(new CapsuleMesh { Radius = 0.31f, Height = 0.9f }, jersey, new Vector3(0, 0.4f, 0)));
        _upper.AddChild(Mat.MeshNode(new SphereMesh { Radius = 0.27f, Height = 0.54f }, skin, new Vector3(0, 1.04f, 0)));
        _upper.AddChild(Mat.MeshNode(new TorusMesh { InnerRadius = 0.245f, OuterRadius = 0.29f }, dark, new Vector3(0, 1.14f, 0)));
        foreach (float x in new[] { -0.095f, 0.095f })
            _upper.AddChild(Mat.MeshNode(new SphereMesh { Radius = 0.042f, Height = 0.084f }, ink, new Vector3(x, 1.08f, -0.245f), false));

        _armL = Arm(-0.39f, skin);
        _armR = Arm(0.39f, skin);
    }

    Node3D Leg(float x, Material skin, Material shoe)
    {
        var pivot = new Node3D { Position = new Vector3(x, 0.6f, 0) };
        pivot.AddChild(Mat.MeshNode(new CapsuleMesh { Radius = 0.11f, Height = 0.62f }, skin, new Vector3(0, -0.29f, 0)));
        pivot.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(0.2f, 0.12f, 0.32f) }, shoe, new Vector3(0, -0.54f, -0.05f)));
        Root.AddChild(pivot);
        return pivot;
    }

    Node3D Arm(float x, Material skin)
    {
        var pivot = new Node3D { Position = new Vector3(x, 0.72f, 0) };
        pivot.AddChild(Mat.MeshNode(new CapsuleMesh { Radius = 0.085f, Height = 0.56f }, skin, new Vector3(0, -0.26f, 0)));
        pivot.AddChild(Mat.MeshNode(new SphereMesh { Radius = 0.1f, Height = 0.2f }, skin, new Vector3(0, -ArmLen + 0.08f, 0)));
        _upper.AddChild(pivot);
        return pivot;
    }

    public void Animate(in RigState s, float dt)
    {
        var t = Tuning.I;
        float k = 1f - Mathf.Exp(-14f * dt);
        float move = Mathf.Clamp(s.Speed / Mathf.Max(t.RunSpeed, 0.1f), 0, 1);
        float walking = Mathf.Clamp(s.Speed / 0.6f, 0, 1);
        _stride += s.Speed * dt / StrideLen * Mathf.Pi;
        float swing = Mathf.Sin(_stride) * (0.3f + 0.5f * move) * walking;

        // agacha carregando arremesso/salto e ao pousar; "pulinho" e esticada ao soltar
        float crouchTarget = 0f;
        if (s.Charging) crouchTarget = Mathf.Clamp(s.Charge / 0.25f, 0, 1);
        if (s.JumpCharging) crouchTarget = 1.3f;
        if (s.Landing) crouchTarget = 0.8f;
        if (s.Dribbling) crouchTarget = t.DribbleCrouch;
        if (s.Stunned && !s.Airborne) crouchTarget = 1.1f;
        _crouch = Mathf.Lerp(_crouch, crouchTarget, k);
        float rel = Mathf.Clamp(s.Release, 0, 1);
        float relWave = rel > 0 ? Mathf.Sin(Mathf.Pi * (1f - rel)) : 0f;
        float hop = relWave * 0.35f;
        float bob = Mathf.Abs(Mathf.Sin(_stride)) * 0.07f * walking - _crouch * 0.12f;

        Root.Rotation = new Vector3(0, s.Yaw, 0);
        Root.Position = new Vector3(0, bob + hop, 0);
        float stretch = relWave + (s.Airborne ? 0.6f : 0f);
        float sy = 1f - 0.09f * _crouch + 0.1f * stretch;
        float sxz = 1f + 0.05f * _crouch - 0.04f * stretch;
        Root.Scale = new Vector3(sxz, sy, sxz);

        float leanTarget = -0.2f * move + 0.12f * _crouch - 0.08f * relWave;
        if (s.Dribbling) leanTarget -= 0.3f; // tronco para frente (negativo = frente), protegendo a bola
        if (s.Dashing) leanTarget = -0.55f;
        if (s.Slowed) leanTarget = 0.25f;
        if (s.Stunned) leanTarget = 0.35f + Mathf.Sin(_stride + Time.GetTicksMsec() / 260f) * 0.08f; // cambaleando
        _lean = Mathf.Lerp(_lean, leanTarget, k);
        _upper.Rotation = new Vector3(_lean, Mathf.Sin(_stride) * 0.08f * walking, 0);

        float legBend = _crouch * (s.Dribbling ? 0.45f : 0.25f);
        if (s.Airborne)
        {
            _legL.Rotation = new Vector3(Mathf.Lerp(_legL.Rotation.X, 0.35f, k), 0, -0.08f);
            _legR.Rotation = new Vector3(Mathf.Lerp(_legR.Rotation.X, -0.15f, k), 0, 0.08f);
        }
        else if (s.Dashing)
        {
            _legL.Rotation = new Vector3(0.7f, 0, 0);
            _legR.Rotation = new Vector3(-0.6f, 0, 0);
        }
        else
        {
            _legL.Rotation = new Vector3(swing + legBend, 0, 0);
            _legR.Rotation = new Vector3(-swing + legBend, 0, 0);
        }

        float r = t.BallRadius;
        var right = new Vector3(Mathf.Cos(s.Yaw), 0, -Mathf.Sin(s.Yaw));
        if (s.Dunking && !s.HasBall)
        {
            _armL.Rotation = new Vector3(Mathf.Lerp(_armL.Rotation.X, 3.05f, k), 0, -0.12f);
            _armR.Rotation = new Vector3(Mathf.Lerp(_armR.Rotation.X, 3.05f, k), 0, 0.12f);
        }
        else if (s.HasBall && (s.Charging || s.PassCharging || s.Dunking))
        {
            PointArm(_armL, s.BallPos - right * (r + 0.03f), right);
            PointArm(_armR, s.BallPos + right * (r + 0.03f), right);
        }
        else if (s.HasBall)
        {
            // a mão do lado da bola acompanha o topo dela; a outra protege
            var top = s.BallPos + Vector3.Up * r;
            var ballArm = s.BallSide >= 0 ? _armR : _armL;
            var otherArm = s.BallSide >= 0 ? _armL : _armR;
            float side = s.BallSide >= 0 ? -1f : 1f;
            top.Y = Mathf.Max(top.Y, ballArm.GlobalPosition.Y - ArmLen);
            PointArm(ballArm, top, right);
            otherArm.Rotation = new Vector3(Mathf.Lerp(otherArm.Rotation.X, 0.7f - swing * 0.3f, k), 0, side * 0.35f);
        }
        else if (rel > 0)
        {
            float up = 2.85f * Mathf.Clamp(rel * 1.6f, 0, 1);
            _armL.Rotation = new Vector3(Mathf.Lerp(_armL.Rotation.X, up, 0.5f), 0, -0.08f);
            _armR.Rotation = new Vector3(Mathf.Lerp(_armR.Rotation.X, up, 0.5f), 0, 0.08f);
        }
        else if (s.PassRelease > 0)
        {
            _armL.Rotation = new Vector3(Mathf.Lerp(_armL.Rotation.X, 1.55f, 0.5f), 0, -0.05f);
            _armR.Rotation = new Vector3(Mathf.Lerp(_armR.Rotation.X, 1.55f, 0.5f), 0, 0.05f);
        }
        else if (s.Airborne || s.JumpCharging)
        {
            float up = s.Airborne ? 3.0f : 0.4f; // braços para cima para bloquear
            _armL.Rotation = new Vector3(Mathf.Lerp(_armL.Rotation.X, up, k), 0, -0.18f);
            _armR.Rotation = new Vector3(Mathf.Lerp(_armR.Rotation.X, up, k), 0, 0.18f);
        }
        else if (s.Dashing)
        {
            _armL.Rotation = new Vector3(Mathf.Lerp(_armL.Rotation.X, 1.3f, 0.6f), 0, 0.1f);
            _armR.Rotation = new Vector3(Mathf.Lerp(_armR.Rotation.X, 1.5f, 0.6f), 0, -0.1f);
        }
        else
        {
            float amp = s.Slowed || s.Stunned ? 0.3f : 1.1f;
            _armL.Rotation = new Vector3(Mathf.Lerp(_armL.Rotation.X, -swing * amp, k), 0, -0.1f);
            _armR.Rotation = new Vector3(Mathf.Lerp(_armR.Rotation.X, swing * amp, k), 0, 0.1f);
        }
    }

    /// <summary>Gira o braço (eixo −Y local) para apontar a mão ao alvo.</summary>
    static void PointArm(Node3D arm, Vector3 target, Vector3 rightRef)
    {
        var dir = target - arm.GlobalPosition;
        if (dir.LengthSquared() < 1e-6f) return;
        var y = -dir.Normalized();
        var z = rightRef.Cross(y);
        if (z.LengthSquared() < 1e-6f) z = Vector3.Forward;
        z = z.Normalized();
        var x = y.Cross(z).Normalized();
        arm.GlobalBasis = new Basis(x, y, z);
    }
}
