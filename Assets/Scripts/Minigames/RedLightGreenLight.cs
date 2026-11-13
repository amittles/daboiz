using System.Collections.Generic;
using Daboiz.Actors;
using Daboiz.AI;
using Daboiz.Core;
using Daboiz.UI;
using UnityEngine;

namespace Daboiz.Minigames
{
    /// <summary>
    /// Movement obstacle course (gaps, ramps, slide tunnel, hurdles, launch jump). A giant doll at
    /// the end turns around: anyone moving during RED LIGHT is comically launched out of the game.
    /// </summary>
    public class RedLightGreenLight : MinigameBase
    {
        public override string Title { get { return "Red Light Green Light"; } }
        public override string Tagline { get { return "Move on green. FREEZE on red. Bhop, slide, don't fall."; } }
        public override string HowToPlay
        {
            get
            {
                return "Race to the finish line. When the doll turns to face you it's RED LIGHT:\n" +
                       "anyone still moving gets yeeted. (Mid-air counts as moving!)\n" +
                       "Gaps reset you to the last checkpoint. Crouch (Ctrl/C/Shift) to slide through the tunnel.";
            }
        }

        private enum Signal { Green, Turning, Red, TurningBack }

        private Signal _light = Signal.Green;
        private float _lightTimer;
        private float _redStart;
        private Transform _doll;
        private Renderer[] _dollEyes;
        private float _dollYaw;
        private const float FinishZ = 150f;
        private const float MoveThreshold = 1.6f;
        private const float RedGrace = 0.35f;

        private readonly List<Actor> _finishOrder = new List<Actor>();
        private readonly List<Actor> _eliminated = new List<Actor>();
        private readonly Dictionary<Actor, int> _checkpoint = new Dictionary<Actor, int>();
        private readonly Dictionary<Actor, float> _progressAtOut = new Dictionary<Actor, float>();
        private readonly Dictionary<Actor, float> _lane = new Dictionary<Actor, float>();

        private static readonly Vector3[] Checkpoints =
        {
            new Vector3(0f, 0.1f, -4f), new Vector3(0f, 0.1f, 53f), new Vector3(0f, 3.1f, 75f),
            new Vector3(0f, 0.1f, 105f), new Vector3(0f, 0.1f, 134f)
        };
        private static readonly float[] CheckpointZ = { -999f, 50.5f, 72f, 102f, 132f };

        public RedLightGreenLight()
        {
            RoundDuration = 150f;
            KillY = -10f;
            Rules = new GameRules { DamageEnabled = false, KnockbackEnabled = false, EffectsEnabled = false, RespawnOnDeath = false };
        }

        protected override void Build()
        {
            Transform root = Ctx.Root;
            LevelKit.Environment(new Color(0.75f, 0.8f, 0.95f), new Color(0.75f, 0.65f, 0.6f), new Color(0.4f, 0.3f, 0.25f), true,
                new Color(0.85f, 0.75f, 0.7f), 0.004f);
            Color sand = new Color(0.93f, 0.82f, 0.62f);
            Color pink = new Color(1f, 0.55f, 0.7f);
            Color teal = new Color(0.2f, 0.75f, 0.7f);

            Seg(root, -8f, 30f, 14f, 0f, sand);
            Seg(root, 33.5f, 38f, 14f, 0f, pink);
            Seg(root, 42f, 46f, 8f, 0f, teal);
            Seg(root, 50.5f, 62f, 14f, 0f, sand);
            LevelKit.Ramp(root, new Vector3(0f, 0f, 62f), 0f, 10f, 14f, 3f, pink);
            Prims.Box(root, "Plateau", new Vector3(0f, 1.5f, 76f), new Vector3(14f, 3f, 8f), pink);
            Seg(root, 80f, 84f, 14f, 0f, sand);
            Seg(root, 84f, 102f, 6f, 0f, sand);
            // Slide tunnel: low roof forces a crouch
            Prims.Box(root, "TunnelRoof", new Vector3(0f, 1.25f + 0.5f, 93f), new Vector3(6.6f, 1f, 18f), teal);
            Prims.Box(root, "TunnelWallL", new Vector3(-3.2f, 1.5f, 93f), new Vector3(0.4f, 3f, 18f), teal);
            Prims.Box(root, "TunnelWallR", new Vector3(3.2f, 1.5f, 93f), new Vector3(0.4f, 3f, 18f), teal);
            Seg(root, 102f, 122f, 14f, 0f, sand);
            // Hurdles + pillars
            Prims.Box(root, "Hurdle", new Vector3(0f, 0.35f, 108f), new Vector3(14f, 0.7f, 0.5f), pink);
            Prims.Box(root, "Hurdle", new Vector3(0f, 0.35f, 115f), new Vector3(14f, 0.7f, 0.5f), pink);
            for (int i = 0; i < 4; i++) Prims.Box(root, "Pillar", new Vector3(-5f + i * 3.4f, 2f, 111.5f + (i % 2) * 1.5f), new Vector3(1f, 4f, 1f), teal);
            LevelKit.Ramp(root, new Vector3(0f, 0f, 122f), 0f, 6f, 14f, 1.8f, pink);
            Prims.Box(root, "RampBlock", new Vector3(0f, 0.9f, 128.5f), new Vector3(14f, 1.8f, 1f), pink);
            Seg(root, 132f, 178f, 18f, 0f, sand);
            // Finish line
            Prims.Make(PrimitiveType.Cube, root, "FinishLine", new Vector3(0f, 0.01f, FinishZ), new Vector3(18f, 0.02f, 1f), Color.white, false);
            Prims.Box(root, "FinishPostL", new Vector3(-9f, 2.5f, FinishZ), new Vector3(0.4f, 5f, 0.4f), Color.white);
            Prims.Box(root, "FinishPostR", new Vector3(9f, 2.5f, FinishZ), new Vector3(0.4f, 5f, 0.4f), Color.white);
            Prims.Make(PrimitiveType.Cube, root, "Banner", new Vector3(0f, 5f, FinishZ), new Vector3(18.4f, 0.8f, 0.2f), pink, false);
            // Side walls along the start run to make it feel like a course
            Prims.Box(root, "Wall", new Vector3(-7.2f, 1.5f, 11f), new Vector3(0.4f, 3f, 38f), new Color(0.85f, 0.75f, 0.6f));
            Prims.Box(root, "Wall", new Vector3(7.2f, 1.5f, 11f), new Vector3(0.4f, 3f, 38f), new Color(0.85f, 0.75f, 0.6f));

            BuildDoll(root, new Vector3(0f, 0f, 170f));

            for (int i = 0; i < 6; i++)
            {
                SpawnPoints.Add(new Vector3(-5f + i * 2f, 0.1f, -4f));
                SpawnYaws.Add(0f);
            }
            SpawnAllActors(null); // no weapons in this one
            foreach (Actor a in Ctx.Actors)
            {
                _checkpoint[a] = 0;
                _lane[a] = Random.Range(-4f, 4f);
            }
            SpectatorPoint = new Vector3(18f, 30f, 60f);
            SpectatorLookAt = new Vector3(0f, 0f, 100f);
            _lightTimer = 4f;
        }

        private static void Seg(Transform root, float z0, float z1, float width, float topY, Color c)
        {
            Prims.Box(root, "Course", new Vector3(0f, topY - 0.5f, (z0 + z1) * 0.5f), new Vector3(width, 1f, z1 - z0), c);
        }

        private void BuildDoll(Transform root, Vector3 pos)
        {
            Prims.Box(root, "Pedestal", pos + Vector3.up * 1f, new Vector3(4f, 2f, 4f), new Color(0.4f, 0.3f, 0.3f));
            _doll = new GameObject("Doll").transform;
            _doll.SetParent(root, false);
            _doll.position = pos + Vector3.up * 2f;
            Color dress = new Color(1f, 0.55f, 0.1f);
            Color skin = new Color(1f, 0.85f, 0.7f);
            Prims.Visual(PrimitiveType.Cylinder, _doll, new Vector3(0f, 2.5f, 0f), new Vector3(3.2f, 2.5f, 3.2f), dress);
            Prims.Visual(PrimitiveType.Cube, _doll, new Vector3(0f, 5.5f, 0f), new Vector3(2.4f, 1.2f, 1.4f), new Color(1f, 0.95f, 0.4f));
            Prims.Visual(PrimitiveType.Sphere, _doll, new Vector3(0f, 7.2f, 0f), Vector3.one * 2.6f, skin);
            Prims.Visual(PrimitiveType.Sphere, _doll, new Vector3(0f, 7.6f, -0.25f), new Vector3(2.75f, 2.4f, 2.6f), new Color(0.1f, 0.08f, 0.06f));
            _dollEyes = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                GameObject eye = Prims.Visual(PrimitiveType.Sphere, _doll, new Vector3(i == 0 ? -0.45f : 0.45f, 7.35f, 1.15f), Vector3.one * 0.45f, Color.black);
                _dollEyes[i] = eye.GetComponent<Renderer>();
            }
            Prims.Visual(PrimitiveType.Cube, _doll, new Vector3(0f, 6.7f, 1.22f), new Vector3(0.6f, 0.12f, 0.1f), new Color(0.8f, 0.2f, 0.3f));
            _dollYaw = 0f; // facing +Z = away from the players
            _doll.rotation = Quaternion.Euler(0f, _dollYaw, 0f);
        }

        protected override void OnRoundStart()
        {
            _light = Signal.Green;
            _lightTimer = Random.Range(3f, 5f);
            Announce("GREEN LIGHT", new Color(0.3f, 1f, 0.4f), 1.2f);
        }

        protected override void OnTick(float dt)
        {
            UpdateLight(dt);

            float targetYaw = (_light == Signal.Red || _light == Signal.Turning) ? 180f : 0f;
            _dollYaw = Mathf.MoveTowardsAngle(_dollYaw, targetYaw, 360f * dt);
            _doll.rotation = Quaternion.Euler(0f, _dollYaw, 0f);
            Material eyeMat = _light == Signal.Red ? Mats.Emissive(Color.red, 3f) : Mats.Get(Color.black);
            for (int i = 0; i < _dollEyes.Length; i++) _dollEyes[i].sharedMaterial = eyeMat;

            foreach (Actor a in Ctx.Actors)
            {
                if (!a.IsAlive || _finishOrder.Contains(a)) continue;
                float z = a.transform.position.z;
                int cp = _checkpoint[a];
                while (cp + 1 < CheckpointZ.Length && z >= CheckpointZ[cp + 1] && a.Motor.Grounded) cp++;
                _checkpoint[a] = cp;

                if (z >= FinishZ)
                {
                    _finishOrder.Add(a);
                    Feed(a.DisplayName + " finished " + Ordinal(_finishOrder.Count) + "!");
                    ProceduralAudio.Play(Sfx.Ding, a.transform.position, 1f, 1f + _finishOrder.Count * 0.1f);
                    Fx.Confetti(a.transform.position + Vector3.up * 2f, 40);
                    continue;
                }

                if (_light == Signal.Red && Time.time - _redStart > RedGrace && a.Motor.Velocity.magnitude > MoveThreshold)
                {
                    _progressAtOut[a] = z;
                    _eliminated.Add(a);
                    Feed(a.DisplayName + " moved on RED. Yeet.");
                    ProceduralAudio.Play(Sfx.Buzz, a.transform.position, 1f, 0.8f);
                    Fx.Confetti(a.transform.position + Vector3.up, 25);
                    a.Eliminate(true);
                }
            }

            bool anyRacing = false;
            foreach (Actor a in Ctx.Actors) if (a.IsAlive && !_finishOrder.Contains(a)) anyRacing = true;
            if (!anyRacing) Finish(_finishOrder.Count > 0 ? _finishOrder[0].DisplayName + " wins the race!" : "Nobody made it!");
        }

        private void UpdateLight(float dt)
        {
            _lightTimer -= dt;
            if (_lightTimer > 0f) return;
            switch (_light)
            {
                case Signal.Green:
                    _light = Signal.Turning;
                    _lightTimer = 0.6f;
                    ProceduralAudio.PlayUI(Sfx.Buzz, 0.5f, 1.4f);
                    break;
                case Signal.Turning:
                    _light = Signal.Red;
                    _redStart = Time.time;
                    _lightTimer = Random.Range(1.6f, 3.4f);
                    Announce("RED LIGHT", new Color(1f, 0.25f, 0.2f), 1.2f);
                    break;
                case Signal.Red:
                    _light = Signal.TurningBack;
                    _lightTimer = 0.4f;
                    break;
                case Signal.TurningBack:
                    _light = Signal.Green;
                    _lightTimer = Random.Range(2.2f, 5.5f);
                    Announce("GREEN LIGHT", new Color(0.3f, 1f, 0.4f), 1f);
                    ProceduralAudio.PlayUI(Sfx.Ding, 0.6f, 1.5f);
                    break;
            }
        }

        protected override void OnActorFellOut(Actor a)
        {
            // Falling isn't fatal here, just costly.
            Vector3 cp = Checkpoints[_checkpoint[a]];
            a.TeleportTo(cp + new Vector3(_lane[a] * 0.5f, 0f, 0f), 0f);
            ProceduralAudio.Play(Sfx.Whoosh, cp, 0.6f, 0.8f);
        }

        public override void HandleActorDied(Actor victim, Actor killer) { }

        protected override void OnTimeUp() { Finish("Time's up!"); }

        private void Finish(string headline)
        {
            List<RankEntry> r = new List<RankEntry>();
            int place = 1;
            for (int i = 0; i < _finishOrder.Count; i++) r.Add(new RankEntry(_finishOrder[i].Participant, place++, "finished"));
            List<Actor> rest = new List<Actor>();
            foreach (Actor a in Ctx.Actors) if (!_finishOrder.Contains(a)) rest.Add(a);
            rest.Sort((x, y) => Progress(y).CompareTo(Progress(x)));
            foreach (Actor a in rest)
                r.Add(new RankEntry(a.Participant, place++, (_eliminated.Contains(a) ? "yeeted at " : "reached ") + Mathf.RoundToInt(Progress(a)) + "m"));
            EndRound(r, headline);
        }

        private float Progress(Actor a)
        {
            float p;
            if (_progressAtOut.TryGetValue(a, out p)) return p - 1000f; // eliminated rank below survivors
            return a.transform.position.z;
        }

        public override void DriveBot(BotBrain bot)
        {
            Actor self = bot.Self;
            if (_finishOrder.Contains(self))
            {
                bot.MoveTarget = new Vector3(_lane[self], 0f, FinishZ + 8f);
                return;
            }
            Vector3 pos = self.transform.position;
            bool danger = _light == Signal.Turning || _light == Signal.Red;
            if (danger)
            {
                // Per-light reaction time: sometimes too slow.
                if (bot.Timer2 <= 0f) bot.Timer2 = Time.time + Mathf.Lerp(0.75f, 0.15f, bot.Skill) * Random.Range(0.6f, 1.25f);
                if (Time.time >= bot.Timer2) { bot.HoldStill = true; bot.DesiredYaw = 0f; return; }
            }
            else bot.Timer2 = 0f;

            float lane = _lane[self];
            float z = pos.z;
            if (z > 40f && z < 48f) lane = Mathf.Clamp(lane, -2.5f, 2.5f);
            if (z > 79f && z < 103f) lane = 0f;
            bot.MoveTarget = new Vector3(lane, pos.y, z + 8f);
            bot.DesiredYaw = 0f;
            bot.WantCrouch = z > 83f && z < 103f;
            bot.AllowBhop = !bot.WantCrouch;

            // Jump at gap edges
            Vector3 vel = self.Motor.Velocity;
            vel.y = 0f;
            if (self.Motor.Grounded && vel.sqrMagnitude > 9f)
            {
                Vector3 ahead = pos + vel.normalized * 1.3f + Vector3.up * 0.5f;
                if (!Physics.Raycast(ahead, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore)) bot.WantJump = true;
            }
        }

        public override void DrawHUD(Hud hud)
        {
            float s = hud.Scale;
            string txt;
            Color c;
            switch (_light)
            {
                case Signal.Green: txt = "GREEN LIGHT"; c = new Color(0.3f, 1f, 0.4f); break;
                case Signal.Turning: txt = "...turning..."; c = new Color(1f, 0.8f, 0.2f); break;
                case Signal.Red: txt = "RED LIGHT - DON'T MOVE"; c = new Color(1f, 0.25f, 0.2f); break;
                default: txt = "GREEN LIGHT"; c = new Color(0.3f, 1f, 0.4f); break;
            }
            if (State == MinigameState.Playing)
            {
                hud.Rect(new Rect(0, 52 * s, Screen.width, 46 * s), new Color(c.r, c.g, c.b, 0.25f));
                hud.Label(new Rect(0, 52 * s, Screen.width, 46 * s), txt, 30, c, TextAnchor.MiddleCenter);
            }
            Actor local = Ctx.LocalActor;
            if (local != null && local.IsAlive)
            {
                float dist = Mathf.Max(0f, FinishZ - local.transform.position.z);
                hud.Label(new Rect(20 * s, 100 * s, 400 * s, 24 * s), "Finish: " + Mathf.RoundToInt(dist) + "m   Checkpoint " +
                    (_checkpoint[local] + 1) + "/" + Checkpoints.Length, 16, Color.white);
                if (_light == Signal.Red)
                {
                    float v = local.Motor.Velocity.magnitude;
                    hud.Label(new Rect(0, Screen.height * 0.66f, Screen.width, 30 * s), v > MoveThreshold * 0.6f ? "STOP!!" : "frozen... good",
                        22, v > MoveThreshold * 0.6f ? Color.red : Color.white, TextAnchor.MiddleCenter);
                }
            }
        }

        public override IEnumerable<string> ScoreLines()
        {
            foreach (Actor a in Ctx.Actors)
            {
                string st = _finishOrder.Contains(a) ? "FINISHED " + Ordinal(_finishOrder.IndexOf(a) + 1)
                    : _eliminated.Contains(a) ? "OUT" : Mathf.RoundToInt(a.transform.position.z) + "m";
                yield return a.DisplayName + "   " + st;
            }
        }
    }
}
