using Daboiz.Actors;
using Daboiz.Core;
using Daboiz.Weapons;
using UnityEngine;

namespace Daboiz.AI
{
    /// <summary>
    /// Simple bot controller. Each frame the active minigame fills in high level intents
    /// (MoveTarget, AimActor, WantFire...) via MinigameBase.DriveBot; the brain turns them into
    /// a human-like ActorCommand (turn-rate limited aim with noise, obstacle jumps, unstick logic).
    /// </summary>
    public class BotBrain : IActorController
    {
        public readonly Actor Self;
        public float Skill;

        // ---- Intents (reset every frame) ----
        public Vector3? MoveTarget;
        public float ArriveDistance;
        public Actor AimActor;
        public Vector3? AimPoint;
        public bool WantFire;
        public bool WantJump;
        public bool WantCrouch;
        public bool InteractHeld;
        public bool InteractPressed;
        public bool HoldStill;
        public Vector2? RawMove;
        /// <summary>World-space wish direction (independent of where the bot is looking).</summary>
        public Vector3? WorldMove;
        public float? DesiredYaw;
        public float StrafeWeave;
        public bool AllowBhop;
        public int WeaponSlot;

        // ---- Per-mode scratch space (persist across frames) ----
        public int Index;
        public float Timer;
        public float Timer2;
        public Vector3 Point;
        public object Blackboard;

        private float _yaw;
        private float _pitch;
        private readonly float _seedA;
        private readonly float _seedB;
        private readonly bool _likesToHop;
        private Vector3 _lastStuckCheckPos;
        private float _nextStuckCheck;
        private float _stuckTime;
        private float _unstickUntil;
        private float _unstickDir;
        private float _nextJumpAllowed;
        private float _nextTriggerToggle;
        private bool _triggerDown;
        private Actor _lastAimActor;
        private float _reactionReadyAt;

        public BotBrain(Actor self, float skill)
        {
            Self = self;
            Skill = Mathf.Clamp01(skill);
            _seedA = Random.Range(0f, 1000f);
            _seedB = Random.Range(0f, 1000f);
            _likesToHop = Random.value < 0.4f + skill * 0.4f;
        }

        public void SetView(float yaw, float pitch) { _yaw = yaw; _pitch = pitch; }

        private void ResetIntents()
        {
            MoveTarget = null;
            ArriveDistance = 0.8f;
            AimActor = null;
            AimPoint = null;
            WantFire = false;
            WantJump = false;
            WantCrouch = false;
            InteractHeld = false;
            InteractPressed = false;
            HoldStill = false;
            RawMove = null;
            WorldMove = null;
            DesiredYaw = null;
            StrafeWeave = 0f;
            AllowBhop = true;
            WeaponSlot = -1;
        }

        public ActorCommand GetCommand(Actor actor, float dt)
        {
            ResetIntents();
            Minigames.MinigameBase mg = GameFlow.Instance != null ? GameFlow.Instance.Current : null;
            if (mg != null && mg.State == Minigames.MinigameState.Playing) mg.DriveBot(this);
            else HoldStill = true;
            return Build(dt);
        }

        private ActorCommand Build(float dt)
        {
            ActorCommand c = ActorCommand.Idle(_yaw, _pitch);
            Vector3 pos = Self.transform.position;
            Vector3 eye = Self.EyePosition;

            // -------- view --------
            Vector3? lookAt = null;
            if (AimActor != null && AimActor.IsAlive)
            {
                float lead = Mathf.Lerp(0f, 0.15f, Skill);
                lookAt = AimActor.CenterPosition + AimActor.Motor.Velocity * lead;
            }
            else if (AimPoint.HasValue) lookAt = AimPoint.Value;

            float targetYaw = _yaw, targetPitch = 0f;
            if (lookAt.HasValue)
            {
                Vector3 d = lookAt.Value - eye;
                targetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                targetPitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            }
            else if (DesiredYaw.HasValue) targetYaw = DesiredYaw.Value;
            else if (MoveTarget.HasValue)
            {
                Vector3 d = MoveTarget.Value - pos;
                d.y = 0f;
                if (d.sqrMagnitude > 0.25f) targetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }

            if (lookAt.HasValue)
            {
                float noise = Mathf.Lerp(9f, 2f, Skill);
                float t = Time.time * 0.9f;
                targetYaw += (Mathf.PerlinNoise(_seedA, t) - 0.5f) * 2f * noise;
                targetPitch += (Mathf.PerlinNoise(_seedB, t) - 0.5f) * 2f * noise * 0.6f;
            }
            float turn = Mathf.Lerp(240f, 620f, Skill) * dt;
            _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, turn);
            _pitch = Mathf.MoveTowards(_pitch, Mathf.Clamp(targetPitch, -85f, 85f), turn);
            c.Yaw = _yaw;
            c.Pitch = _pitch;

            // -------- movement --------
            float distToTarget = 0f;
            if (!HoldStill)
            {
                if (RawMove.HasValue) c.Move = Vector2.ClampMagnitude(RawMove.Value, 1f);
                else if (WorldMove.HasValue)
                {
                    Vector3 wm = WorldMove.Value;
                    wm.y = 0f;
                    Vector3 local = Quaternion.Euler(0f, -_yaw, 0f) * wm;
                    c.Move = Vector2.ClampMagnitude(new Vector2(local.x, local.z), 1f);
                }
                else if (MoveTarget.HasValue)
                {
                    Vector3 to = MoveTarget.Value - pos;
                    to.y = 0f;
                    distToTarget = to.magnitude;
                    if (distToTarget > ArriveDistance)
                    {
                        Vector3 local = Quaternion.Euler(0f, -_yaw, 0f) * (to / distToTarget);
                        Vector2 mv = new Vector2(local.x, local.z);
                        if (StrafeWeave > 0f) mv += new Vector2(Mathf.Sin(Time.time * 2.3f + _seedA) * StrafeWeave, 0f);
                        c.Move = mv.sqrMagnitude > 0.0001f ? mv.normalized : Vector2.zero;
                    }
                }
                if (Time.time < _unstickUntil) c.Move = new Vector2(_unstickDir, -0.3f).normalized;
            }

            bool moving = c.Move.sqrMagnitude > 0.1f;
            bool jump = WantJump;

            // Stuck detection
            if (Time.time >= _nextStuckCheck)
            {
                _nextStuckCheck = Time.time + 0.5f;
                float moved = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(_lastStuckCheckPos.x, 0f, _lastStuckCheckPos.z));
                _stuckTime = moving && moved < 0.35f ? _stuckTime + 0.5f : 0f;
                _lastStuckCheckPos = pos;
                if (_stuckTime >= 1f)
                {
                    jump = true;
                    _unstickUntil = Time.time + 0.6f;
                    _unstickDir = Random.value < 0.5f ? -1f : 1f;
                    _stuckTime = 0f;
                }
            }

            // Obstacle ahead -> hop
            if (moving && Self.Motor.Grounded)
            {
                Vector3 worldMove = Quaternion.Euler(0f, _yaw, 0f) * new Vector3(c.Move.x, 0f, c.Move.y);
                RaycastHit hit;
                if (PhysicsUtil.RaycastIgnoring(pos + Vector3.up * 0.5f, worldMove.normalized, 1.1f, Self.transform, out hit)
                    && hit.normal.y < 0.5f
                    && !PhysicsUtil.RaycastIgnoring(pos + Vector3.up * 1.7f, worldMove.normalized, 1.3f, Self.transform, out hit))
                    jump = true;
            }

            if (jump && Time.time >= _nextJumpAllowed)
            {
                c.JumpPressed = true;
                c.JumpHeld = true;
                _nextJumpAllowed = Time.time + 0.35f;
            }
            if (AllowBhop && _likesToHop && moving && distToTarget > 10f && !HoldStill) c.JumpHeld = true;
            c.CrouchHeld = WantCrouch;

            // -------- shooting --------
            if (AimActor != _lastAimActor)
            {
                _lastAimActor = AimActor;
                _reactionReadyAt = Time.time + Mathf.Lerp(0.6f, 0.2f, Skill) * Random.Range(0.7f, 1.3f);
            }
            WeaponInstance w = Self.Weapons != null ? Self.Weapons.Current : null;
            if (WantFire && lookAt.HasValue && w != null && Time.time >= _reactionReadyAt)
            {
                Vector3 aimDir = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;
                Vector3 toTarget = lookAt.Value - eye;
                float tolerance = Mathf.Lerp(16f, 7f, Skill) + Mathf.Clamp(6f - toTarget.magnitude, 0f, 6f) * 3f;
                bool onTarget = Vector3.Angle(aimDir, toTarget) < tolerance;
                WeaponDefinition d = w.Def;
                if (d.Mode == FireMode.Charge)
                {
                    bool holding = Self.Weapons.Charging;
                    c.FireHeld = onTarget && !(holding && Self.Weapons.ChargeAmount >= 0.75f + Skill * 0.2f);
                    if (holding && !onTarget) c.FireHeld = true; // keep charging while re-aiming
                }
                else if (onTarget)
                {
                    if (d.Automatic || d.Mode == FireMode.Beam) c.FireHeld = true;
                    else if (Time.time >= _nextTriggerToggle)
                    {
                        _triggerDown = !_triggerDown;
                        _nextTriggerToggle = Time.time + Random.Range(0.08f, 0.3f);
                        c.FirePressed = _triggerDown;
                        c.FireHeld = _triggerDown;
                    }
                }
            }
            else if (Self.Weapons != null && Self.Weapons.Charging)
            {
                c.FireHeld = false; // release a held charge
            }

            c.InteractHeld = InteractHeld;
            c.InteractPressed = InteractPressed;
            c.WeaponSlot = WeaponSlot;
            return c;
        }

        // ---------------------------------------------------------------- helpers for minigames

        public static Actor NearestEnemy(Actor self, float maxDist, bool requireSight)
        {
            Actor best = null;
            float bestD = maxDist;
            for (int i = 0; i < Actor.All.Count; i++)
            {
                Actor a = Actor.All[i];
                if (a == self || !a.IsAlive || a.Eliminated) continue;
                float d = Vector3.Distance(self.transform.position, a.transform.position);
                if (d >= bestD) continue;
                if (requireSight && !CanSee(self, a)) continue;
                best = a;
                bestD = d;
            }
            return best;
        }

        public static bool CanSee(Actor a, Actor b)
        {
            return PhysicsUtil.LineOfSight(a.EyePosition, b.CenterPosition, a.transform, b.transform);
        }

        /// <summary>Default free-for-all behaviour: chase + strafe + shoot the nearest visible enemy.</summary>
        public void DefaultCombat(float preferredRange = 9f)
        {
            Actor target = NearestEnemy(Self, 70f, true) ?? NearestEnemy(Self, 200f, false);
            if (target == null) return;
            bool visible = CanSee(Self, target);
            AimActor = visible ? target : null;
            WantFire = visible;
            Vector3 to = target.transform.position - Self.transform.position;
            float dist = to.magnitude;
            Vector3 side = Vector3.Cross(Vector3.up, to.normalized) * (Mathf.Sin(Time.time * 0.7f + _seedA) * 5f);
            Vector3 desired = dist > preferredRange
                ? target.transform.position + side
                : Self.transform.position - to.normalized * 2f + side;
            MoveTarget = desired;
            if (!visible) AimPoint = null;
            StrafeWeave = visible ? 0.6f : 0f;
        }
    }
}
