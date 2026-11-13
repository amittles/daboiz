using Daboiz.Actors;
using Daboiz.Core;
using UnityEngine;

namespace Daboiz.Player
{
    /// <summary>Reads mouse/keyboard (via GameInput) into an ActorCommand. The only place player input lives.</summary>
    public class PlayerInputController : IActorController
    {
        private readonly GameSettings _settings;
        private float _yaw;
        private float _pitch;

        public PlayerInputController(GameSettings settings) { _settings = settings; }

        public void SetView(float yaw, float pitch) { _yaw = yaw; _pitch = pitch; }

        public ActorCommand GetCommand(Actor actor, float dt)
        {
            ActorCommand c = ActorCommand.Idle(_yaw, _pitch);
            bool active = GameFlow.Instance == null || GameFlow.Instance.GameplayInputActive;
            if (!active) return c;

            Vector2 md = GameInput.MouseDelta * _settings.MouseSensitivity;
            _yaw = Mathf.Repeat(_yaw + md.x, 360f);
            _pitch = Mathf.Clamp(_pitch + (_settings.InvertY ? md.y : -md.y), -89f, 89f);
            c.Yaw = _yaw;
            c.Pitch = _pitch;

            float x = 0f, y = 0f;
            if (GameInput.GetKey(KeyCode.W)) y += 1f;
            if (GameInput.GetKey(KeyCode.S)) y -= 1f;
            if (GameInput.GetKey(KeyCode.D)) x += 1f;
            if (GameInput.GetKey(KeyCode.A)) x -= 1f;
            c.Move = new Vector2(x, y);

            c.JumpHeld = GameInput.GetKey(KeyCode.Space);
            c.JumpPressed = GameInput.GetKeyDown(KeyCode.Space);
            c.CrouchHeld = GameInput.GetKey(KeyCode.LeftControl) || GameInput.GetKey(KeyCode.C) || GameInput.GetKey(KeyCode.LeftShift);
            c.FireHeld = GameInput.GetMouseButton(0);
            c.FirePressed = GameInput.GetMouseButtonDown(0);
            c.Reload = GameInput.GetKeyDown(KeyCode.R);
            c.InteractHeld = GameInput.GetKey(KeyCode.E);
            c.InteractPressed = GameInput.GetKeyDown(KeyCode.E);

            c.WeaponSlot = -1;
            for (int i = 0; i < 9; i++)
                if (GameInput.GetKeyDown(KeyCode.Alpha1 + i)) c.WeaponSlot = i;
            float scroll = GameInput.Scroll;
            if (scroll > 0.01f) c.WeaponScroll = -1;
            else if (scroll < -0.01f) c.WeaponScroll = 1;
            if (GameInput.GetKeyDown(KeyCode.Q) || GameInput.GetKeyDown(KeyCode.LeftBracket)) c.WeaponScroll = -1;
            if (GameInput.GetKeyDown(KeyCode.RightBracket)) c.WeaponScroll = 1;
            return c;
        }
    }
}
