using Godot;

namespace AB;

/// <summary>
/// Jogador. ServerTick() roda só na autoridade (host) a partir de um PlayerInput.
/// Visual é placeholder (cápsulas) até a arte do Blender.
/// </summary>
public partial class Player : CharacterBody3D
{
    public int Id;
    public int Team;
    public string PlayerName = "Jogador";
    public bool IsBot;
    public bool IsLocalHuman;
    public Match M;

    public float Yaw;
    public float Stamina;
    public bool Running;
    public bool ChargingShot;
    public float ShotCharge;
    public float BallLockoutTimer;
    public float DribblePhase;
    public PlayerInput LastInput;

    float _regenDelay;
    int _lastBounceIdx;
    Node3D _visual;
    PlayerRig _rig;
    Label3D _nameLabel;

    public bool HasBall => M?.Ball?.Holder == this;
    public float ShotPower => Mathf.Clamp(ShotCharge / Tuning.I.ShotChargeTime, 0, 1);
    const float ReleaseAnimTime = 0.45f;
    float _releaseT;
    public Vector3 Forward => ShotMath.Dir(Yaw);
    public Vector3 Right => new(Mathf.Cos(Yaw), 0, -Mathf.Sin(Yaw));

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 1 | 2;
        FloorSnapLength = 0.2f;
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.8f }, Position = new Vector3(0, 0.9f, 0) });
        BuildVisual();
        Stamina = Tuning.I.StaminaMax;
    }

    void BuildVisual()
    {
        _rig = new PlayerRig(this, Team);
        _visual = _rig.Root;

        _nameLabel = new Label3D
        {
            Text = PlayerName,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Position = new Vector3(0, 2.25f, 0),
            FontSize = 44,
            OutlineSize = 12,
            Modulate = Palette.Team[Team].Lightened(0.35f),
            NoDepthTest = false,
            Visible = !IsLocalHuman,
        };
        AddChild(_nameLabel);
    }

    /// <summary>Simulação autoritativa de um tick.</summary>
    public void ServerTick(PlayerInput inp, float dt)
    {
        var t = Tuning.I;
        LastInput = inp;
        bool canMove = M.CanMove;
        bool canAct = M.CanAct;
        if (BallLockoutTimer > 0) BallLockoutTimer -= dt;

        var wish = canMove ? new Vector3(inp.Move.X, 0, inp.Move.Y) : Vector3.Zero;
        if (wish.LengthSquared() > 1f) wish = wish.Normalized();
        bool moving = wish.LengthSquared() > 0.01f;

        // ---- arremesso ----
        if (HasBall && canAct) HandleShot(inp, dt);
        else CancelShot(false);

        // ---- corrida / estamina ----
        bool wantRun = inp.IsHeld(Btn.Sprint) && moving && !ChargingShot;
        Running = wantRun && Stamina > 0 && (Running || Stamina >= t.StaminaMinToRun);
        if (Running)
        {
            Stamina = Mathf.Max(0, Stamina - t.StaminaDrainPerSec * dt);
            _regenDelay = t.StaminaRegenDelay;
            if (Stamina <= 0) Running = false;
        }
        else if (_regenDelay > 0) _regenDelay -= dt;
        else Stamina = Mathf.Min(t.StaminaMax, Stamina + t.StaminaRegenPerSec * dt);

        // ---- movimento ----
        float speed = Running ? t.RunSpeed : t.WalkSpeed;
        if (HasBall) speed *= t.CarrierSpeedMult;
        if (ChargingShot) speed *= t.ShotChargeMoveMult;
        var target = wish * speed;
        var v = Velocity;
        var hv = new Vector3(v.X, 0, v.Z);
        float accel = target.LengthSquared() >= hv.LengthSquared() ? t.Acceleration : t.Deceleration;
        hv = hv.MoveToward(target, accel * dt);
        v.Y = IsOnFloor() ? -0.5f : v.Y - t.PlayerGravity * dt;
        Velocity = new Vector3(hv.X, v.Y, hv.Z);
        MoveAndSlide();

        // ---- rotação ----
        float targetYaw = Yaw;
        if (ChargingShot) targetYaw = inp.AimYaw;
        else if (moving) targetYaw = ShotMath.YawOf(wish);
        Yaw = Mathf.LerpAngle(Yaw, targetYaw, 1f - Mathf.Exp(-t.TurnSpeed * dt));

        UpdateDribble(dt);
        ApplyPose(dt, hv.Length());
    }

    void HandleShot(PlayerInput inp, float dt)
    {
        if (!ChargingShot && inp.WasPressed(Btn.Shoot))
        {
            ChargingShot = true;
            ShotCharge = 0;
        }
        if (!ChargingShot) return;
        ShotCharge += dt;
        if (!inp.IsHeld(Btn.Shoot) || ShotCharge >= Tuning.I.ShotAutoReleaseTime) ReleaseShot();
    }

    /// <summary>Interrompe a carga sem arremessar (ex.: perdeu a bola, round reiniciou).</summary>
    public void CancelShot(bool feedback)
    {
        if (!ChargingShot) return;
        ChargingShot = false;
        ShotCharge = 0;
    }

    /// <summary>Plano do arremesso atual (para prévia, HUD e soltura).</summary>
    public bool GetShotPlan(out ShotMath.ShotPlan plan)
    {
        plan = default;
        if (!ChargingShot) return false;
        plan = ShotMath.Plan(GlobalPosition, Team, ShotCharge, LastInput.AimYaw, LastInput.AimPitch, M.Geo);
        return true;
    }

    void ReleaseShot()
    {
        if (!GetShotPlan(out var plan)) return;
        ChargingShot = false;
        ShotCharge = 0;
        BallLockoutTimer = Tuning.I.ShotPickupLockout;
        _releaseT = ReleaseAnimTime;
        M.Ball.Shoot(this, plan.Pos, plan.Vel);
    }

    void UpdateDribble(float dt)
    {
        if (!HasBall || ChargingShot) return;
        DribblePhase += dt * Mathf.Pi / Tuning.I.DribblePeriod;
        int idx = Mathf.FloorToInt((DribblePhase + Mathf.Pi / 2f) / Mathf.Pi);
        if (idx != _lastBounceIdx)
        {
            _lastBounceIdx = idx;
            Sfx.Play3D(M, "dribble", GlobalPosition, Tuning.I.DribbleSoundDb, (float)GD.RandRange(0.94, 1.06));
        }
    }

    /// <summary>Onde a bola fica enquanto este jogador a segura (quique automático, mesmo parado).</summary>
    public Vector3 HeldBallPosition()
    {
        var t = Tuning.I;
        if (ChargingShot) return GlobalPosition + Vector3.Up * 2.02f + Forward * 0.33f;
        float y = t.BallRadius + (t.DribbleHandHeight - t.BallRadius) * Mathf.Abs(Mathf.Cos(DribblePhase));
        return GlobalPosition + Right * 0.46f + Forward * 0.36f + Vector3.Up * y;
    }

    void ApplyPose(float dt, float hspeed)
    {
        if (_releaseT > 0) _releaseT -= dt;
        var st = new RigState
        {
            Yaw = Yaw, Speed = hspeed, HasBall = HasBall, Charging = ChargingShot, Charge = ShotCharge,
            Release = _releaseT / ReleaseAnimTime,
        };
        if (st.HasBall) st.BallPos = HeldBallPosition();
        _rig.Animate(st, dt);
    }

    public void Teleport(Vector3 pos, float yaw)
    {
        GlobalPosition = pos;
        Velocity = Vector3.Zero;
        Yaw = yaw;
        ChargingShot = false;
        ShotCharge = 0;
        Running = false;
        _releaseT = 0;
        _visual.Rotation = new Vector3(0, Yaw, 0);
        ResetPhysicsInterpolation();
    }

    public void SetNameVisible(bool v) { if (_nameLabel != null) _nameLabel.Visible = v; }
}
