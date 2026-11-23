using Godot;

namespace AB;

/// <summary>
/// Jogador. ServerTick() roda só na autoridade (host) a partir de um PlayerInput.
/// Ações: arremesso, passe (normal/alto), salto (bloqueio), roubo, drible especial.
/// Visual/animação em PlayerRig (placeholder até a arte do Blender).
/// </summary>
public partial class Player : CharacterBody3D
{
    public int Id;
    public int Team;
    public string PlayerName = "Jogador";
    public bool IsBot;
    public bool IsDummy;
    public bool IsLocalHuman;
    public Match M;
    public Vector3 HomePos;          // posição do início do round (bonecos voltam para cá)

    // ---- estado ----
    public float Yaw;
    public float Stamina;
    public bool Running;
    public bool ChargingShot;
    public float ShotCharge;
    public bool ChargingPass;
    public float PassCharge;
    public bool PassHigh;
    public Player PassTarget;        // alvo mostrado enquanto carrega (passe mirado)
    public bool ChargingJump;
    public float JumpCharge;
    public bool Airborne;
    public float LandLock;
    public float StealDash;
    public float StealFail;
    public int DribbleCharges;
    public float DribbleRecharge;
    public float DribbleActive;
    public float BallLockoutTimer;
    public float DribblePhase;
    public PlayerInput LastInput;

    float _regenDelay;
    int _lastBounceIdx;
    float _airTime;
    Vector3 _jumpVel;
    Node3D _visual;
    PlayerRig _rig;
    Label3D _nameLabel;
    const float ReleaseAnimTime = 0.45f;
    float _releaseT, _passReleaseT;

    public bool HasBall => M?.Ball?.Holder == this;
    public bool IsDribbling => DribbleActive > 0;
    public bool CanCatch => true;
    /// <summary>Pode ter a bola roubada? (não se estiver driblando, no ar ou carregando arremesso)</summary>
    public bool CanBeStolen => !IsDribbling && !Airborne && !ChargingShot && !ChargingPass;
    public Vector3 Forward => ShotMath.Dir(Yaw);
    public Vector3 Right => new(Mathf.Cos(Yaw), 0, -Mathf.Sin(Yaw));

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 1 | 2;
        FloorSnapLength = 0.2f;
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.8f }, Position = new Vector3(0, 0.9f, 0) });
        _rig = new PlayerRig(this, Team);
        _visual = _rig.Root;
        _nameLabel = new Label3D
        {
            Text = PlayerName,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Position = new Vector3(0, 2.35f, 0),
            FontSize = 44,
            OutlineSize = 12,
            Modulate = Palette.Team[Team].Lightened(0.35f),
            Visible = !IsLocalHuman,
        };
        AddChild(_nameLabel);
        Stamina = Tuning.I.StaminaMax;
        DribbleCharges = Mathf.RoundToInt(Tuning.I.DribbleCharges);
    }

    // =====================================================================
    /// <summary>Simulação autoritativa de um tick.</summary>
    public void ServerTick(PlayerInput inp, float dt)
    {
        var t = Tuning.I;
        LastInput = inp;
        TickTimers(dt);

        bool canMove = M.CanMove && LandLock <= 0;
        bool canAct = M.CanAct && LandLock <= 0 && !Airborne;

        var wish = canMove ? new Vector3(inp.Move.X, 0, inp.Move.Y) : Vector3.Zero;
        if (wish.LengthSquared() > 1f) wish = wish.Normalized();
        bool moving = wish.LengthSquared() > 0.01f;

        // ---------- ações ----------
        if (HasBall)
        {
            ChargingJump = false;
            StealDash = 0;
            if (canAct && !IsDribbling)
            {
                if (!ChargingShot && !ChargingPass && inp.WasPressed(Btn.Dribble) && DribbleCharges > 0) StartDribble();
                else
                {
                    if (!ChargingPass) HandleShot(inp, dt);
                    if (!ChargingShot) HandlePass(inp, dt);
                }
            }
            else { CancelShot(false); CancelPass(); }
        }
        else
        {
            CancelShot(false);
            CancelPass();
            DribbleActive = 0;
            if (canAct)
            {
                HandleJump(inp, dt, wish);
                HandleSteal(inp);
            }
            else ChargingJump = false;
        }

        // ---------- corrida / estamina ----------
        bool wantRun = inp.IsHeld(Btn.Sprint) && moving && !ChargingShot && StealFail <= 0 && !Airborne;
        Running = wantRun && Stamina > 0 && (Running || Stamina >= t.StaminaMinToRun);
        if (Running)
        {
            Stamina = Mathf.Max(0, Stamina - t.StaminaDrainPerSec * dt);
            _regenDelay = t.StaminaRegenDelay;
            if (Stamina <= 0) Running = false;
        }
        else if (_regenDelay > 0) _regenDelay -= dt;
        else Stamina = Mathf.Min(t.StaminaMax, Stamina + t.StaminaRegenPerSec * dt);

        // ---------- movimento ----------
        float speed = Running ? t.RunSpeed : t.WalkSpeed;
        if (HasBall) speed *= t.CarrierSpeedMult;
        if (ChargingShot) speed *= t.ShotChargeMoveMult;
        if (ChargingPass) speed *= t.PassChargeMoveMult;
        if (IsDribbling) speed *= t.DribbleSpeedMult;
        if (StealFail > 0) speed *= t.StealFailSpeedMult;
        if (IsDummy) speed *= t.DummySpeedMult;
        if (ChargingJump) speed = 0;

        var v = Velocity;
        Vector3 hv;
        if (Airborne) hv = new Vector3(_jumpVel.X, 0, _jumpVel.Z);
        else if (StealDash > 0) hv = Forward * t.StealDashSpeed;
        else
        {
            hv = new Vector3(v.X, 0, v.Z);
            var target = wish * speed;
            float accel = target.LengthSquared() >= hv.LengthSquared() ? t.Acceleration : t.Deceleration;
            hv = hv.MoveToward(target, accel * dt);
        }
        if (_jumpVel.Y > 0) { v.Y = _jumpVel.Y; _jumpVel.Y = 0; }
        else v.Y = IsOnFloor() && !Airborne ? -0.5f : v.Y - t.PlayerGravity * dt;
        Velocity = new Vector3(hv.X, v.Y, hv.Z);
        MoveAndSlide();

        if (Airborne)
        {
            _airTime += dt;
            if (_airTime > 0.08f && IsOnFloor())
            {
                Airborne = false;
                LandLock = t.LandLockTime;
                _jumpVel = Vector3.Zero;
                M.OnLanded(this);
            }
        }

        // ---------- roubo: acertou alguém? ----------
        if (StealDash > 0) CheckStealHit();

        // ---------- rotação ----------
        float targetYaw = Yaw;
        if (ChargingShot || ChargingPass || ChargingJump) targetYaw = inp.AimYaw;
        else if (moving && !Airborne && StealDash <= 0 && LandLock <= 0) targetYaw = ShotMath.YawOf(wish);
        Yaw = Mathf.LerpAngle(Yaw, targetYaw, 1f - Mathf.Exp(-t.TurnSpeed * dt));

        UpdateDribble(dt);
        ApplyPose(dt, new Vector2(Velocity.X, Velocity.Z).Length());
    }

    void TickTimers(float dt)
    {
        var t = Tuning.I;
        if (BallLockoutTimer > 0) BallLockoutTimer -= dt;
        if (LandLock > 0 && !Airborne) LandLock -= dt;
        if (StealFail > 0) StealFail -= dt;
        if (DribbleActive > 0) DribbleActive -= dt;
        int max = Mathf.RoundToInt(t.DribbleCharges);
        if (DribbleCharges < max)
        {
            DribbleRecharge += dt;
            if (DribbleRecharge >= t.DribbleRecharge) { DribbleCharges++; DribbleRecharge = 0; }
        }
        else DribbleRecharge = 0;
        if (StealDash > 0)
        {
            StealDash -= dt;
            if (StealDash <= 0) FailSteal();
        }
    }

    // =====================================================================
    // Arremesso

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

    public bool GetShotPlan(out ShotMath.ShotPlan plan)
    {
        plan = default;
        if (!ChargingShot) return false;
        plan = ShotMath.Plan(GlobalPosition, Team, LastInput.AimYaw, LastInput.AimPitch, M.Geo);
        return true;
    }

    void ReleaseShot()
    {
        if (!GetShotPlan(out var plan)) return;
        ChargingShot = false;
        ShotCharge = 0;
        BallLockoutTimer = Tuning.I.ShotPickupLockout;
        _releaseT = ReleaseAnimTime;
        M.Ball.Throw(this, plan.Pos, plan.Vel, BallState.Shot);
        M.LogEvent("SHOT", this);
    }

    // =====================================================================
    // Passe

    void HandlePass(PlayerInput inp, float dt)
    {
        var t = Tuning.I;
        if (!ChargingPass && inp.WasPressed(Btn.Pass))
        {
            ChargingPass = true;
            PassCharge = 0;
            PassHigh = false;
            PassTarget = null;
        }
        if (!ChargingPass) return;
        PassCharge += dt;
        if (inp.WasPressed(Btn.Variation)) PassHigh = !PassHigh;
        bool tap = PassCharge < t.PassTapTime;
        if (inp.IsHeld(Btn.Pass) && PassCharge < t.PassAutoReleaseTime) return;

        ChargingPass = false;
        PassTarget = null;
        var state = PassHigh ? BallState.HighPass : BallState.Pass;
        if (tap)
        {
            // passe rápido: aliado livre mais próximo (se não houver, não passa)
            var target = Passing.Nearest(this, PassHigh);
            if (target == null) return;
            var vel = Passing.Velocity(this, target, PassHigh, out var pos);
            Yaw = ShotMath.YawOf(target.GlobalPosition - GlobalPosition);
            ThrowPass(pos, vel, state, target);
        }
        else
        {
            // passe carregado: lança na direção da câmera, sem alvo e sem assistência
            var vel = Passing.AimedVelocity(inp.AimYaw, inp.AimPitch, PassHigh);
            Yaw = inp.AimYaw;
            ThrowPass(Passing.ReleasePos(this, GlobalPosition + ShotMath.Dir(inp.AimYaw)), vel, state, null);
        }
    }

    void ThrowPass(Vector3 pos, Vector3 vel, BallState state, Player target)
    {
        BallLockoutTimer = Tuning.I.PassLockout;
        _passReleaseT = 0.3f;
        M.Ball.Throw(this, pos, vel, state, target);
        M.LogEvent(state == BallState.HighPass ? "HIGHPASS" : "PASS", this, target);
    }

    public void CancelPass()
    {
        ChargingPass = false;
        PassTarget = null;
    }

    // =====================================================================
    // Salto

    void HandleJump(PlayerInput inp, float dt, Vector3 wish)
    {
        var t = Tuning.I;
        if (!ChargingJump && inp.WasPressed(Btn.Jump) && StealFail <= 0 && StealDash <= 0 && IsOnFloor())
        {
            ChargingJump = true;
            JumpCharge = 0;
            _jumpWish = wish;
            _jumpRun = inp.IsHeld(Btn.Sprint) && Stamina > 0;
        }
        if (!ChargingJump) return;
        JumpCharge = Mathf.Min(JumpCharge + dt, t.JumpChargeTime);
        if (wish.LengthSquared() > 0.01f) { _jumpWish = wish; _jumpRun = inp.IsHeld(Btn.Sprint) && Stamina > 0; }
        if (inp.IsHeld(Btn.Jump)) return;

        // pula: direção vem do movimento antes de pular, quase sem controle no ar
        float k = t.JumpChargeTime > 0 ? JumpCharge / t.JumpChargeTime : 1f;
        float vy = Mathf.Lerp(t.JumpVelMin, t.JumpVelMax, k);
        float hs = (_jumpRun ? t.RunSpeed : t.WalkSpeed) * t.JumpCarry;
        var h = _jumpWish * hs;
        var cur = new Vector3(Velocity.X, 0, Velocity.Z);
        if (cur.Length() > h.Length()) h = cur * t.JumpCarry;
        _jumpVel = new Vector3(h.X, vy, h.Z);
        ChargingJump = false;
        Airborne = true;
        _airTime = 0;
        M.LogEvent("JUMP", this);
    }
    Vector3 _jumpWish;
    bool _jumpRun;

    // =====================================================================
    // Roubo

    void HandleSteal(PlayerInput inp)
    {
        if (!inp.WasPressed(Btn.Steal) || StealFail > 0 || StealDash > 0 || ChargingJump || !IsOnFloor()) return;
        if (inp.Move.LengthSquared() > 0.01f) Yaw = ShotMath.YawOf(new Vector3(inp.Move.X, 0, inp.Move.Y));
        StealDash = Tuning.I.StealDashTime;
        M.LogEvent("STEAL_TRY", this);
    }

    void CheckStealHit()
    {
        var t = Tuning.I;
        var holder = M.Ball.Holder;
        if (holder == null || holder.Team == Team) return;
        var rel = holder.GlobalPosition - GlobalPosition; rel.Y = 0;
        if (rel.Length() > t.StealReach) return;
        float ang = Mathf.Abs(ShotMath.WrapAngle(ShotMath.YawOf(rel) - Yaw));
        if (ang > Mathf.DegToRad(t.StealAngleDeg)) return;
        StealDash = 0;
        if (holder.CanBeStolen)
        {
            holder.CancelShot(false);
            holder.CancelPass();
            M.Ball.GiveTo(this);
            M.OnSteal(this, holder);
        }
        else FailSteal(holder);
    }

    void FailSteal(Player immuneTarget = null)
    {
        StealDash = 0;
        StealFail = Tuning.I.StealFailTime;
        M.OnStealFailed(this, immuneTarget);
    }

    // =====================================================================
    // Drible especial

    void StartDribble()
    {
        DribbleCharges--;
        DribbleActive = Tuning.I.DribbleDuration;
        M.LogEvent("DRIBBLE", this);
        Sfx.Play3D(M, "dribble", GlobalPosition, Tuning.I.DribbleSoundDb + 6, 1.2f);
    }

    void UpdateDribble(float dt)
    {
        if (!HasBall || ChargingShot || ChargingPass) return;
        float speed = IsDribbling ? Tuning.I.DribbleBounceMult : 1f;
        DribblePhase += dt * speed * Mathf.Pi / Tuning.I.DribblePeriod;
        int idx = Mathf.FloorToInt((DribblePhase + Mathf.Pi / 2f) / Mathf.Pi);
        if (idx != _lastBounceIdx)
        {
            _lastBounceIdx = idx;
            Sfx.Play3D(M, "dribble", GlobalPosition, Tuning.I.DribbleSoundDb, (float)GD.RandRange(0.94, 1.06));
        }
    }

    /// <summary>Lado da bola no drible: +1 direita, −1 esquerda (no drible especial ela troca de mão).</summary>
    public float BallSide => IsDribbling ? Mathf.Sin(DribblePhase * 0.5f + 0.6f) : 1f;

    /// <summary>Onde a bola fica enquanto este jogador a segura.</summary>
    public Vector3 HeldBallPosition()
    {
        var t = Tuning.I;
        if (ChargingShot) return GlobalPosition + Vector3.Up * 2.02f + Forward * 0.33f;
        if (ChargingPass) return GlobalPosition + Vector3.Up * 1.25f + Forward * 0.5f;
        // driblando: agachado, a mão encontra a bola mais baixo (quique curto e rápido)
        float hand = IsDribbling ? Mathf.Lerp(t.DribbleHandHeight, t.BallRadius * 2.2f, Mathf.Clamp(t.DribbleCrouch, 0, 1) * 0.6f) : t.DribbleHandHeight;
        float y = t.BallRadius + (hand - t.BallRadius) * Mathf.Abs(Mathf.Cos(DribblePhase));
        return GlobalPosition + Right * (0.46f * BallSide) + Forward * 0.36f + Vector3.Up * y;
    }

    void ApplyPose(float dt, float hspeed)
    {
        if (_releaseT > 0) _releaseT -= dt;
        if (_passReleaseT > 0) _passReleaseT -= dt;
        var st = new RigState
        {
            Yaw = Yaw, Speed = Airborne || StealDash > 0 ? 0 : hspeed, HasBall = HasBall, Charging = ChargingShot, Charge = ShotCharge,
            Release = _releaseT / ReleaseAnimTime, PassCharging = ChargingPass, PassRelease = _passReleaseT / 0.3f,
            JumpCharging = ChargingJump, Airborne = Airborne, Dashing = StealDash > 0, Slowed = StealFail > 0,
            Landing = LandLock > 0, BallSide = BallSide, Dribbling = IsDribbling,
        };
        if (st.HasBall) st.BallPos = HeldBallPosition();
        _rig.Animate(st, dt);
    }

    /// <summary>Reposiciona no início do round e limpa estados de ação.</summary>
    public void Teleport(Vector3 pos, float yaw)
    {
        GlobalPosition = pos;
        HomePos = pos;
        Velocity = Vector3.Zero;
        Yaw = yaw;
        ChargingShot = false; ShotCharge = 0;
        CancelPass();
        ChargingJump = false; Airborne = false; _jumpVel = Vector3.Zero; LandLock = 0;
        StealDash = 0; StealFail = 0; DribbleActive = 0;
        Running = false;
        _releaseT = 0; _passReleaseT = 0;
        _visual.Rotation = new Vector3(0, Yaw, 0);
        ResetPhysicsInterpolation();
    }

    public void SetNameVisible(bool v) { if (_nameLabel != null) _nameLabel.Visible = v; }
}
