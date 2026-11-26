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
    /// <summary>v0.23: personagem (ver <see cref="Characters"/>); definido antes de entrar na árvore.</summary>
    public int CharacterId;
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
    /// <summary>Host: tempo extra esperando o aperto de um jogador remoto antes de dar o skill check como perdido.</summary>
    public float SkillGrace;
    public float DunkPressT;
    public float SkillFlash;          // >0: mostra o resultado do skill check (HUD)
    public bool SkillFlashOk;
    /// <summary>v0.27: enterrada começada perto de um King com a Presença Implacável: sem skill check e erra com certeza.</summary>
    public bool DunkJinx;
    Vector3 _flightFrom, _flightTo;
    bool _stunOnLand;
    /// <summary>v0.21: pendurado no aro depois de uma enterrada certa (s restantes).</summary>
    public float RimHang;
    Vector3 _hangPos;
    /// <summary>v0.21: expressão escolhida na roda (0 = nenhuma; ver <see cref="Expressions"/>) e quanto tempo falta.</summary>
    public byte Emote;
    public float EmoteT;
    float _celebrateT, _angryT;
    int _lastTeamScore = -1, _lastOppScore = -1;

    // prévia do arremesso = exatamente o que vai sair (calculada no fim de cada tick)
    ShotMath.ShotPlan _shotPlan;
    bool _shotPlanValid;

    float _regenDelay;
    int _lastBounceIdx;
    float _airTime;
    Vector3 _jumpVel;
    Node3D _visual;
    IPlayerVisual _rig;
    Label3D _nameLabel;
    Node3D _stars;                    // estrelinhas girando = atordoado
    const float ReleaseAnimTime = 0.45f;
    float _releaseT, _passReleaseT;
    float _passBuffer; // aperto de passe guardado (Tuning.PassBufferTime)

    // ---- v0.26: personagens com atributos e habilidades ----
    /// <summary>Atributos de estrela já aplicados (velocidade, estamina...). Atualizado a cada tick.</summary>
    public PlayerStats St = PlayerStats.Neutral;
    public PlayerAbilities Ab;
    // movimento roteirizado curto (Drible Relâmpago do Bolt): ignora a física até chegar
    Vector3 _moveFrom, _moveTo; float _moveT, _moveDur, _moveBulge;
    public bool InScriptedMove => _moveDur > 0;

    public bool HasBall => M?.Ball?.Holder == this;
    public bool IsDribbling => DribbleActive > 0;
    /// <summary>v0.37: na Investida o Iron não pega a bola.</summary>
    public bool CanCatch => Ab == null || (!Ab.Charging && !Ab.Phased);
    /// <summary>v0.41: no Realm do One (sozinho ou abduzido): atravessa os jogadores e não interage com eles nem com a bola.</summary>
    public bool Phased => Ab != null && Ab.Phased;
    /// <summary>Pode ter a bola roubada? (não se estiver driblando, no ar ou carregando arremesso; v0.37: nem o Iron que pegou a bola no bloqueio)</summary>
    public bool CanBeStolen => !IsDribbling && !Airborne && !ChargingShot && !ChargingPass && Flight == FlightKind.None && (Ab == null || !Ab.BlockCatch);
    public Vector3 Forward => ShotMath.Dir(Yaw);
    public Vector3 Right => new(Mathf.Cos(Yaw), 0, -Mathf.Sin(Yaw));

    public override void _Ready()
    {
        Ab ??= new PlayerAbilities(this);
        UpdateStats();
        CollisionLayer = 2;
        CollisionMask = 1 | 2;
        FloorSnapLength = 0.2f;
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.8f }, Position = new Vector3(0, 0.9f, 0) });
        _rig = CharacterVisual.Available ? new CharacterVisual(this) : new PlayerRig(this, Team);
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
        _nameWanted = !IsLocalHuman;
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
        Stamina = St.StaminaMax;
        DribbleCharges = St.DribbleCharges;
        AddChild(new AbilityAura { P = this, Name = "AbilityAura" });
    }

    /// <summary>Atributos de estrela (+ bônus do modo apostador do Luck). Testes antigos jogam com tudo neutro.</summary>
    public void UpdateStats()
    {
        St = PlayerStats.For(CharacterId, Ab?.ModeBonus ?? 0f, M == null || M.UseStars);
        // v0.41: Maldição do One (atributos pela metade) e Domínio das Sombras de um One adversário (−20%)
        if (Ab != null)
        {
            float k = 1f;
            if (Ab.CurseT > 0) k *= Mathf.Clamp(Tuning.I.OneCurseStatMult, 0.05f, 1f);
            if (M != null && M.DomainAgainst(this) != null) k *= Mathf.Clamp(Tuning.I.OneUltEnemyStatMult, 0.05f, 1f);
            if (k < 0.999f) St = St.Scaled(k);
            if (Ab.RealmSpeed) St.Speed *= 1f + Mathf.Max(0f, Tuning.I.OneRealmSpeedBonus);
        }
        // v0.37: Iron Indestrutível corre como alguém de IronUltSpeedStars estrelas (se as dele forem menos)
        if (Ab != null && Ab.Indestructible)
            St.Speed = Mathf.Max(St.Speed, PlayerStats.Mult(Mathf.Clamp(Mathf.RoundToInt(Tuning.I.IronUltSpeedStars), 1, 5), Tuning.I.StarSpeedStep));
    }

    /// <summary>v0.27 (modo de teste): troca o personagem na hora — refaz o visual. Recargas e ultimate continuam.</summary>
    public void SetCharacter(int id)
    {
        id = Characters.Clamp(id);
        if (id == CharacterId) return;
        CharacterId = id;
        if (Ab != null) { Ab.OnRoundReset(); Ab.EyeT = 0; Ab.PresenceT = 0; Ab.ModeT = 0; Ab.ModeBonus = 0; Ab.BetT = 0; Ab.BetTarget = 0; Ab.UltT = 0; Ab.WallT = 0; }
        if (_rig is CharacterVisual cv) cv.Remove(); else _visual?.QueueFree();
        _rig = CharacterVisual.Available ? new CharacterVisual(this) : new PlayerRig(this, Team);
        _visual = _rig.Root;
        _visual.Rotation = new Vector3(0, Yaw, 0);
        UpdateStats();
        Stamina = Mathf.Min(Stamina, St.StaminaMax);
    }

    /// <summary>v0.27: nome mostrado em cima do boneco e no menu de escolher alvo.</summary>
    public void SetDisplayName(string name)
    {
        PlayerName = name;
        if (_nameLabel != null) _nameLabel.Text = name;
    }

    /// <summary>v0.27: destaque do anel do chão (alvo do Swap, só na tela do Bolt).</summary>
    public void SetRingHighlight(float k) { if (_rig is CharacterVisual cv) cv.SetRingHighlight(k); }

    // =====================================================================
    /// <summary>Simulação autoritativa de um tick.</summary>
    public void ServerTick(PlayerInput inp, float dt)
    {
        var t = Tuning.I;
        // v0.41: abduzido pelo One = só anda e corre (nem habilidades)
        if (Ab.AbductT > 0) { inp.Held &= Btn.Sprint; inp.Pressed = Btn.None; inp.Target = 0; }
        LastInput = inp;
        UpdateStats();
        TickTimers(dt);
        if (SkillFlash > 0) SkillFlash -= dt;
        // guarda o aperto do passe por um instante (sai assim que der: pegando a bola, pousando, fim do drible...)
        if (inp.WasPressed(Btn.Pass)) _passBuffer = Tuning.I.PassBufferTime;
        else if (_passBuffer > 0) _passBuffer -= dt;
        // expressões (v0.21): valem a qualquer momento
        if (inp.Emote > 0 && inp.Emote <= Expressions.Count) { Emote = inp.Emote; EmoteT = t.EmoteDuration; }
        else if (EmoteT > 0) { EmoteT -= dt; if (EmoteT <= 0) Emote = 0; }
        Ab.Tick(inp, dt);
        // v0.41: no Realm, pular ou tentar roubar tira o One de lá na hora (e a ação acontece normalmente)
        if (Ab.RealmT > 0 && Ab.AbductT <= 0 && (inp.WasPressed(Btn.Jump) || inp.WasPressed(Btn.Steal))) Ab.ExitRealm();
        // atravessa os jogadores: Investida do Iron (v0.37) ou no Realm (v0.41)
        if (_unghostTicks > 0) _unghostTicks--;
        bool ghost = Ab.Charging || Ab.Phased || _unghostTicks > 0; // saiu do Realm: espera a física ver a posição nova
        CollisionLayer = ghost ? 0u : 2u;
        CollisionMask = ghost ? 1u : 3u;

        if (RimHang > 0)
        {
            // pendurado no aro: parado lá em cima até soltar (depois cai normalmente)
            RimHang -= dt;
            GlobalPosition = GlobalPosition.Lerp(_hangPos, 1f - Mathf.Exp(-22f * dt));
            Velocity = Vector3.Zero;
            if (RimHang <= 0) { RimHang = 0; Airborne = true; _airTime = 0; _jumpVel = Vector3.Zero; }
            ApplyPose(dt, 0);
            return;
        }

        if (Flight != FlightKind.None)
        {
            FlightTick(inp, dt);
            ApplyPose(dt, 0);
            return;
        }

        if (_moveDur > 0)
        {
            ScriptedMoveTick(dt);
            ApplyPose(dt, 0);
            return;
        }

        if (Ab.Charging && (Stun > 0 || !M.CanMove || HasBall)) Ab.EndCharge();
        bool canMove = M.CanMove && LandLock <= 0 && Stun <= 0 && Ab.TiredT <= 0;
        bool canAct = M.CanAct && LandLock <= 0 && !Airborne && Stun <= 0 && Ab.TiredT <= 0 && !Ab.Charging;

        var wish = canMove ? new Vector3(inp.Move.X, 0, inp.Move.Y) : Vector3.Zero;
        if (wish.LengthSquared() > 1f) wish = wish.Normalized();
        bool moving = wish.LengthSquared() > 0.01f;

        // ---------- ações ----------
        if (HasBall)
        {
            StealDash = 0;
            if (Airborne && ChargingShot && M.CanAct && Stun <= 0)
            {
                // arremesso no pulo (v0.10): solta no ar quando quiser; se tocar o chão carregando, arremessa sozinho
                ChargingJump = false;
                HandleShot(inp, dt);
            }
            else if (canAct && !IsDribbling)
            {
                if (!ChargingShot && !ChargingPass && inp.WasPressed(Btn.Dribble) && DribbleCharges > 0) StartDribble();
                else if (!ChargingShot && !ChargingPass && WantsDunk(inp) && CanDunk()) StartDunk();
                else
                {
                    if (!ChargingPass) HandleShot(inp, dt);
                    if (!ChargingShot) HandlePass(inp, dt);
                    // carregando o arremesso dá para pular (qualquer direção)
                    if (ChargingShot) HandleJump(inp, dt, wish);
                    else ChargingJump = false;
                }
            }
            else { CancelShot(false); CancelPass(); ChargingJump = false; }
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
        bool wantRun = inp.IsHeld(Btn.Sprint) && moving && !ChargingShot && !IsDribbling && StealFail <= 0 && !Airborne && Ab.DashT <= 0 && !Ab.Charging;
        Running = wantRun && Stamina > 0 && (Running || Stamina >= t.StaminaMinToRun);
        if (Running)
        {
            Stamina = Mathf.Max(0, Stamina - t.StaminaDrainPerSec * dt);
            _regenDelay = t.StaminaRegenDelay;
            if (Stamina <= 0) Running = false;
        }
        else if (_regenDelay > 0) _regenDelay -= dt;
        else Stamina = Mathf.Min(St.StaminaMax, Stamina + St.StaminaRegen * dt);
        if (Stamina > St.StaminaMax || Ab.Indestructible) Stamina = St.StaminaMax; // v0.37: Indestrutível = estamina infinita

        // ---------- movimento ----------
        float speed = Running ? St.RunSpeed : St.WalkSpeed;
        if (HasBall) speed *= t.CarrierSpeedMult;
        if (ChargingShot) speed *= t.ShotChargeMoveMult;
        if (ChargingPass) speed *= t.PassChargeMoveMult;
        if (IsDribbling) speed = St.WalkSpeed * St.DribbleSpeedMult; // driblando: fração da velocidade andando
        if (StealFail > 0) speed *= t.StealFailSpeedMult;
        if (IsDummy) speed *= t.DummySpeedMult;
        if (ChargingJump) speed = 0;

        var v = Velocity;
        Vector3 hv;
        if (Airborne) hv = new Vector3(_jumpVel.X, 0, _jumpVel.Z);
        else if (Ab.DashT > 0) hv = Forward * t.LuckDashSpeed;
        else if (Ab.Charging)
        {
            // v0.37: Investida do Iron — segue para a frente acelerando. v0.38: vira seguindo a câmera (até IronChargeTurnRate);
            // os botões de movimento não fazem nada
            Ab.ChargeT += dt;
            Yaw = Mathf.RotateToward(Yaw, IsDummy ? Yaw : inp.AimYaw, Mathf.DegToRad(t.IronChargeTurnRate) * dt); // boneco de teste: reto
            hv = Forward * Ab.ChargeSpeed;
            Ab.ChargeHits();
        }
        else if (StealDash > 0) hv = Forward * St.StealDashSpeed;
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
        bool onPlayer = SlideOffPlayers();
        SanitizeState();
        if (Ab.Charging)
        {
            // bateu numa parede (colisão ou limite da quadra) = atordoado; acabou o tempo = cansado
            var gp = GlobalPosition;
            bool edge = gp.X <= M.Geo.MinX + 0.45f || gp.X >= M.Geo.MaxX - 0.45f || gp.Z <= M.Geo.MinZ + 0.45f || gp.Z >= M.Geo.MaxZ - 0.45f;
            bool wallHit = false;
            for (int i = 0; i < GetSlideCollisionCount(); i++)
            {
                var col = GetSlideCollision(i);
                if (col.GetCollider() is not Player && Mathf.Abs(col.GetNormal().Y) < 0.5f && col.GetNormal().Dot(Forward) < -0.5f) wallHit = true;
            }
            if (wallHit || edge) { Ab.EndCharge(true); Velocity = Vector3.Zero; }
            else if (Ab.ChargeT >= t.IronChargeDuration) { Ab.EndCharge(false); Velocity = Vector3.Zero; }
        }

        if (Airborne)
        {
            _airTime += dt;
            if (_airTime > 0.08f && IsOnFloor() && !onPlayer)
            {
                Airborne = false;
                // pulou carregando e não soltou: arremessa ao tocar o chão (v0.39: se ainda não deu o tempo mínimo mirando,
                // continua mirando no chão, sem o travamento do pouso, e sai quando completar)
                if (HasBall && ChargingShot && ShotCharge >= t.ShotMinChargeTime) ReleaseShot();
                if (_stunOnLand) { _stunOnLand = false; Stun = t.DunkStunTime; }
                else LandLock = HasBall && ChargingShot ? 0f : St.LandLockTime;
                Ab.SkyJump = false;
                _jumpVel = Vector3.Zero;
                M.OnLanded(this);
            }
        }

        // ---------- roubo: acertou alguém? ----------
        if (StealDash > 0) CheckStealHit();
        if (Ab.DashT > 0) Ab.CheckDashSteal();

        // ---------- rotação ----------
        float targetYaw = Yaw;
        if (ChargingShot || ChargingPass || ChargingJump) targetYaw = inp.AimYaw;
        else if (moving && !Airborne && StealDash <= 0 && Ab.DashT <= 0 && LandLock <= 0 && !Ab.Charging && Ab.TiredT <= 0) targetYaw = ShotMath.YawOf(wish);
        Yaw = Mathf.LerpAngle(Yaw, targetYaw, 1f - Mathf.Exp(-t.TurnSpeed * dt));

        UpdateDribble(dt);
        _shotPlanValid = ChargingShot;
        if (ChargingShot) _shotPlan = PlanShot(inp.AimYaw, inp.AimPitch);
        ApplyPose(dt, new Vector2(Velocity.X, Velocity.Z).Length());
    }

    /// <summary>
    /// Ninguém fica em cima de outro jogador (v0.9): pisou na cabeça/ombro de alguém → escorrega para o lado e continua caindo.
    /// </summary>
    bool SlideOffPlayers()
    {
        bool on = false;
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var c = GetSlideCollision(i);
            if (c.GetCollider() is not Player o || c.GetNormal().Y < 0.3f) continue;
            on = true;
            var away = GlobalPosition - o.GlobalPosition; away.Y = 0;
            away = away.LengthSquared() > 1e-4f ? away.Normalized() : (Forward.LengthSquared() > 0.5f ? -Forward : Vector3.Right);
            GlobalPosition += away * 0.12f;
            Velocity = new Vector3(away.X * 3f, Mathf.Min(Velocity.Y, 0f), away.Z * 3f);
        }
        if (on && !Airborne) { Airborne = true; _airTime = 0.08f; _jumpVel = Vector3.Zero; }
        return on;
    }

    /// <summary>v0.41: saiu do Realm dentro de alguém → empurrado só o bastante para os corpos não se sobreporem.</summary>
    int _unghostTicks;
    public void PushOutOfPlayers()
    {
        if (M == null) return;
        _unghostTicks = 2;
        const float minD = 0.74f; // 2 × raio da cápsula + folga
        for (int pass = 0; pass < 3; pass++)
        {
            bool moved = false;
            foreach (var q in M.Players)
            {
                if (q == this || q.Phased) continue;
                var d = (GlobalPosition - q.GlobalPosition) with { Y = 0 };
                if (d.Length() >= minD || Mathf.Abs(GlobalPosition.Y - q.GlobalPosition.Y) > 1.7f) continue;
                var dir = d.LengthSquared() > 1e-4f ? d.Normalized() : -Forward;
                var to = q.GlobalPosition + dir * minD;
                GlobalPosition = M.Geo.ClampInside(to with { Y = GlobalPosition.Y }, 0.4f);
                moved = true;
            }
            if (!moved) break;
        }
    }

    /// <summary>Valor inválido (NaN/infinito) nunca chega na física: volta para um lugar seguro.</summary>
    void SanitizeState()
    {
        var p = GlobalPosition; var v = Velocity;
        if (float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z) && float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z)
            && float.IsFinite(Yaw) && p.Y > -5f && p.Y < 50f) return;
        GD.PushWarning($"[SAFE] {PlayerName}: posição/velocidade inválida ({p} / {v}) — reposicionado");
        GlobalPosition = HomePos with { Y = 0.05f };
        Velocity = Vector3.Zero;
        _jumpVel = Vector3.Zero;
        if (!float.IsFinite(Yaw)) Yaw = 0;
        Airborne = false;
        ResetPhysicsInterpolation();
    }

    /// <summary>Início de round (v0.9): estamina cheia e todos os cooldowns zerados.</summary>
    public void ResetForRound()
    {
        var t = Tuning.I;
        UpdateStats();
        Stamina = St.StaminaMax;
        _regenDelay = 0;
        DribbleCharges = St.DribbleCharges;
        Ab.OnRoundReset(); // v0.26: recargas das habilidades e a carga da ultimate NÃO voltam
        DribbleRecharge = 0;
        DribbleActive = 0;
        StealFail = 0;
        StealDash = 0;
        LandLock = 0;
        Stun = 0;
        _stunOnLand = false;
        RimHang = 0;
        BallLockoutTimer = 0;
        SkillFlash = 0;
        Running = false;
    }

    void TickTimers(float dt)
    {
        var t = Tuning.I;
        if (BallLockoutTimer > 0) BallLockoutTimer -= dt;
        if (LandLock > 0 && !Airborne) LandLock -= dt;
        if (Stun > 0 && !Airborne) Stun -= dt;
        if (StealFail > 0) StealFail -= dt;
        if (DribbleActive > 0) DribbleActive -= dt;
        int max = St.DribbleCharges;
        if (DribbleCharges > max) DribbleCharges = max;
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
            Ab.ArmEyeIfActive(); // Olho do Rei: gasto neste arremesso
        }
        if (!ChargingShot) return;
        ShotCharge += dt;
        // v0.39: só sai depois do tempo mínimo mirando (apertou e soltou rápido = continua mirando até completar)
        bool minOk = ShotCharge >= Tuning.I.ShotMinChargeTime;
        if ((!inp.IsHeld(Btn.Shoot) && minOk) || ShotCharge >= Tuning.I.ShotAutoReleaseTime) ReleaseShot();
    }

    /// <summary>Interrompe a carga sem arremessar (ex.: perdeu a bola, round reiniciou).</summary>
    public void CancelShot(bool feedback)
    {
        if (!ChargingShot) return;
        ChargingShot = false;
        ShotCharge = 0;
        Ab.EyeArmed = false;
    }

    /// <summary>Plano mostrado na prévia (calculado no fim do último tick). O arremesso usa exatamente este plano.</summary>
    public bool GetShotPlan(out ShotMath.ShotPlan plan)
    {
        plan = default;
        if (!ChargingShot) return false;
        if (!_shotPlanValid) _shotPlan = PlanShot(LastInput.AimYaw, LastInput.AimPitch);
        _shotPlanValid = true;
        plan = _shotPlan;
        return true;
    }

    /// <summary>Plano do arremesso com o alcance deste jogador (Precisão) e o Olho do Rei (alcance extra subindo a câmera).</summary>
    public ShotMath.ShotPlan PlanShot(float yaw, float pitch)
    {
        float range = St.ShotRangeMax;
        float ext = Ab.EyeArmed ? PlayerAbilities.EyeShotRange(this, range) : -1f;
        return ShotMath.Plan(GlobalPosition, Team, yaw, pitch, M.Geo, range, ext);
    }

    void ReleaseShot()
    {
        if (!GetShotPlan(out var plan)) return;
        ChargingShot = false;
        ShotCharge = 0;
        _shotPlanValid = false;
        BallLockoutTimer = Tuning.I.ShotPickupLockout;
        _releaseT = ReleaseAnimTime;
        // v0.26: Presença Implacável de um King adversário por perto -> erra com certeza
        bool jinx = M.PresenceJinx(this);
        if (jinx) plan = ShotMath.Jinx(plan, Team, M.Geo, Tuning.I.KingPresenceMissOffset, M.Rng.Randf() < 0.5f ? -1f : 1f);
        M.Ball.Throw(this, plan.Pos, plan.Vel, BallState.Shot);
        if (jinx) { M.Ball.NoScore = true; Ab.JinxFlash = Tuning.I.KingPresenceFlash; Sfx.Play3D(M, "fx_jinx", plan.Pos); M.LogEvent("JINX", this); }
        if (Ab.EyeArmed) { M.Ball.Aura = 1; Ab.EyeArmed = false; }
        M.LogEvent("SHOT", this);
    }

    /// <summary>v0.26 (Luck 1): arremesso na hora, direto na cesta adversária. missOffset &gt; 0 = erra (bate no aro e sai).</summary>
    public void ThrowDirectShot(float missOffset)
    {
        CancelShot(false); CancelPass(); DribbleActive = 0;
        var plan = ShotMath.Direct(GlobalPosition, Team, M.Geo, missOffset, M.Rng.Randf() < 0.5f ? -1f : 1f);
        Yaw = ShotMath.YawOf((M.Geo.RimCenter[CourtGeo.AttackHoop(Team)] - GlobalPosition) with { Y = 0 });
        BallLockoutTimer = Tuning.I.ShotPickupLockout;
        _releaseT = ReleaseAnimTime;
        M.Ball.Throw(this, plan.Pos, plan.Vel, BallState.Shot);
        if (missOffset > 0) M.Ball.NoScore = true;
        M.LogEvent("SHOT", this);
    }

    // =====================================================================
    // Passe

    void HandlePass(PlayerInput inp, float dt)
    {
        var t = Tuning.I;
        if (!ChargingPass && (inp.WasPressed(Btn.Pass) || _passBuffer > 0))
        {
            _passBuffer = 0;
            ChargingPass = true;
            PassCharge = 0;
            Ab.ArmEyeIfActive(); // Olho do Rei: gasto neste passe
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
            var target = Passing.Nearest(this, PassHigh, Ab.EyeMult);
            if (target == null) { Ab.EyeArmed = false; return; }
            var vel = Passing.Velocity(this, target, PassHigh, out var pos);
            Yaw = ShotMath.YawOf(target.GlobalPosition - GlobalPosition);
            ThrowPass(pos, vel, state, target);
        }
        else
        {
            // passe carregado: lança na direção da câmera, sem alvo e sem assistência
            var vel = Passing.AimedVelocity(inp.AimYaw, inp.AimPitch, PassHigh, St.AimPassSpeedMult * Mathf.Sqrt(Ab.EyeMult));
            Yaw = inp.AimYaw;
            ThrowPass(Passing.ReleasePos(this, GlobalPosition + ShotMath.Dir(inp.AimYaw)), vel, state, null);
        }
    }

    void ThrowPass(Vector3 pos, Vector3 vel, BallState state, Player target)
    {
        BallLockoutTimer = Tuning.I.PassLockout;
        _passReleaseT = 0.3f;
        M.Ball.Throw(this, pos, vel, state, target);
        if (Ab.EyeArmed) { M.Ball.Aura = 1; Ab.EyeArmed = false; }
        M.LogEvent(state == BallState.HighPass ? "HIGHPASS" : "PASS", this, target);
    }

    public void CancelPass()
    {
        if (ChargingPass && Ab != null) Ab.EyeArmed = false;
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
        float chargeTime = St.JumpChargeTime;
        JumpCharge = Mathf.Min(JumpCharge + dt, chargeTime);
        if (wish.LengthSquared() > 0.01f) { _jumpWish = wish; _jumpRun = inp.IsHeld(Btn.Sprint) && Stamina > 0; }
        if (inp.IsHeld(Btn.Jump)) return; // pula só ao soltar (segurando, fica carregado até soltar)

        // pula: direção vem do movimento antes de pular, quase sem controle no ar
        float k = chargeTime > 0 ? JumpCharge / chargeTime : 1f;
        float vy = Mathf.Lerp(St.JumpVelMin, St.JumpVelMax, k);
        float hs = (_jumpRun ? St.RunSpeed : St.WalkSpeed) * t.JumpCarry;
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

    /// <summary>
    /// Enterrada: pulo (v0.21: drible e roubo dividem o X no controle, então o X com a bola é drible). O roubo só conta
    /// se o jogador remapear roubo e drible para botões diferentes.
    /// </summary>
    static bool WantsDunk(PlayerInput inp) =>
        inp.WasPressed(Btn.Jump) || (inp.WasPressed(Btn.Steal) && !inp.WasPressed(Btn.Dribble));

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
        if (holder.Ab.PerfectDribble && holder.IsDribbling && !Characters.Immune(this, holder)) { holder.Ab.PunishStealer(this); return; } // v0.26: Drible Perfeito
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
        StealFail = St.StealFailTime;
        M.OnStealFailed(this, immuneTarget);
    }

    // =====================================================================
    // Drible especial

    /// <summary>Drible especial. free = não gasta carga (Drible Perfeito do King, com duração extra).</summary>
    public void StartDribble(bool free = false, float extra = 0f)
    {
        if (!free) DribbleCharges--;
        DribbleActive = Tuning.I.DribbleDuration + extra;
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
        if (Flight != FlightKind.None)
        {
            // v0.21: a bola sobe do peito, arma atrás da cabeça e desce por cima do aro (o fim bate com o alvo da enterrada)
            float k = Mathf.Clamp(FlightT / Mathf.Max(FlightDur, 0.01f), 0, 1), reach = t.DunkHandReach, up, fwd;
            if (k < 0.55f) { float a = Mathf.SmoothStep(0, 1, k / 0.55f); up = Mathf.Lerp(1.15f, reach + 0.05f, a); fwd = Mathf.Lerp(0.45f, -0.2f, a); }
            else if (k < 0.8f) { float a = (k - 0.55f) / 0.25f; up = reach + 0.05f + 0.1f * a; fwd = -0.2f - 0.05f * a; }
            else { float a = Mathf.SmoothStep(0, 1, (k - 0.8f) / 0.2f); up = Mathf.Lerp(reach + 0.15f, reach - 0.05f, a); fwd = Mathf.Lerp(-0.25f, 0.55f, a); }
            return GlobalPosition + Vector3.Up * up + Forward * fwd;
        }
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
            Ramming = Ab.Charging, Tired = Ab.TiredT > 0, Bonk = Ab.BonkT > 0 ? 1f - Ab.BonkT / 0.4f : 0f,
            Yaw = Yaw, Speed = Airborne || StealDash > 0 || Ab.DashT > 0 ? 0 : hspeed, HasBall = HasBall, Charging = ChargingShot, Charge = ShotCharge,
            Release = _releaseT / ReleaseAnimTime, PassCharging = ChargingPass, PassRelease = _passReleaseT / 0.3f,
            JumpCharging = ChargingJump, Airborne = Airborne, Dashing = StealDash > 0 || Ab.DashT > 0 || _moveDur > 0 || _netScripted, Slowed = StealFail > 0,
            Landing = LandLock > 0, BallSide = BallSide, Dribbling = IsDribbling,
            Dunking = Flight != FlightKind.None, Stunned = Stun > 0 || _stunOnLand,
            VelY = Velocity.Y, PassHigh = PassHigh, Hanging = RimHang > 0, Emote = Emote,
            FlightK = Flight != FlightKind.None ? Mathf.Clamp(FlightT / Mathf.Max(FlightDur, 0.01f), 0, 1) : 0,
        };
        if (M != null)
        {
            if (M.Geo != null) st.RimPos = M.Geo.RimCenter[CourtGeo.AttackHoop(Team)];
            var h = M.Ball?.Holder;
            st.Defending = h != null && h.Team != Team && !HasBall && (h.GlobalPosition - GlobalPosition).Length() < 3.2f;
            int sc = M.Score[Team];
            if (_lastTeamScore >= 0 && sc > _lastTeamScore) _celebrateT = 1.8f;
            _lastTeamScore = sc;
            if (_celebrateT > 0) _celebrateT -= dt;
            // v0.22: o adversário pontuou → fica bravo sozinho por um tempo
            int opp = M.Score[1 - Team];
            if (_lastOppScore >= 0 && opp > _lastOppScore) _angryT = Tuning.I.ConcedeAngryTime;
            _lastOppScore = opp;
            if (_angryT > 0) _angryT -= dt;
            st.Angry = _angryT > 0;
            bool ended = M.Phase == MatchPhase.Ended && M.Winner >= 0;
            st.Mood = (ended && M.Winner == Team) || (_celebrateT > 0 && !ended) ? 1 : ended ? 2 : 0;
        }
        if (st.HasBall) st.BallPos = HeldBallPosition();
        _stars.Visible = Stun > 0 && !_viewHidden;
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
        Flight = FlightKind.None; Stun = 0; _stunOnLand = false; SkillFlash = 0; _shotPlanValid = false; RimHang = 0;
        Running = false;
        _moveDur = 0;
        Ab?.OnRoundReset();
        _releaseT = 0; _passReleaseT = 0; _passBuffer = 0;
        _visual.Rotation = new Vector3(0, Yaw, 0);
        ResetPhysicsInterpolation();
    }

    // =====================================================================
    // v0.26: movimentos das habilidades

    /// <summary>Movimento curto roteirizado (Drible Relâmpago): de <paramref name="from"/> a <paramref name="to"/> em dur s,
    /// com uma curva para o lado (bulge, m) para passar ao lado do adversário.</summary>
    public void StartScriptedMove(Vector3 from, Vector3 to, float dur, float bulge)
    {
        CancelShot(false); CancelPass(); ChargingJump = false; StealDash = 0;
        _moveFrom = from; _moveTo = to; _moveT = 0; _moveDur = Mathf.Max(dur, 0.02f); _moveBulge = bulge; _moveKeepYaw = false;
        Velocity = Vector3.Zero;
    }

    void ScriptedMoveTick(float dt)
    {
        _moveT += dt;
        float k = Mathf.Clamp(_moveT / _moveDur, 0, 1);
        var d = (_moveTo - _moveFrom) with { Y = 0 };
        var side = d.LengthSquared() > 1e-4f ? new Vector3(-d.Z, 0, d.X).Normalized() : Vector3.Zero;
        float e = 1f - (1f - k) * (1f - k) * (1f - k); // v0.27: reto e rápido (relâmpago), freia só no fim
        var p = _moveFrom.Lerp(_moveTo, e) + side * (_moveBulge * Mathf.Sin(k * Mathf.Pi));
        GlobalPosition = p;
        Velocity = Vector3.Zero;
        if (d.LengthSquared() > 1e-4f && !_moveKeepYaw) Yaw = ShotMath.YawOf(d);
        if (k >= 1f) { _moveDur = 0; if (d.LengthSquared() > 1e-4f && !_moveKeepYaw) Velocity = d.Normalized() * St.RunSpeed * 0.6f; _moveKeepYaw = false; }
    }

    bool _moveKeepYaw;

    /// <summary>v0.37: atropelado pela Investida — escorrega para o lado (sem virar), já atordoado.</summary>
    public void StartKnockback(Vector3 to, float dur)
    {
        var from = GlobalPosition;
        StartScriptedMove(from, to with { Y = from.Y }, dur, 0f);
        _moveKeepYaw = true;
    }

    Vector3? _queuedReloc;

    /// <summary>Swap: a troca acontece no fim do tick (Match), senão os dois corpos se sobrepõem no meio da física.</summary>
    public void QueueRelocate(Vector3 pos) => _queuedReloc = pos;

    public void ApplyQueuedRelocate()
    {
        if (!_queuedReloc.HasValue) return;
        var p = _queuedReloc.Value;
        _queuedReloc = null;
        Relocate(p);
    }

    /// <summary>Teleporte que mantém o estado (Swap do Bolt). v0.31: quem estava carregando arremesso/passe continua
    /// carregando, só que no lugar novo (a prévia é recalculada no fim do tick).</summary>
    public void Relocate(Vector3 pos)
    {
        GlobalPosition = pos;
        Velocity = Vector3.Zero;
        _moveDur = 0;
        ChargingJump = false; StealDash = 0;
        _shotPlanValid = false;
        ResetPhysicsInterpolation();
    }

    /// <summary>Pulo reto para cima, sem carga (Arranha-Céu do Bolt).</summary>
    public void SkyJumpNow(float vy)
    {
        CancelShot(false); CancelPass(); ChargingJump = false; StealDash = 0;
        _jumpVel = new Vector3(0, vy, 0);
        Velocity = Vector3.Zero;
        Airborne = true;
        _airTime = 0;
        M.LogEvent("JUMP", this);
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
        if (to.Length() > St.DunkRange) return false;
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
        float dur = t.DunkFlightTime + M.Rng.RandfRange(-t.DunkFlightJitter, t.DunkFlightJitter);
        BeginFlight(FlightKind.Dunk, Mathf.Max(dur, 0.25f), rim - dir * 0.1f + Vector3.Up * 0.35f, hoop);
        M.LogEvent("DUNK", this);
        // v0.27: Presença Implacável também vale para enterradas: sem skill check, erra com certeza
        DunkJinx = M.PresenceJinx(this);
        if (DunkJinx) { Ab.JinxFlash = t.KingPresenceFlash; Sfx.Play3D(M, "fx_jinx", GlobalPosition + Vector3.Up * 2f); M.LogEvent("JINX", this); }
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
        if (DunkJinx)
        {
            // sem skill check: chega no aro e erra
            if (FlightT >= FlightDur) FinishDunk(false);
            return;
        }
        if (!DunkPressed && inp.WasPressed(Btn.Shoot))
        {
            DunkPressed = true;
            // remoto: vale onde o ponteiro estava na tela dele (não quando o aperto chegou)
            DunkPressT = inp.SkillT >= 0 ? Mathf.Min(inp.SkillT, FlightT) : FlightT;
        }
        if (DunkPressed)
        {
            bool inWindow = Mathf.Abs(DunkPressT - FlightDur) <= half;
            FinishDunk(inWindow); // errou (cedo ou tarde): erra NA HORA (v0.9) — o anel some, o jogador cai
            return;
        }
        else if (FlightT >= FlightDur + half + SkillGrace) FinishDunk(false); // não apertou a tempo (remoto: espera o atraso da rede)
    }

    void FinishDunk(bool ok)
    {
        var t = Tuning.I;
        var rim = M.Geo.RimCenter[FlightHoop];
        // resultado fica na tela com o ponteiro parado (v0.19): verde = certo, vermelho = errado
        if (Flight == FlightKind.Dunk && !DunkJinx) { SkillFlash = Mathf.Max(t.SkillResultHold, 0.001f); SkillFlashOk = ok; }
        DunkJinx = false;
        if (ok)
        {
            BallLockoutTimer = 0.8f;
            M.Ball.Throw(this, rim + Vector3.Up * 0.3f, new Vector3(0, -7f, 0), BallState.Shot);
            M.Ball.FromDunk = true; // v0.27: enterrada carrega mais a ultimate
            Sfx.Play3D(M, "rim", rim, 2, 0.8f);
            Sfx.Play3D(M, "board", rim, 0, 0.9f);
            Sfx.Play3D(M, "fx_dunk", rim);   // sem som: aro treme (HoopVisual) e efeito de impacto (ImpactFx), inclusive no cliente
            Sfx.Play3D(M, "slam", rim, 3);
            M.LogEvent("DUNK_OK", this);
            if (t.DunkHangTime > 0)
            {
                // v0.21: fica pendurado no aro (mãos na frente do aro, corpo embaixo) e depois cai
                EndFlight();
                var d = FlatToRim(FlightHoop); d = d.LengthSquared() > 1e-4f ? d.Normalized() : Forward;
                _hangPos = rim - d * (t.RimRadius + 0.22f) - Vector3.Up * (t.DunkHandReach - 0.12f);
                _hangPos.Y = Mathf.Max(_hangPos.Y, 0.05f);
                RimHang = t.DunkHangTime;
                return;
            }
        }
        else
        {
            // bola cai do lado da cesta; o jogador cai e fica atordoado
            var side = Right;
            BallLockoutTimer = 0.8f;
            // longe do aro: a bola escapa da mão; perto: cai do lado do aro (nunca dentro)
            if (FlightT < FlightDur * 0.7f) M.Ball.Drop(HeldBallPosition(), side * 1.6f + Forward * 0.5f + Vector3.Up * 0.5f);
            else M.Ball.Drop(rim + side * (t.RimRadius + 0.45f) + Vector3.Up * 0.1f, side * 1.4f + Vector3.Down * 1f);
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
        DunkJinx = false;
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
            ReleaseT, PassReleaseT, FlightT, FlightDur, DunkPressT, SkillFlash, DribbleRecharge, RimHang;
        public byte DribbleCharges, Emote, Char;
        public PlayerAbilities.NetData Ab;   // v0.26
    }

    [System.Flags]
    enum NF : ushort
    {
        Running = 1, ChargingShot = 2, ChargingPass = 4, PassHigh = 8, ChargingJump = 16, Airborne = 32, Dunk = 64,
        DunkPressed = 128, SkillFlashOk = 256, StunOnLand = 512, DunkReady = 1024, Scripted = 2048, DunkJinx = 4096,
    }

    /// <summary>Cliente: "pode enterrar" calculado no host (o boneco do cliente não tem física de chão).</summary>
    public bool NetDunkReady;
    bool _netScripted;
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
        if (_moveDur > 0) f |= NF.Scripted;
        if (DunkJinx) f |= NF.DunkJinx;
        return new NetState
        {
            Pos = GlobalPosition, Vel = Velocity, Yaw = Yaw, Flags = (ushort)f,
            Stamina = Stamina, ShotCharge = ShotCharge, PassCharge = PassCharge, Stun = Stun, StealFail = StealFail, StealDash = StealDash,
            DribbleActive = DribbleActive, LandLock = LandLock, DribblePhase = DribblePhase, ReleaseT = _releaseT, PassReleaseT = _passReleaseT,
            FlightT = FlightT, FlightDur = FlightDur, DunkPressT = DunkPressT, SkillFlash = SkillFlash, DribbleRecharge = DribbleRecharge,
            DribbleCharges = (byte)Mathf.Clamp(DribbleCharges, 0, 255), RimHang = RimHang, Emote = Emote, Ab = Ab.Capture(), Char = (byte)CharacterId,
        };
    }

    public static void WriteNet(NetWriter w, in NetState s)
    {
        w.V3(s.Pos); w.V3(s.Vel); w.F(s.Yaw); w.U16(s.Flags);
        w.F(s.Stamina); w.F(s.ShotCharge); w.F(s.PassCharge); w.F(s.Stun); w.F(s.StealFail); w.F(s.StealDash); w.F(s.DribbleActive);
        w.F(s.LandLock); w.F(s.DribblePhase); w.F(s.ReleaseT); w.F(s.PassReleaseT); w.F(s.FlightT); w.F(s.FlightDur); w.F(s.DunkPressT);
        w.F(s.SkillFlash); w.F(s.DribbleRecharge); w.U8(s.DribbleCharges); w.F(s.RimHang); w.U8(s.Emote); w.U8(s.Char);
        PlayerAbilities.Write(w, s.Ab);
    }

    public static NetState ReadNet(NetReader r) => new()
    {
        Pos = r.V3(), Vel = r.V3(), Yaw = r.F(), Flags = r.U16(),
        Stamina = r.F(), ShotCharge = r.F(), PassCharge = r.F(), Stun = r.F(), StealFail = r.F(), StealDash = r.F(), DribbleActive = r.F(),
        LandLock = r.F(), DribblePhase = r.F(), ReleaseT = r.F(), PassReleaseT = r.F(), FlightT = r.F(), FlightDur = r.F(), DunkPressT = r.F(),
        SkillFlash = r.F(), DribbleRecharge = r.F(), DribbleCharges = r.U8(), RimHang = r.F(), Emote = r.U8(), Char = r.U8(),
        Ab = PlayerAbilities.Read(r),
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
        DunkJinx = (f & NF.DunkJinx) != 0;
        if (s.Char != CharacterId) SetCharacter(s.Char); // v0.27: troca de personagem no modo de teste
        Stamina = s.Stamina; ShotCharge = s.ShotCharge; PassCharge = s.PassCharge; Stun = s.Stun; StealFail = s.StealFail; StealDash = s.StealDash;
        DribbleActive = s.DribbleActive; LandLock = s.LandLock; DribblePhase = s.DribblePhase; _releaseT = s.ReleaseT; _passReleaseT = s.PassReleaseT;
        FlightT = s.FlightT; FlightDur = s.FlightDur; DunkPressT = s.DunkPressT; SkillFlash = s.SkillFlash; DribbleRecharge = s.DribbleRecharge;
        DribbleCharges = s.DribbleCharges;
        RimHang = s.RimHang; Emote = s.Emote;
        Ab.Apply(s.Ab);
        _netScripted = (f & NF.Scripted) != 0;
        UpdateStats();
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

    /// <summary>Teste (--smoke-abilities, v0.27): começa uma enterrada se puder.</summary>
    public bool TestDunk() { if (!CanDunk()) return false; StartDunk(); return true; }

    /// <summary>Teste (--smoke-abilities): arremesso certinho na cesta adversária, pelo caminho normal (passa pela Presença).</summary>
    public void TestShot()
    {
        var rim = M.Geo.RimCenter[CourtGeo.AttackHoop(Team)];
        float yaw = ShotMath.YawOf((rim - GlobalPosition) with { Y = 0 });
        var rel = ShotMath.ReleasePos(GlobalPosition, yaw);
        float d = new Vector2(rim.X - rel.X, rim.Z - rel.Z).Length();
        LastInput = new PlayerInput { AimYaw = yaw, AimPitch = ShotMath.PitchForDistance(d, St.ShotRangeMax), SkillT = -1 };
        Yaw = yaw;
        ChargingShot = true;
        _shotPlanValid = false;
        ReleaseShot();
    }

    /// <summary>Teste: bote do roubo na direção de alguém.</summary>
    public void TestSteal(Player target)
    {
        Yaw = ShotMath.YawOf((target.GlobalPosition - GlobalPosition) with { Y = 0 });
        StealDash = Tuning.I.StealDashTime;
    }

    /// <summary>Animação de soltar o arremesso (usada no lance livre automático).</summary>
    public void PlayReleaseAnim() => _releaseT = ReleaseAnimTime;

    public void SetNameVisible(bool v) { _nameWanted = v; if (_nameLabel != null) _nameLabel.Visible = v && !_viewHidden; }
    bool _nameWanted = true;

    // ---- v0.41: como ESTA tela vê o jogador (Realm / Domínio das Sombras do One) ----
    float _viewAlpha = 1f;
    bool _viewHidden;
    readonly System.Collections.Generic.List<GeometryInstance3D> _viewGeo = new();
    Node _viewGeoRig;
    public bool ViewHidden => _viewHidden;

    /// <summary>1 = normal, entre 0 e 1 = transparente ("de outra dimensão"), 0 = invisível (nem sombra, nome ou efeitos).</summary>
    public void ApplyView(float alpha)
    {
        alpha = Mathf.Clamp(alpha, 0f, 1f);
        if (_visual != null && _viewGeoRig != _visual)
        {
            _viewGeoRig = _visual; _viewGeo.Clear(); _viewAlpha = -1f;
            foreach (var n in _visual.FindChildren("*", "GeometryInstance3D", true, false)) _viewGeo.Add((GeometryInstance3D)n);
            if (GetNodeOrNull<GeometryInstance3D>("TeamRing") is { } ring) _viewGeo.Add(ring);
        }
        if (Mathf.IsEqualApprox(alpha, _viewAlpha)) return;
        _viewAlpha = alpha;
        _viewHidden = alpha < 0.01f;
        if (_visual != null) _visual.Visible = !_viewHidden;
        if (GetNodeOrNull<Node3D>("TeamRing") is { } r) r.Visible = !_viewHidden;
        if (GetNodeOrNull<Node3D>("AbilityAura") is { } aura) aura.Visible = !_viewHidden;
        if (_nameLabel != null) { _nameLabel.Visible = _nameWanted && !_viewHidden; _nameLabel.Transparency = 1f - alpha; }
        if (_stars != null && _viewHidden) _stars.Visible = false;
        foreach (var g in _viewGeo) if (IsInstanceValid(g)) g.Transparency = _viewHidden ? 0f : 1f - alpha;
    }
}
