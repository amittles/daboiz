using UnityEngine;

namespace Daboiz.Movement
{
    /// <summary>
    /// Source/STRAFTAT-style movement tuning. Units are metres and seconds.
    /// (1 Source unit ~= 1.905 cm, so 320 u/s ~= 6.1 m/s.)
    /// </summary>
    [System.Serializable]
    public class MovementSettings
    {
        [Header("Ground")]
        public float MaxGroundSpeed = 8.5f;
        public float CrouchSpeed = 4f;
        public float GroundAccel = 11f;      // sv_accelerate-ish
        public float GroundFriction = 6f;    // sv_friction
        public float StopSpeed = 2.5f;       // sv_stopspeed
        public float MaxGroundAngle = 45f;   // steeper = surfable

        [Header("Air")]
        public float AirAccel = 14f;         // sv_airaccelerate-ish (higher = easier strafing)
        public float AirWishSpeedCap = 0.9f; // the "30 units" cap that makes air-strafing work
        public float Gravity = 20f;
        public float JumpSpeed = 7.2f;
        public float MaxHorizontalSpeed = 45f;
        public float MaxFallSpeed = 60f;

        [Header("Jump assists")]
        public bool AutoBhop = true;         // hold jump to hop on landing
        public float JumpBuffer = 0.12f;
        public float CoyoteTime = 0.1f;

        [Header("Crouch / Slide")]
        public float StandHeight = 1.8f;
        public float CrouchHeight = 1.0f;
        public float SlideStartSpeed = 6.5f;
        public float SlideEndSpeed = 3.5f;
        public float SlideFriction = 0.6f;
        public float SlideAccel = 2f;
        public float SlideBoost = 3.5f;
        public float SlideBoostCooldown = 1.2f;
        public float SlideGravityAssist = 1f; // speed gained sliding downhill

        [Header("Misc")]
        public float StickToGround = 6f;
        public float GroundSnapDistance = 0.45f;
        public float PushPower = 2.5f;
    }

    /// <summary>One tick of movement intent. Plain struct = easy to send over the network later.</summary>
    public struct MotorInput
    {
        public Vector2 Move;   // x = strafe, y = forward, each -1..1
        public float Yaw;      // degrees
        public bool JumpHeld;
        public bool JumpPressed;
        public bool CrouchHeld;
    }
}
