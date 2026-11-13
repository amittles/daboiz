using Daboiz.Core;
using Daboiz.Movement;
using Daboiz.Weapons;
using UnityEngine;

namespace Daboiz.Actors
{
    /// <summary>Builds a complete actor GameObject (body, motor, weapons, controller, camera for locals).</summary>
    public static class ActorFactory
    {
        public static Actor Spawn(Participant p, Vector3 position, float yaw, Transform parent, GameSettings settings)
        {
            GameObject go = new GameObject((p.IsLocal ? "Player_" : "Bot_") + p.Name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            CharacterController cc = go.AddComponent<CharacterController>();
            cc.radius = 0.4f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0f;

            CharacterMotor motor = go.AddComponent<CharacterMotor>();
            motor.Settings.AutoBhop = p.IsLocal ? settings.AutoBhop : true;

            Actor actor = go.AddComponent<Actor>();
            actor.Participant = p;
            actor.IsLocalPlayer = p.IsLocal;
            actor.Yaw = yaw;

            ActorVisual vis = go.AddComponent<ActorVisual>();
            vis.Build(p.Color, p.IsLocal);
            actor.Visual = vis;

            WeaponHolder holder = go.AddComponent<WeaponHolder>();
            actor.Weapons = holder;

            if (p.IsLocal)
            {
                Player.PlayerInputController input = new Player.PlayerInputController(settings);
                input.SetView(yaw, 0f);
                actor.Controller = input;
                Player.PlayerCameraRig rig = go.AddComponent<Player.PlayerCameraRig>();
                rig.Init(actor, GameFlow.Instance != null ? GameFlow.Instance.Cam : Camera.main, settings);
            }
            else
            {
                AI.BotBrain brain = new AI.BotBrain(actor, p.BotSkill);
                brain.SetView(yaw, 0f);
                actor.Controller = brain;
            }
            return actor;
        }
    }
}
