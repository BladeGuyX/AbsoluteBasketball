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

        // agacha carregando o arremesso; "pulinho" e esticada ao soltar
        float crouchTarget = s.Charging ? Mathf.Clamp(s.Charge / 0.25f, 0, 1) : 0f;
        _crouch = Mathf.Lerp(_crouch, crouchTarget, k);
        float rel = Mathf.Clamp(s.Release, 0, 1);
        float relWave = rel > 0 ? Mathf.Sin(Mathf.Pi * (1f - rel)) : 0f;
        float hop = relWave * 0.35f;
        float bob = Mathf.Abs(Mathf.Sin(_stride)) * 0.07f * walking - _crouch * 0.12f;

        Root.Rotation = new Vector3(0, s.Yaw, 0);
        Root.Position = new Vector3(0, bob + hop, 0);
        float sy = 1f - 0.09f * _crouch + 0.1f * relWave;
        float sxz = 1f + 0.05f * _crouch - 0.04f * relWave;
        Root.Scale = new Vector3(sxz, sy, sxz);

        _lean = Mathf.Lerp(_lean, -0.2f * move + 0.12f * _crouch - 0.08f * relWave, k);
        _upper.Rotation = new Vector3(_lean, Mathf.Sin(_stride) * 0.08f * walking, 0);

        float legBend = _crouch * 0.25f;
        _legL.Rotation = new Vector3(swing + legBend, 0, 0);
        _legR.Rotation = new Vector3(-swing + legBend, 0, 0);

        float r = t.BallRadius;
        var right = new Vector3(Mathf.Cos(s.Yaw), 0, -Mathf.Sin(s.Yaw));
        if (s.HasBall && s.Charging)
        {
            PointArm(_armL, s.BallPos - right * (r + 0.03f), right);
            PointArm(_armR, s.BallPos + right * (r + 0.03f), right);
        }
        else if (s.HasBall)
        {
            // mão direita acompanha o topo da bola no quique; esquerda protege a bola
            var top = s.BallPos + Vector3.Up * r;
            var shoulder = _armR.GlobalPosition;
            top.Y = Mathf.Max(top.Y, shoulder.Y - ArmLen);
            PointArm(_armR, top, right);
            _armL.Rotation = new Vector3(Mathf.Lerp(_armL.Rotation.X, 0.7f - swing * 0.3f, k), 0, -0.35f);
        }
        else if (rel > 0)
        {
            float up = 2.85f * Mathf.Clamp(rel * 1.6f, 0, 1);
            _armL.Rotation = new Vector3(Mathf.Lerp(_armL.Rotation.X, up, 0.5f), 0, -0.08f);
            _armR.Rotation = new Vector3(Mathf.Lerp(_armR.Rotation.X, up, 0.5f), 0, 0.08f);
        }
        else
        {
            _armL.Rotation = new Vector3(Mathf.Lerp(_armL.Rotation.X, -swing * 1.1f, k), 0, -0.1f);
            _armR.Rotation = new Vector3(Mathf.Lerp(_armR.Rotation.X, swing * 1.1f, k), 0, 0.1f);
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
