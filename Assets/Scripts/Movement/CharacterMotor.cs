using UnityEngine;

namespace Daboiz.Movement
{
    /// <summary>
    /// Kinematic Quake/Source style movement on top of CharacterController:
    /// ground friction + acceleration, air acceleration with strafe gain (bhop / air-strafe),
    /// auto-bhop, jump buffering, coyote time, crouch-slide with momentum, and surfing on
    /// surfaces steeper than <see cref="MovementSettings.MaxGroundAngle"/>.
    /// It knows nothing about input devices: feed it <see cref="MotorInput"/> via Simulate().
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMotor : MonoBehaviour
    {
        public MovementSettings Settings = new MovementSettings();

        public Vector3 Velocity;
        public bool Grounded { get; private set; }
        public bool Crouching { get; private set; }
        public bool Sliding { get; private set; }
        public bool Surfing { get; private set; }
        public Vector3 GroundNormal { get; private set; }
        public float HorizontalSpeed { get { return new Vector3(Velocity.x, 0f, Velocity.z).magnitude; } }

        /// <summary>Set every frame by status effects.</summary>
        public float SpeedMultiplier = 1f;
        public float GravityMultiplier = 1f;
        public float JumpMultiplier = 1f;

        /// <summary>Eye height above feet (scaled), smoothed when crouching.</summary>
        public float EyeHeight { get { return _eyeHeight * transform.lossyScale.y; } }

        public System.Action OnJumped;
        public System.Action<float> OnLanded; // impact speed

        private CharacterController _cc;
        private float _lastGroundedTime = -10f;
        private float _lastJumpPressedTime = -10f;
        private float _lastJumpTime = -10f;
        private float _ungroundUntil;
        private float _slideBoostReadyAt;
        private bool _jumpedSinceGrounded;
        private float _eyeHeight = 1.6f;

        // Collected during CharacterController.Move via OnControllerColliderHit
        private bool _hitGround;
        private bool _hitSteep;
        private Vector3 _bestGroundNormal;

        public CharacterController Controller { get { return _cc; } }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            ApplyHeight(Settings.StandHeight);
            _eyeHeight = Settings.StandHeight - 0.2f;
            GroundNormal = Vector3.up;
        }

        public void Simulate(MotorInput input, float dt)
        {
            if (dt <= 0f || _cc == null || !_cc.enabled) return;
            MovementSettings s = Settings;
            float now = Time.time;

            if (input.JumpPressed) _lastJumpPressedTime = now;

            UpdateCrouch(input.CrouchHeld);

            Quaternion yawRot = Quaternion.Euler(0f, input.Yaw, 0f);
            Vector3 wishDir = yawRot * new Vector3(input.Move.x, 0f, input.Move.y);
            float wishMag = Mathf.Clamp01(wishDir.magnitude);
            if (wishMag > 0.0001f) wishDir /= wishDir.magnitude; else wishDir = Vector3.zero;
            float maxSpeed = (Crouching && !Sliding ? s.CrouchSpeed : s.MaxGroundSpeed) * SpeedMultiplier;
            float wishSpeed = wishMag * maxSpeed;

            // ---- Jump (checked before friction so perfectly timed hops keep all speed) ----
            bool wantJump = (now - _lastJumpPressedTime <= s.JumpBuffer) || (s.AutoBhop && input.JumpHeld);
            bool coyote = !Grounded && !_jumpedSinceGrounded && (now - _lastGroundedTime <= s.CoyoteTime);
            if (wantJump && (Grounded || coyote) && now - _lastJumpTime > 0.12f && JumpMultiplier > 0f)
                DoJump();

            if (Grounded)
            {
                if (Sliding)
                {
                    ApplyFriction(s.SlideFriction, dt);
                    Accelerate(wishDir, wishSpeed, s.SlideAccel, dt);
                    // Sliding downhill builds speed.
                    Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, GroundNormal);
                    Velocity += downhill * (s.Gravity * s.SlideGravityAssist * dt);
                }
                else
                {
                    ApplyFriction(s.GroundFriction, dt);
                    Accelerate(wishDir, wishSpeed, s.GroundAccel, dt);
                }
                if (Velocity.y < 0f) Velocity.y = 0f;
            }
            else
            {
                AirAccelerate(wishDir, wishSpeed, s.AirAccel, dt);
                Velocity.y -= s.Gravity * GravityMultiplier * dt;
                if (Velocity.y < -s.MaxFallSpeed) Velocity.y = -s.MaxFallSpeed;
            }

            Vector3 h = new Vector3(Velocity.x, 0f, Velocity.z);
            if (h.magnitude > s.MaxHorizontalSpeed)
            {
                h = h.normalized * s.MaxHorizontalSpeed;
                Velocity.x = h.x; Velocity.z = h.z;
            }

            Move(dt);

            float targetEye = (Crouching ? s.CrouchHeight : s.StandHeight) - 0.2f;
            _eyeHeight = Mathf.MoveTowards(_eyeHeight, targetEye, dt * 6f);
        }

        // ------------------------------------------------------------------ physics steps

        private void ApplyFriction(float friction, float dt)
        {
            Vector3 hv = new Vector3(Velocity.x, 0f, Velocity.z);
            float speed = hv.magnitude;
            if (speed < 0.01f) { Velocity.x = 0f; Velocity.z = 0f; return; }
            float control = speed < Settings.StopSpeed ? Settings.StopSpeed : speed;
            float drop = control * friction * dt;
            float newSpeed = Mathf.Max(speed - drop, 0f) / speed;
            Velocity.x *= newSpeed;
            Velocity.z *= newSpeed;
        }

        private void Accelerate(Vector3 wishDir, float wishSpeed, float accel, float dt)
        {
            if (wishSpeed <= 0f) return;
            float current = Vector3.Dot(Velocity, wishDir);
            float add = wishSpeed - current;
            if (add <= 0f) return;
            float accelSpeed = Mathf.Min(accel * wishSpeed * dt, add);
            Velocity += wishDir * accelSpeed;
        }

        private void AirAccelerate(Vector3 wishDir, float wishSpeed, float accel, float dt)
        {
            if (wishSpeed <= 0f) return;
            float capped = Mathf.Min(wishSpeed, Settings.AirWishSpeedCap);
            float current = Vector3.Dot(Velocity, wishDir);
            float add = capped - current;
            if (add <= 0f) return;
            float accelSpeed = Mathf.Min(accel * wishSpeed * dt, add);
            Velocity += wishDir * accelSpeed;
        }

        private void DoJump()
        {
            float js = Settings.JumpSpeed * JumpMultiplier;
            Velocity.y = Mathf.Max(Velocity.y, 0f) + js;
            Grounded = false;
            Sliding = false;
            _ungroundUntil = Time.time + 0.1f;
            _jumpedSinceGrounded = true;
            _lastJumpPressedTime = -10f;
            _lastJumpTime = Time.time;
            if (OnJumped != null) OnJumped();
        }

        private void Move(float dt)
        {
            _hitGround = false;
            _hitSteep = false;
            _bestGroundNormal = Vector3.zero;

            bool wasGrounded = Grounded;
            float fallSpeed = -Velocity.y;
            Vector3 disp = Velocity * dt;
            bool canSnap = Time.time >= _ungroundUntil;
            if (wasGrounded && canSnap) disp.y -= Settings.StickToGround * dt;

            _cc.Move(disp);

            if (!canSnap)
            {
                Grounded = false;
            }
            else if (_hitGround)
            {
                Grounded = true;
                GroundNormal = _bestGroundNormal;
            }
            else if (wasGrounded && Velocity.y <= 0.01f && ProbeGround(out RaycastHit hit))
            {
                // Keep contact when walking down slopes/steps.
                _cc.Move(Vector3.down * Mathf.Max(0f, hit.distance - _cc.skinWidth));
                Grounded = true;
                GroundNormal = hit.normal;
            }
            else
            {
                Grounded = false;
            }

            Surfing = !Grounded && _hitSteep;

            if (Grounded)
            {
                _lastGroundedTime = Time.time;
                _jumpedSinceGrounded = false;
                if (!wasGrounded)
                {
                    if (Crouching && HorizontalSpeed >= Settings.SlideStartSpeed) StartSlide();
                    if (OnLanded != null) OnLanded(fallSpeed);
                }
            }
            else
            {
                GroundNormal = Vector3.up;
            }
        }

        private bool ProbeGround(out RaycastHit hit)
        {
            float scale = transform.lossyScale.y;
            float r = _cc.radius * scale * 0.95f;
            Vector3 origin = transform.position + Vector3.up * (r + 0.05f);
            float minY = Mathf.Cos(Settings.MaxGroundAngle * Mathf.Deg2Rad);
            if (Physics.SphereCast(origin, r, Vector3.down, out hit, Settings.GroundSnapDistance + 0.05f, ~0,
                    QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && hit.collider.transform.IsChildOf(transform)) return false;
                return hit.normal.y >= minY;
            }
            return false;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Vector3 n = hit.normal;
            float minY = Mathf.Cos(Settings.MaxGroundAngle * Mathf.Deg2Rad);
            if (n.y >= minY)
            {
                _hitGround = true;
                if (n.y > _bestGroundNormal.y) _bestGroundNormal = n;
                if (Velocity.y < 0f) Velocity.y = 0f;
            }
            else
            {
                if (n.y > 0.05f) _hitSteep = true;
                // Clip velocity along the surface (walls, ceilings, surf ramps). This is what
                // makes surfing work: gravity's into-ramp component gets removed every frame.
                float into = Vector3.Dot(Velocity, n);
                if (into < 0f) Velocity -= n * into;
            }

            Rigidbody rb = hit.rigidbody;
            if (rb != null && !rb.isKinematic)
            {
                Vector3 push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
                rb.AddForceAtPosition(push * (Settings.PushPower * Mathf.Max(1f, HorizontalSpeed * 0.3f)),
                    hit.point, ForceMode.Impulse);
            }
        }

        // ------------------------------------------------------------------ crouch / slide

        private void UpdateCrouch(bool crouchHeld)
        {
            if (crouchHeld && !Crouching)
            {
                Crouching = true;
                ApplyHeight(Settings.CrouchHeight);
                if (Grounded && HorizontalSpeed >= Settings.SlideStartSpeed) StartSlide();
            }
            else if (!crouchHeld && Crouching && CanStand())
            {
                Crouching = false;
                Sliding = false;
                ApplyHeight(Settings.StandHeight);
            }
            if (Sliding && (!Crouching || HorizontalSpeed < Settings.SlideEndSpeed)) Sliding = false;
        }

        private void StartSlide()
        {
            Sliding = true;
            if (Time.time >= _slideBoostReadyAt)
            {
                Vector3 hv = new Vector3(Velocity.x, 0f, Velocity.z);
                if (hv.sqrMagnitude > 0.01f) Velocity += hv.normalized * Settings.SlideBoost;
                _slideBoostReadyAt = Time.time + Settings.SlideBoostCooldown;
            }
        }

        private bool CanStand()
        {
            float scale = transform.lossyScale.y;
            float r = _cc.radius * scale * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (Settings.CrouchHeight * scale - r);
            float dist = (Settings.StandHeight - Settings.CrouchHeight) * scale;
            RaycastHit hit;
            if (Physics.SphereCast(origin, r, Vector3.up, out hit, dist, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider != null && hit.collider.transform.IsChildOf(transform);
            return true;
        }

        private void ApplyHeight(float h)
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            _cc.height = h;
            _cc.center = new Vector3(0f, h * 0.5f, 0f);
        }

        // ------------------------------------------------------------------ external API

        /// <summary>Add velocity (knockback, launch pads). Upward impulses unstick from the ground.</summary>
        public void AddImpulse(Vector3 deltaV)
        {
            Velocity += deltaV;
            if (deltaV.y > 0.5f)
            {
                Grounded = false;
                _ungroundUntil = Time.time + 0.15f;
            }
        }

        public void Teleport(Vector3 position, bool resetVelocity = true)
        {
            bool was = _cc.enabled;
            _cc.enabled = false;
            transform.position = position;
            _cc.enabled = was;
            if (resetVelocity) Velocity = Vector3.zero;
            Grounded = false;
            Sliding = false;
        }

        public void ForceStand()
        {
            Crouching = false;
            Sliding = false;
            ApplyHeight(Settings.StandHeight);
        }
    }
}
