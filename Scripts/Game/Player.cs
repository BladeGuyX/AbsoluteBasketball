using Godot;

namespace AB;

/// <summary>
/// Jogador. ServerTick() roda só na autoridade (host) a partir de um PlayerInput.
/// Ações: arremesso, passe (normal/alto), salto (bloqueio), roubo, drible especial.
/// Visual/animação em PlayerRig (placeholder até a arte do Blender).
/// </summary>
public enum FlightKind { None, Dunk }

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
    public float Stun;                // atordoado (errou enterrada): sem mover nem agir

    // ---- enterrada / tapinha (voo roteirizado) ----
    public FlightKind Flight;
    public float FlightT, FlightDur;
    public int FlightHoop;
    public bool DunkPressed;
    public float DunkPressT;
    public float SkillFlash;          // >0: mostra o resultado do skill check (HUD)
    public bool SkillFlashOk;
    Vector3 _flightFrom, _flightTo;
    bool _stunOnLand;

    // prévia do arremesso = exatamente o que vai sair (calculada no fim de cada tick)
    ShotMath.ShotPlan _shotPlan;
    bool _shotPlanValid;

    float _regenDelay;
    int _lastBounceIdx;
    float _airTime;
    Vector3 _jumpVel;
    Node3D _visual;
    PlayerRig _rig;
    Label3D _nameLabel;
    Node3D _stars;                    // estrelinhas girando = atordoado
    const float ReleaseAnimTime = 0.45f;
    float _releaseT, _passReleaseT;

    public bool HasBall => M?.Ball?.Holder == this;
    public bool IsDribbling => DribbleActive > 0;
    public bool CanCatch => true;
    /// <summary>Pode ter a bola roubada? (não se estiver driblando, no ar ou carregando arremesso)</summary>
    public bool CanBeStolen => !IsDribbling && !Airborne && !ChargingShot && !ChargingPass && Flight == FlightKind.None;
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
        _stars = new Node3D { Position = new Vector3(0, 2.15f, 0), Visible = false };
        var starMat = Mat.Unshaded(new Color("ffd23f"));
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.Tau / 3f;
            _stars.AddChild(Mat.MeshNode(new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 8, Rings = 4 }, starMat,
                new Vector3(Mathf.Cos(a) * 0.3f, 0, Mathf.Sin(a) * 0.3f), false));
        }
        AddChild(_stars);
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
        if (SkillFlash > 0) SkillFlash -= dt;

        if (Flight != FlightKind.None)
        {
            FlightTick(inp, dt);
            ApplyPose(dt, 0);
            return;
        }

        bool canMove = M.CanMove && LandLock <= 0 && Stun <= 0;
        bool canAct = M.CanAct && LandLock <= 0 && !Airborne && Stun <= 0;

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
                else if (!ChargingShot && !ChargingPass && inp.WasPressed(Btn.Jump) && CanDunk()) StartDunk();
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
        bool wantRun = inp.IsHeld(Btn.Sprint) && moving && !ChargingShot && !IsDribbling && StealFail <= 0 && !Airborne;
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
        if (IsDribbling) speed = t.WalkSpeed * t.DribbleSpeedMult; // driblando: fração da velocidade andando
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
                if (_stunOnLand) { _stunOnLand = false; Stun = t.DunkStunTime; }
                else LandLock = t.LandLockTime;
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
        _shotPlanValid = ChargingShot;
        if (ChargingShot) _shotPlan = ShotMath.Plan(GlobalPosition, Team, inp.AimYaw, inp.AimPitch, M.Geo);
        ApplyPose(dt, new Vector2(Velocity.X, Velocity.Z).Length());
    }

    void TickTimers(float dt)
    {
        var t = Tuning.I;
        if (BallLockoutTimer > 0) BallLockoutTimer -= dt;
        if (LandLock > 0 && !Airborne) LandLock -= dt;
        if (Stun > 0 && !Airborne) Stun -= dt;
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

    /// <summary>Plano mostrado na prévia (calculado no fim do último tick). O arremesso usa exatamente este plano.</summary>
    public bool GetShotPlan(out ShotMath.ShotPlan plan)
    {
        plan = default;
        if (!ChargingShot) return false;
        if (!_shotPlanValid) _shotPlan = ShotMath.Plan(GlobalPosition, Team, LastInput.AimYaw, LastInput.AimPitch, M.Geo);
        _shotPlanValid = true;
        plan = _shotPlan;
        return true;
    }

    void ReleaseShot()
    {
        if (!GetShotPlan(out var plan)) return;
        ChargingShot = false;
        ShotCharge = 0;
        _shotPlanValid = false;
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
        if (Flight != FlightKind.None) return GlobalPosition + Vector3.Up * t.DunkHandReach + Forward * 0.35f;
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
            Dunking = Flight != FlightKind.None, Stunned = Stun > 0 || _stunOnLand,
        };
        if (st.HasBall) st.BallPos = HeldBallPosition();
        _stars.Visible = Stun > 0;
        if (_stars.Visible) _stars.RotateY(dt * 5f);
        _rig.Animate(st, dt);
    }

    /// <summary>Reposiciona no início do round e limpa estados de ação.</summary>
    public void Teleport(Vector3 pos, float yaw, bool setHome = true)
    {
        GlobalPosition = pos;
        if (setHome) HomePos = pos;
        Velocity = Vector3.Zero;
        Yaw = yaw;
        ChargingShot = false; ShotCharge = 0;
        CancelPass();
        ChargingJump = false; Airborne = false; _jumpVel = Vector3.Zero; LandLock = 0;
        StealDash = 0; StealFail = 0; DribbleActive = 0;
        Flight = FlightKind.None; Stun = 0; _stunOnLand = false; SkillFlash = 0; _shotPlanValid = false;
        Running = false;
        _releaseT = 0; _passReleaseT = 0;
        _visual.Rotation = new Vector3(0, Yaw, 0);
        ResetPhysicsInterpolation();
    }

    // =====================================================================
    // Enterrada (skill check)

    Vector3 FlatToRim(int hoop) { var v = M.Geo.RimCenter[hoop] - GlobalPosition; v.Y = 0; return v; }

    /// <summary>
    /// Pode enterrar agora? Com a bola, no chão, perto da cesta adversária e indo (ou, parado, virado) para ela.
    /// Não precisa correr (v0.7). Usado também pelo indicativo da HUD.
    /// </summary>
    public bool CanDunk()
    {
        var t = Tuning.I;
        if (!HasBall || !IsOnFloor() || Airborne || Flight != FlightKind.None || Stun > 0 || LandLock > 0) return false;
        if (IsDribbling || ChargingShot || ChargingPass || M == null || !M.CanAct) return false;
        int hoop = CourtGeo.AttackHoop(Team);
        var to = FlatToRim(hoop);
        if (to.Length() > t.DunkRange) return false;
        if (to.Length() < 0.3f) return true;
        var hv = new Vector3(Velocity.X, 0, Velocity.Z);
        var dir = hv.Length() >= 0.6f ? hv : Forward;
        return dir.AngleTo(to) <= Mathf.DegToRad(t.DunkMaxAngleDeg);
    }

    void BeginFlight(FlightKind kind, float dur, Vector3 handTarget, int hoop)
    {
        var t = Tuning.I;
        CancelShot(false); CancelPass(); ChargingJump = false; DribbleActive = 0;
        var to = FlatToRim(hoop);
        var dir = to.LengthSquared() > 0.01f ? to.Normalized() : Forward;
        Yaw = ShotMath.YawOf(dir);
        Flight = kind;
        FlightHoop = hoop;
        FlightT = 0;
        FlightDur = dur;
        DunkPressed = false;
        _flightFrom = GlobalPosition;
        // os pés ficam onde a mão (bola) alcança o alvo
        _flightTo = handTarget - dir * 0.35f - Vector3.Up * t.DunkHandReach;
        _flightTo.Y = Mathf.Max(_flightTo.Y, 0.05f);
        Airborne = true;
        _airTime = 0;
        _jumpVel = Vector3.Zero;
        Velocity = Vector3.Zero;
    }

    void StartDunk()
    {
        var t = Tuning.I;
        int hoop = CourtGeo.AttackHoop(Team);
        var rim = M.Geo.RimCenter[hoop];
        var dir = FlatToRim(hoop).Normalized();
        // a bola chega um pouco acima e à frente do centro do aro
        float dur = t.DunkFlightTime + (float)GD.RandRange(-t.DunkFlightJitter, t.DunkFlightJitter);
        BeginFlight(FlightKind.Dunk, Mathf.Max(dur, 0.25f), rim - dir * 0.1f + Vector3.Up * 0.35f, hoop);
        M.LogEvent("DUNK", this);
    }

    void FlightTick(PlayerInput inp, float dt)
    {
        var t = Tuning.I;
        FlightT += dt;
        float k = Mathf.Clamp(FlightT / Mathf.Max(FlightDur, 0.01f), 0, 1);
        float kh = 1f - (1f - k) * (1f - k);          // horizontal: sai rápido e desacelera
        float kv = Mathf.Sin(k * Mathf.Pi * 0.5f);     // vertical: sobe rápido, "flutua" no topo
        var p = _flightFrom.Lerp(_flightTo, kh);
        p.Y = Mathf.Lerp(_flightFrom.Y, _flightTo.Y, kv);
        GlobalPosition = p;
        Velocity = Vector3.Zero;
        Yaw = Mathf.LerpAngle(Yaw, ShotMath.YawOf(FlatToRim(FlightHoop)), 1f - Mathf.Exp(-t.TurnSpeed * dt));

        // enterrada: skill check SÓ com o botão de arremesso no momento de tocar a cesta (Espaço não vale — v0.7)
        float half = t.DunkWindow * 0.5f;
        if (!HasBall) { EndFlight(); return; }
        if (!DunkPressed && inp.WasPressed(Btn.Shoot)) { DunkPressed = true; DunkPressT = FlightT; }
        if (DunkPressed)
        {
            bool inWindow = Mathf.Abs(DunkPressT - FlightDur) <= half;
            if (inWindow) { FinishDunk(true); return; }
            if (FlightT >= FlightDur) { FinishDunk(false); return; } // apertou cedo demais: erra ao tocar a cesta
        }
        else if (FlightT >= FlightDur + half) FinishDunk(false); // não apertou a tempo
    }

    void FinishDunk(bool ok)
    {
        var t = Tuning.I;
        var rim = M.Geo.RimCenter[FlightHoop];
        if (Flight == FlightKind.Dunk) { SkillFlash = 0.6f; SkillFlashOk = ok; }
        if (ok)
        {
            BallLockoutTimer = 0.8f;
            M.Ball.Throw(this, rim + Vector3.Up * 0.3f, new Vector3(0, -7f, 0), BallState.Shot);
            Sfx.Play3D(M, "rim", rim, 2, 0.8f);
            Sfx.Play3D(M, "board", rim, 0, 0.9f);
            M.LogEvent("DUNK_OK", this);
        }
        else
        {
            // bola cai do lado da cesta; o jogador cai e fica atordoado
            var side = Right;
            BallLockoutTimer = 0.8f;
            M.Ball.Drop(rim + side * (t.RimRadius + 0.45f) + Vector3.Up * 0.1f, side * 1.4f + Vector3.Down * 1f);
            Sfx.Play3D(M, "rim", rim, 0, 1.25f);
            _stunOnLand = true;
            M.LogEvent("DUNK_FAIL", this);
        }
        EndFlight();
    }

    /// <summary>Termina o voo roteirizado: o jogador cai normalmente (gravidade).</summary>
    public void EndFlight()
    {
        if (Flight == FlightKind.None) return;
        Flight = FlightKind.None;
        Airborne = true;
        _airTime = 0;
        _jumpVel = Vector3.Zero;
        Velocity = Vector3.Zero;
    }

    // =====================================================================
    // Rede (Etapa 5): estado replicado do host para os clientes

    /// <summary>Tudo que o cliente precisa para desenhar/animar este jogador e mostrar a HUD dele.</summary>
    public struct NetState
    {
        public Vector3 Pos, Vel;
        public float Yaw;
        public ushort Flags;
        public float Stamina, ShotCharge, PassCharge, Stun, StealFail, StealDash, DribbleActive, LandLock, DribblePhase,
            ReleaseT, PassReleaseT, FlightT, FlightDur, DunkPressT, SkillFlash, DribbleRecharge;
        public byte DribbleCharges;
    }

    [System.Flags]
    enum NF : ushort
    {
        Running = 1, ChargingShot = 2, ChargingPass = 4, PassHigh = 8, ChargingJump = 16, Airborne = 32, Dunk = 64,
        DunkPressed = 128, SkillFlashOk = 256, StunOnLand = 512, DunkReady = 1024,
    }

    /// <summary>Cliente: "pode enterrar" calculado no host (o boneco do cliente não tem física de chão).</summary>
    public bool NetDunkReady;
    /// <summary>Setinhas de "pode enterrar" (host: calcula; cliente: o que o host mandou).</summary>
    public bool DunkReadyVisual => M != null && M.IsClient ? NetDunkReady : CanDunk();

    public NetState CaptureNet()
    {
        NF f = 0;
        if (Running) f |= NF.Running;
        if (ChargingShot) f |= NF.ChargingShot;
        if (ChargingPass) f |= NF.ChargingPass;
        if (PassHigh) f |= NF.PassHigh;
        if (ChargingJump) f |= NF.ChargingJump;
        if (Airborne) f |= NF.Airborne;
        if (Flight == FlightKind.Dunk) f |= NF.Dunk;
        if (DunkPressed) f |= NF.DunkPressed;
        if (SkillFlashOk) f |= NF.SkillFlashOk;
        if (_stunOnLand) f |= NF.StunOnLand;
        if (IsLocalHuman || !IsBot) { if (CanDunk()) f |= NF.DunkReady; }
        return new NetState
        {
            Pos = GlobalPosition, Vel = Velocity, Yaw = Yaw, Flags = (ushort)f,
            Stamina = Stamina, ShotCharge = ShotCharge, PassCharge = PassCharge, Stun = Stun, StealFail = StealFail, StealDash = StealDash,
            DribbleActive = DribbleActive, LandLock = LandLock, DribblePhase = DribblePhase, ReleaseT = _releaseT, PassReleaseT = _passReleaseT,
            FlightT = FlightT, FlightDur = FlightDur, DunkPressT = DunkPressT, SkillFlash = SkillFlash, DribbleRecharge = DribbleRecharge,
            DribbleCharges = (byte)Mathf.Clamp(DribbleCharges, 0, 255),
        };
    }

    public static void WriteNet(NetWriter w, in NetState s)
    {
        w.V3(s.Pos); w.V3(s.Vel); w.F(s.Yaw); w.U16(s.Flags);
        w.F(s.Stamina); w.F(s.ShotCharge); w.F(s.PassCharge); w.F(s.Stun); w.F(s.StealFail); w.F(s.StealDash); w.F(s.DribbleActive);
        w.F(s.LandLock); w.F(s.DribblePhase); w.F(s.ReleaseT); w.F(s.PassReleaseT); w.F(s.FlightT); w.F(s.FlightDur); w.F(s.DunkPressT);
        w.F(s.SkillFlash); w.F(s.DribbleRecharge); w.U8(s.DribbleCharges);
    }

    public static NetState ReadNet(NetReader r) => new()
    {
        Pos = r.V3(), Vel = r.V3(), Yaw = r.F(), Flags = r.U16(),
        Stamina = r.F(), ShotCharge = r.F(), PassCharge = r.F(), Stun = r.F(), StealFail = r.F(), StealDash = r.F(), DribbleActive = r.F(),
        LandLock = r.F(), DribblePhase = r.F(), ReleaseT = r.F(), PassReleaseT = r.F(), FlightT = r.F(), FlightDur = r.F(), DunkPressT = r.F(),
        SkillFlash = r.F(), DribbleRecharge = r.F(), DribbleCharges = r.U8(),
    };

    /// <summary>Cliente: aplica o estado (já interpolado) e anima. Não simula nada.</summary>
    public void ClientApply(in NetState s, float dt)
    {
        var f = (NF)s.Flags;
        bool jump = s.Pos.DistanceTo(GlobalPosition) > 4f; // teleporte (início de round / lance livre): não desliza
        GlobalPosition = s.Pos;
        if (jump) ResetPhysicsInterpolation();
        Velocity = s.Vel;
        Yaw = s.Yaw;
        Running = (f & NF.Running) != 0;
        ChargingShot = (f & NF.ChargingShot) != 0;
        ChargingPass = (f & NF.ChargingPass) != 0;
        PassHigh = (f & NF.PassHigh) != 0;
        ChargingJump = (f & NF.ChargingJump) != 0;
        Airborne = (f & NF.Airborne) != 0;
        Flight = (f & NF.Dunk) != 0 ? FlightKind.Dunk : FlightKind.None;
        DunkPressed = (f & NF.DunkPressed) != 0;
        SkillFlashOk = (f & NF.SkillFlashOk) != 0;
        _stunOnLand = (f & NF.StunOnLand) != 0;
        NetDunkReady = (f & NF.DunkReady) != 0;
        Stamina = s.Stamina; ShotCharge = s.ShotCharge; PassCharge = s.PassCharge; Stun = s.Stun; StealFail = s.StealFail; StealDash = s.StealDash;
        DribbleActive = s.DribbleActive; LandLock = s.LandLock; DribblePhase = s.DribblePhase; _releaseT = s.ReleaseT; _passReleaseT = s.PassReleaseT;
        FlightT = s.FlightT; FlightDur = s.FlightDur; DunkPressT = s.DunkPressT; SkillFlash = s.SkillFlash; DribbleRecharge = s.DribbleRecharge;
        DribbleCharges = s.DribbleCharges;
        _shotPlanValid = false;
        ApplyPose(dt, new Vector2(s.Vel.X, s.Vel.Z).Length());
    }

    /// <summary>Interpola dois estados (posição, rotação e cronômetros contínuos); o resto vem do mais novo.</summary>
    public static NetState Lerp(in NetState a, in NetState b, float k)
    {
        var s = b;
        s.Pos = a.Pos.Lerp(b.Pos, k);
        s.Vel = a.Vel.Lerp(b.Vel, k);
        s.Yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, k);
        s.DribblePhase = Mathf.Lerp(a.DribblePhase, b.DribblePhase, k);
        s.FlightT = Mathf.Lerp(a.FlightT, b.FlightT, k);
        return s;
    }

    /// <summary>Animação de soltar o arremesso (usada no lance livre automático).</summary>
    public void PlayReleaseAnim() => _releaseT = ReleaseAnimTime;

    public void SetNameVisible(bool v) { if (_nameLabel != null) _nameLabel.Visible = v; }
}
