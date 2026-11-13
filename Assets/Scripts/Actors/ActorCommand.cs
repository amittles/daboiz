using UnityEngine;

namespace Daboiz.Actors
{
    /// <summary>
    /// Everything an actor wants to do this frame. Produced by a local input reader, a bot brain,
    /// or (later) a network message. Gameplay never reads input devices directly.
    /// </summary>
    public struct ActorCommand
    {
        public Vector2 Move;
        public float Yaw;
        public float Pitch;
        public bool JumpHeld;
        public bool JumpPressed;
        public bool CrouchHeld;
        public bool FireHeld;
        public bool FirePressed;
        public bool Reload;
        public int WeaponSlot;   // -1 = no change, 0..8 = select slot
        public int WeaponScroll; // -1, 0, +1
        public bool InteractHeld;
        public bool InteractPressed;

        public static ActorCommand Idle(float yaw, float pitch)
        {
            return new ActorCommand { Yaw = yaw, Pitch = pitch, WeaponSlot = -1 };
        }
    }

    public interface IActorController
    {
        ActorCommand GetCommand(Actor actor, float dt);
    }
}
