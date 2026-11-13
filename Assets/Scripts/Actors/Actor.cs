using System.Collections.Generic;
using Daboiz.Combat;
using Daboiz.Core;
using Daboiz.Movement;
using Daboiz.Weapons;
using UnityEngine;

namespace Daboiz.Actors
{
    /// <summary>
    /// A body in the world (human or bot): health, knockback, status effects, movement and weapons.
    /// The same component is used for every player so bots and humans obey identical rules.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterMotor))]
    public class Actor : MonoBehaviour, IDamageable, IKnockbackable
    {
        public static readonly List<Actor> All = new List<Actor>();

        public Participant Participant;
        public bool IsLocalPlayer;
        public IActorController Controller;
        public CharacterMotor Motor;
        public WeaponHolder Weapons;
        public ActorVisual Visual;
        public readonly StatusEffects Status = new StatusEffects();

        public float MaxHealth = 100f;
        public float Health = 100f;
        public bool IsAlive { get; private set; }
        /// <summary>Permanently out of the current round (fell, voted out...). Not respawned.</summary>
        public bool Eliminated;
        /// <summary>Set by minigames to freeze movement (intros, meetings, turn-based waits).</summary>
        public bool MovementLocked;
        public float Yaw;
        public float Pitch;
        public ActorCommand LastCommand;
        public float DeathTime { get; private set; }
        public float LastDamageTime { get; private set; }
        public Actor LastAttacker { get; private set; }
        public int Kills;
        public int Deaths;

        public string DisplayName { get { return Participant != null ? Participant.Name : name; } }
        public Color Color { get { return Participant != null ? Participant.Color : Color.white; } }
        public bool IsBot { get { return !IsLocalPlayer; } }

        public Vector3 EyePosition { get { return transform.position + Vector3.up * Motor.EyeHeight; } }
        public Vector3 CenterPosition { get { return transform.position + Vector3.up * (Motor.EyeHeight * 0.6f); } }
        public Quaternion AimRotation { get { return Quaternion.Euler(Pitch, Yaw, 0f); } }
        public Vector3 AimDirection { get { return AimRotation * Vector3.forward; } }

        public System.Action<Actor, DamageInfo> Damaged;
        public System.Action<Actor, Actor> Died; // victim, killer

        private bool _interactLatch;

        /// <summary>True once per interact press (safe regardless of script update order).</summary>
        public bool ConsumeInteractPressed()
        {
            bool v = _interactLatch;
            _interactLatch = false;
            return v;
        }

        private Vector3 _ragdollVelocity;
        private bool _ragdollLaunch;
        private float _ragdollSpin;

        private void Awake()
        {
            Motor = GetComponent<CharacterMotor>();
            IsAlive = true;
            Health = MaxHealth;
        }

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!IsAlive)
            {
                TickRagdoll(dt);
                if (Visual != null) Visual.Tick(this, dt);
                return;
            }

            ActorCommand cmd = Controller != null ? Controller.GetCommand(this, dt) : ActorCommand.Idle(Yaw, Pitch);
            Yaw = cmd.Yaw;
            Pitch = Mathf.Clamp(cmd.Pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);

            Status.Tick(dt);
            GooPuddleCheck();

            if (MovementLocked || Status.Frozen)
            {
                cmd.Move = Vector2.zero;
                cmd.JumpHeld = false;
                cmd.JumpPressed = false;
                cmd.CrouchHeld = false;
            }
            if (Status.Frozen) { cmd.FireHeld = false; cmd.FirePressed = false; }
            if (Status.Reversed) cmd.Move = -cmd.Move;
            LastCommand = cmd;
            if (cmd.InteractPressed) _interactLatch = true;

            Motor.SpeedMultiplier = Status.SpeedMultiplier;
            Motor.GravityMultiplier = Status.GravityMultiplier;
            Motor.JumpMultiplier = Status.JumpMultiplier;
            if (Status.Frozen)
            {
                // Frozen: drift to a halt in place, still affected by gravity.
                Motor.Velocity.x *= 0.9f;
                Motor.Velocity.z *= 0.9f;
            }

            Motor.Simulate(new MotorInput
            {
                Move = cmd.Move,
                Yaw = Yaw,
                JumpHeld = cmd.JumpHeld,
                JumpPressed = cmd.JumpPressed,
                CrouchHeld = cmd.CrouchHeld
            }, dt);

            float targetScale = Status.ScaleTarget;
            float s = Mathf.MoveTowards(transform.localScale.y, targetScale, dt * 2f);
            if (!Mathf.Approximately(s, transform.localScale.y)) transform.localScale = new Vector3(s, s, s);

            if (Weapons != null) Weapons.Tick(cmd, dt);
            if (Visual != null) Visual.Tick(this, dt);
        }

        private void GooPuddleCheck()
        {
            if (!Motor.Grounded) return;
            if (Props.GooPuddle.IsInGoo(transform.position)) Status.Apply(StatusType.Gooed, 0.35f);
        }

        // ---------------------------------------------------------------- IDamageable / IKnockbackable

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || info.Amount <= 0f) return;
            if (Status.Shielded) return;
            Health -= info.Amount;
            LastDamageTime = Time.time;
            if (info.Attacker != null && info.Attacker != this) LastAttacker = info.Attacker;
            if (Damaged != null) Damaged(this, info);
            if (Health <= 0f) Die(info.Attacker != null ? info.Attacker : LastAttacker, false);
        }

        public void ApplyKnockback(Vector3 impulse, Vector3 point, Actor source)
        {
            if (!IsAlive) return;
            if (Status.Shielded && source != this) return;
            if (source != null && source != this) { LastAttacker = source; LastDamageTime = Time.time; }
            float scaleFactor = Mathf.Lerp(1.6f, 1f, Mathf.InverseLerp(0.5f, 1f, transform.localScale.y));
            Motor.AddImpulse(impulse * scaleFactor);
        }

        // ---------------------------------------------------------------- life cycle

        /// <summary>Kill this actor. launch = comedic yeet ragdoll (Red Light Green Light).</summary>
        public void Die(Actor killer, bool launch)
        {
            if (!IsAlive) return;
            IsAlive = false;
            Health = 0f;
            DeathTime = Time.time;
            Deaths++;
            if (killer != null && killer != this) killer.Kills++;
            Status.Clear();
            Motor.Controller.enabled = false;
            _ragdollLaunch = launch;
            _ragdollVelocity = launch
                ? new Vector3(Random.Range(-6f, 6f), Random.Range(16f, 22f), Random.Range(-10f, -4f))
                : Motor.Velocity * 0.5f;
            _ragdollSpin = launch ? Random.Range(540f, 900f) : 0f;
            if (Visual != null) Visual.SetDead(true);
            if (Died != null) Died(this, killer);
            Minigames.MinigameBase mg = GameFlow.Instance != null ? GameFlow.Instance.Current : null;
            if (mg != null) mg.HandleActorDied(this, killer);
        }

        private void TickRagdoll(float dt)
        {
            float t = Time.time - DeathTime;
            if (t > 6f) return;
            if (_ragdollLaunch || t < 1.2f)
            {
                _ragdollVelocity.y -= 20f * dt;
                Vector3 next = transform.position + _ragdollVelocity * dt;
                if (!_ragdollLaunch)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out hit,
                            0.3f + Mathf.Max(0f, -_ragdollVelocity.y * dt), ~0, QueryTriggerInteraction.Ignore)
                        && !hit.collider.transform.IsChildOf(transform))
                    {
                        next.y = hit.point.y;
                        _ragdollVelocity = Vector3.zero;
                    }
                }
                transform.position = next;
                if (_ragdollSpin > 0f) transform.Rotate(_ragdollSpin * dt * 0.6f, _ragdollSpin * dt, 0f, Space.World);
            }
        }

        public void Respawn(Vector3 position, float yaw)
        {
            IsAlive = true;
            Eliminated = false;
            Health = MaxHealth;
            Status.Clear();
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Motor.Controller.enabled = true;
            Motor.ForceStand();
            Motor.Teleport(position);
            Yaw = yaw;
            Pitch = 0f;
            _ragdollLaunch = false;
            if (Visual != null) { Visual.SetDead(false); Visual.SetHidden(false); }
            if (Weapons != null) Weapons.RefillAll();
            BotSyncView();
        }

        /// <summary>Instantly move (keeps the actor alive).</summary>
        public void TeleportTo(Vector3 position, float yaw, bool resetVelocity = true)
        {
            Motor.Teleport(position, resetVelocity);
            Yaw = yaw;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            BotSyncView();
        }

        private void BotSyncView()
        {
            Player.PlayerInputController pic = Controller as Player.PlayerInputController;
            if (pic != null) pic.SetView(Yaw, Pitch);
            AI.BotBrain bb = Controller as AI.BotBrain;
            if (bb != null) bb.SetView(Yaw, Pitch);
        }

        /// <summary>Remove from play for the rest of the round.</summary>
        public void Eliminate(bool comedicLaunch)
        {
            Eliminated = true;
            if (IsAlive) Die(null, comedicLaunch);
        }
    }
}
