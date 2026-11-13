using System.Collections.Generic;
using Daboiz.Actors;
using Daboiz.AI;
using Daboiz.Core;
using Daboiz.Props;
using Daboiz.UI;
using Daboiz.Weapons;
using UnityEngine;

namespace Daboiz.Minigames
{
    /// <summary>Free play: every gun, physics props, target dummies and a movement course.</summary>
    public class SandboxRange : MinigameBase
    {
        public override string Title { get { return "Sandbox / Range"; } }
        public override string Tagline { get { return "Try every gun. Knock stuff over. Practise bhops and surf."; } }
        public override string HowToPlay
        {
            get
            {
                return "Scroll wheel / Q / [ ] cycles ALL guns, 1-9 picks the first nine.\n" +
                       "P = reset props   B = toggle bot aggression   Tab = scores\n" +
                       "Movement course on the left: ramps, bhop pillars, slide tunnel and a surf ramp.";
            }
        }

        private Transform _propRoot;
        private bool _botsAggressive = true;
        private readonly Dictionary<Actor, WeaponDefinition> _botGun = new Dictionary<Actor, WeaponDefinition>();
        private readonly Dictionary<Actor, float> _botSwitchAt = new Dictionary<Actor, float>();

        public SandboxRange()
        {
            RoundDuration = 0f;
            IntroDuration = 2.5f;
            KillY = -20f;
            Rules = new GameRules { RespawnOnDeath = true, RespawnDelay = 2f };
            LockDuringIntroValue = false;
        }

        private bool LockDuringIntroValue;
        public override bool LockDuringIntro { get { return LockDuringIntroValue; } }

        protected override void Build()
        {
            Transform root = Ctx.Root;
            LevelKit.Environment(new Color(0.65f, 0.75f, 0.95f), new Color(0.55f, 0.55f, 0.6f), new Color(0.3f, 0.28f, 0.25f), false, Color.gray);
            LevelKit.CheckerFloor(root, Vector3.zero, 20, 20, 4f, new Color(0.82f, 0.82f, 0.86f), new Color(0.62f, 0.64f, 0.7f));
            Color wall = new Color(0.45f, 0.5f, 0.6f);
            LevelKit.Wall(root, new Vector3(-40, 0, -40), new Vector3(40, 0, -40), 6f, 1f, wall);
            LevelKit.Wall(root, new Vector3(-40, 0, 40), new Vector3(40, 0, 40), 6f, 1f, wall);
            LevelKit.Wall(root, new Vector3(-40, 0, -40), new Vector3(-40, 0, 40), 6f, 1f, wall);
            LevelKit.Wall(root, new Vector3(40, 0, -40), new Vector3(40, 0, 40), 6f, 1f, wall);

            // Shooting range backstop + dummies
            Prims.Box(root, "Backstop", new Vector3(10f, 3f, 37f), new Vector3(40f, 6f, 1f), new Color(0.35f, 0.3f, 0.3f));
            for (int i = 0; i < 6; i++) TargetDummy.Create(root, new Vector3(-2f + i * 4f, 0f, 32f), 180f);
            for (int i = 0; i < 3; i++) TargetDummy.Create(root, new Vector3(4f + i * 6f, 0f, 20f + i * 3f), 180f);

            BuildProps();
            BuildMovementCourse(root);

            for (int i = 0; i < 6; i++)
            {
                SpawnPoints.Add(new Vector3(-6f + i * 2.5f, 0.1f, -12f));
                SpawnYaws.Add(0f);
            }
            List<WeaponDefinition> all = WeaponRegistry.SandboxList;
            SpawnAllActors(p =>
            {
                if (p.IsLocal) return all;
                List<WeaponDefinition> l = new List<WeaponDefinition>();
                for (int i = 0; i < 3; i++) l.Add(WeaponRegistry.Random(Ctx.Rng, "dm"));
                return l;
            });
            SpectatorPoint = new Vector3(0f, 25f, -30f);
        }

        private void BuildProps()
        {
            if (_propRoot != null) Object.Destroy(_propRoot.gameObject);
            _propRoot = new GameObject("Props").transform;
            _propRoot.SetParent(Ctx.Root, false);
            Transform pr = _propRoot;

            // Tables with can pyramids
            for (int t = 0; t < 3; t++)
            {
                Vector3 tc = new Vector3(-2f + t * 6f, 0f, 12f);
                Prims.Box(pr, "Table", tc + Vector3.up * 0.95f, new Vector3(3f, 0.1f, 1.2f), new Color(0.5f, 0.35f, 0.2f));
                Prims.Box(pr, "Leg", tc + new Vector3(-1.3f, 0.45f, 0f), new Vector3(0.15f, 0.9f, 1f), new Color(0.4f, 0.28f, 0.15f));
                Prims.Box(pr, "Leg", tc + new Vector3(1.3f, 0.45f, 0f), new Vector3(0.15f, 0.9f, 1f), new Color(0.4f, 0.28f, 0.15f));
                for (int row = 0; row < 4; row++)
                    for (int i = 0; i < 4 - row; i++)
                    {
                        Vector3 p = tc + new Vector3((i - (3 - row) * 0.5f) * 0.17f, 1.0f + 0.125f + row * 0.245f, 0f);
                        PhysicsProp.Create(PropKind.Can, pr, p, Quaternion.identity);
                    }
            }

            // Crate pyramid
            for (int row = 0; row < 4; row++)
                for (int i = 0; i < 4 - row; i++)
                    PhysicsProp.Create(PropKind.Crate, pr, new Vector3(22f + (i - (3 - row) * 0.5f) * 0.82f, 0.4f + row * 0.81f, 14f), Quaternion.identity);

            // Barrels (some explosive)
            for (int i = 0; i < 6; i++)
                PhysicsProp.Create(i % 3 == 0 ? PropKind.ExplosiveBarrel : PropKind.Barrel, pr,
                    new Vector3(28f + (i % 3) * 0.9f, 0.46f, 4f + (i / 3) * 0.9f), Quaternion.identity);

            // Bowling pins
            for (int row = 0; row < 4; row++)
                for (int i = 0; i <= row; i++)
                    PhysicsProp.Create(PropKind.Pin, pr, new Vector3(12f + (i - row * 0.5f) * 0.42f, 0.39f, 24f + row * 0.37f), Quaternion.identity);

            // Beach balls
            for (int i = 0; i < 5; i++)
                PhysicsProp.Create(PropKind.Ball, pr, new Vector3(-8f + i * 1.5f, 0.3f, 4f), Quaternion.identity);
            for (int i = 0; i < 4; i++)
                PhysicsProp.Create(PropKind.Chair, pr, new Vector3(30f + i * 1.2f, 0.3f, -8f), Quaternion.Euler(0f, i * 40f, 0f));
        }

        private static void BuildMovementCourse(Transform root)
        {
            Color c = new Color(0.95f, 0.6f, 0.25f);
            // Ramps
            LevelKit.Ramp(root, new Vector3(-30f, 0f, -30f), 0f, 10f, 6f, 3f, c);
            Prims.Box(root, "RampTop", new Vector3(-30f, 1.5f, -16f), new Vector3(6f, 3f, 8f), c);
            LevelKit.Ramp(root, new Vector3(-30f, 0f, -2f), 180f, 10f, 6f, 3f, c);
            // Bhop pillars
            for (int i = 0; i < 7; i++)
                Prims.Box(root, "Pillar", new Vector3(-20f, 0.5f + i * 0.25f, -30f + i * 4.5f), new Vector3(2f, 1f + i * 0.5f, 2f),
                    Color.Lerp(c, Color.white, 0.4f));
            // Slide tunnel
            Prims.Box(root, "TunnelRoof", new Vector3(-36f, 1.35f, 0f), new Vector3(4f, 0.3f, 14f), new Color(0.3f, 0.3f, 0.35f));
            Prims.Box(root, "TunnelWallL", new Vector3(-38.2f, 0.75f, 0f), new Vector3(0.4f, 1.5f, 14f), new Color(0.3f, 0.3f, 0.35f));
            Prims.Box(root, "TunnelWallR", new Vector3(-33.8f, 0.75f, 0f), new Vector3(0.4f, 1.5f, 14f), new Color(0.3f, 0.3f, 0.35f));
            // Surf ramp + access stairs
            LevelKit.SurfRamp(root, new Vector3(-28f, 7f, 8f), 0f, 28f, 6f, 60f, 0f, new Color(0.3f, 0.7f, 1f));
            for (int i = 0; i < 8; i++)
                Prims.Box(root, "Step", new Vector3(-20f, 0.5f + i, 6f - i * 1.4f + 10f), new Vector3(3f, 1f + 2f * i, 1.4f), new Color(0.5f, 0.5f, 0.55f));
            Prims.Box(root, "SurfStart", new Vector3(-23.5f, 7.5f, 6f), new Vector3(4f, 1f, 3f), new Color(0.5f, 0.5f, 0.55f));
        }

        protected override void OnTick(float dt)
        {
            if (GameInput.GetKeyDown(KeyCode.P) && GameFlow.Instance.GameplayInputActive)
            {
                BuildProps();
                Announce("Props reset", Color.white, 1f);
            }
            if (GameInput.GetKeyDown(KeyCode.B) && GameFlow.Instance.GameplayInputActive)
            {
                _botsAggressive = !_botsAggressive;
                Announce(_botsAggressive ? "Bots: aggressive" : "Bots: chill", Color.white, 1.2f);
            }
        }

        public override void DriveBot(BotBrain bot)
        {
            Actor self = bot.Self;
            float switchAt;
            if (!_botSwitchAt.TryGetValue(self, out switchAt) || Time.time > switchAt)
            {
                _botSwitchAt[self] = Time.time + Random.Range(10f, 20f);
                bot.WeaponSlot = Random.Range(0, Mathf.Max(1, self.Weapons.Inventory.Count));
            }
            if (_botsAggressive)
            {
                bot.DefaultCombat();
                return;
            }
            // Chill: wander and plink at props/dummies
            if (bot.Timer < Time.time || (bot.Point - self.transform.position).sqrMagnitude < 4f)
            {
                bot.Timer = Time.time + Random.Range(3f, 7f);
                bot.Point = new Vector3(Random.Range(-15f, 25f), 0f, Random.Range(-10f, 20f));
            }
            bot.MoveTarget = bot.Point;
            bot.AllowBhop = false;
        }

        public override void DrawHUD(Hud hud)
        {
            float s = hud.Scale;
            hud.Label(new Rect(20 * s, 44 * s, 900 * s, 24 * s),
                "Scroll/Q/[ ] cycle " + WeaponRegistry.SandboxList.Count + " guns   P reset props   B bots " +
                (_botsAggressive ? "aggressive" : "chill") + "   Esc menu", 14, new Color(1f, 1f, 1f, 0.75f));
        }

        public override IEnumerable<string> ScoreLines()
        {
            foreach (Actor a in Ctx.Actors) yield return a.DisplayName + "   K " + a.Kills + "  D " + a.Deaths;
        }
    }
}
