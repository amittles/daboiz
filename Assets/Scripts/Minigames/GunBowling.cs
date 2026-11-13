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
    /// <summary>
    /// Turn-based bowling where the ball is a gun. Each frame rolls a random "bowling-friendly" gun;
    /// every player gets 3 shots from the foul line. Score = pins knocked (+5 strike bonus).
    /// </summary>
    public class GunBowling : MinigameBase
    {
        public override string Title { get { return "Gun Bowling"; } }
        public override string Tagline { get { return "No ball. Just a gun. Knock down the pins."; } }
        public override string HowToPlay
        {
            get
            {
                return "Players take turns at the foul line. You get " + ShotsPerTurn + " shots with this frame's gun.\n" +
                       "Score = pins down after the shots settle. All 10 = STRIKE (+5).\n" +
                       Frames + " frames. A new random gun every frame. Stay behind the line!";
            }
        }

        private enum Phase { Setup, Aim, Settle, Score }

        public int Frames = 2;
        public const int ShotsPerTurn = 3;
        private const float LaneZ0 = 0f;
        private const float PinZ = 18f;
        private const float AimTime = 10f;
        private const float SettleTime = 3.5f;

        private readonly List<PhysicsProp> _pins = new List<PhysicsProp>();
        private readonly List<Vector3> _pinStarts = new List<Vector3>();
        private Phase _phase = Phase.Setup;
        private float _phaseTimer;
        private int _frame;
        private int _turn;      // index into Ctx.Actors
        private Actor _active;
        private WeaponDefinition _frameGun;
        private int _lastKnocked;
        private readonly Dictionary<Actor, List<int>> _frameScores = new Dictionary<Actor, List<int>>();
        private List<WeaponDefinition> _gunPool;

        public GunBowling()
        {
            RoundDuration = 0f;
            KillY = -15f;
            Rules = new GameRules { DamageEnabled = false, KnockbackEnabled = true, EffectsEnabled = true, KnockbackScale = 0.6f, RespawnOnDeath = true };
        }

        protected override void Build()
        {
            Transform root = Ctx.Root;
            LevelKit.Environment(new Color(0.35f, 0.3f, 0.5f), new Color(0.3f, 0.25f, 0.35f), new Color(0.15f, 0.1f, 0.12f), false, Color.black);
            Color wood = new Color(0.85f, 0.65f, 0.4f);
            Color carpet = new Color(0.3f, 0.1f, 0.4f);

            // Approach / seating area
            LevelKit.CheckerFloor(root, new Vector3(0f, 0f, -7f), 8, 4, 2.5f, carpet, new Color(0.15f, 0.5f, 0.6f));
            // Lane + gutters
            Prims.Box(root, "Lane", new Vector3(0f, -0.5f, 11f), new Vector3(3.2f, 1f, 22f), wood);
            Prims.Box(root, "GutterL", new Vector3(-1.85f, -0.65f, 11f), new Vector3(0.5f, 1f, 22f), new Color(0.2f, 0.2f, 0.25f));
            Prims.Box(root, "GutterR", new Vector3(1.85f, -0.65f, 11f), new Vector3(0.5f, 1f, 22f), new Color(0.2f, 0.2f, 0.25f));
            Prims.Box(root, "RailL", new Vector3(-2.2f, 0.2f, 11f), new Vector3(0.2f, 1.2f, 22f), new Color(0.5f, 0.5f, 0.55f));
            Prims.Box(root, "RailR", new Vector3(2.2f, 0.2f, 11f), new Vector3(0.2f, 1.2f, 22f), new Color(0.5f, 0.5f, 0.55f));
            Prims.Box(root, "Pit", new Vector3(0f, -1f, 23f), new Vector3(4.6f, 1f, 2f), new Color(0.05f, 0.05f, 0.05f));
            Prims.Box(root, "BackWall", new Vector3(0f, 1.5f, 24.2f), new Vector3(6f, 5f, 0.5f), new Color(0.1f, 0.1f, 0.12f));
            Prims.Box(root, "Hood", new Vector3(0f, 2.6f, 19.5f), new Vector3(5f, 1.5f, 3f), new Color(0.2f, 0.1f, 0.3f));
            Prims.Make(PrimitiveType.Cube, root, "FoulLine", new Vector3(0f, 0.005f, LaneZ0), new Vector3(16f, 0.01f, 0.08f), Color.red, false);
            // Lane arrows
            for (int i = -2; i <= 2; i++)
                Prims.Make(PrimitiveType.Cube, root, "Arrow", new Vector3(i * 0.5f, 0.005f, 5f + Mathf.Abs(i) * 0.5f), new Vector3(0.08f, 0.01f, 0.3f),
                    new Color(0.4f, 0.2f, 0.1f), false);
            // Neighbour decorative lanes
            for (int side = -1; side <= 1; side += 2)
                for (int l = 1; l <= 2; l++)
                    Prims.Box(root, "DecoLane", new Vector3(side * l * 5f, -0.5f, 11f), new Vector3(3.2f, 1f, 22f), Color.Lerp(wood, Color.gray, 0.3f));
            // Benches
            for (int i = 0; i < 2; i++)
                Prims.Box(root, "Bench", new Vector3(i == 0 ? -6f : 6f, 0.25f, -9f), new Vector3(3.5f, 0.5f, 1f), new Color(0.6f, 0.15f, 0.2f));
            LevelKit.Lamp(root, new Vector3(0f, 4f, 17f), new Color(1f, 0.95f, 0.85f), 12f, 2f);
            LevelKit.Lamp(root, new Vector3(0f, 4f, 4f), new Color(1f, 0.6f, 0.9f), 14f, 1.2f);

            // Pins (triangle, head pin nearest)
            float spacing = 0.42f;
            for (int row = 0; row < 4; row++)
                for (int i = 0; i <= row; i++)
                {
                    Vector3 p = new Vector3((i - row * 0.5f) * spacing, 0.39f, PinZ + row * spacing * 0.866f);
                    _pins.Add(PhysicsProp.Create(PropKind.Pin, root, p, Quaternion.identity));
                    _pinStarts.Add(p);
                }

            for (int i = 0; i < 6; i++)
            {
                SpawnPoints.Add(new Vector3(-7f + i * 2.8f, 0.1f, -7f));
                SpawnYaws.Add(0f);
            }
            _gunPool = WeaponRegistry.WithTag("bowling");
            _frameGun = _gunPool[Ctx.Rng.Next(_gunPool.Count)];
            SpawnAllActors(p => new List<WeaponDefinition> { _frameGun });
            foreach (Actor a in Ctx.Actors) _frameScores[a] = new List<int>();
            SpectatorPoint = new Vector3(0f, 4f, -6f);
            SpectatorLookAt = new Vector3(0f, 0f, PinZ);
        }

        protected override void OnRoundStart()
        {
            _frame = 0;
            _turn = 0;
            BeginTurn();
        }

        private void BeginTurn()
        {
            _active = Ctx.Actors[_turn];
            ResetPins();
            for (int i = 0; i < Ctx.Actors.Count; i++)
            {
                Actor a = Ctx.Actors[i];
                if (!a.IsAlive) a.Respawn(SpawnPoints[i], 0f);
                if (a != _active && a.transform.position.z > -3f) a.TeleportTo(SpawnPoints[i], 0f);
                a.Weapons.SetLoadout(new List<WeaponDefinition> { _frameGun });
                WeaponInstance w = a.Weapons.Current;
                w.Magazine = _frameGun.Mode == FireMode.Beam ? 40 : ShotsPerTurn;
                w.Reserve = 0;
            }
            _active.TeleportTo(new Vector3(0f, 0.1f, -1.2f), 0f);
            _phase = Phase.Setup;
            _phaseTimer = 1.2f;
            Announce(_active.DisplayName + "'s turn  -  " + _frameGun.DisplayName, _active.Color, 2f);
        }

        private void ResetPins()
        {
            for (int i = 0; i < _pins.Count; i++)
            {
                PhysicsProp p = _pins[i];
                if (p == null) continue;
                p.Body.isKinematic = true;
                p.transform.SetPositionAndRotation(_pinStarts[i], Quaternion.identity);
                p.Body.isKinematic = false;
                RbCompat.SetVelocity(p.Body, Vector3.zero);
                p.Body.angularVelocity = Vector3.zero;
                p.Body.Sleep();
            }
        }

        private int CountKnocked()
        {
            int n = 0;
            for (int i = 0; i < _pins.Count; i++)
            {
                PhysicsProp p = _pins[i];
                if (p == null) { n++; continue; }
                Vector3 d = p.transform.position - _pinStarts[i];
                d.y = 0f;
                if (p.transform.up.y < 0.8f || d.magnitude > 0.3f || p.transform.position.y < 0f) n++;
            }
            return n;
        }

        protected override void OnTick(float dt)
        {
            // Keep everyone behind the foul line
            foreach (Actor a in Ctx.Actors)
            {
                if (!a.IsAlive) continue;
                Vector3 p = a.transform.position;
                if (p.z > LaneZ0 - 0.4f && p.y > -1f)
                {
                    a.TeleportTo(new Vector3(p.x, p.y, LaneZ0 - 0.5f), a.Yaw, false);
                    a.Motor.Velocity.z = Mathf.Min(0f, a.Motor.Velocity.z);
                }
            }

            _phaseTimer -= dt;
            switch (_phase)
            {
                case Phase.Setup:
                    if (_phaseTimer <= 0f) { _phase = Phase.Aim; _phaseTimer = AimTime; }
                    break;
                case Phase.Aim:
                    WeaponInstance w = _active.Weapons.Current;
                    bool outOfAmmo = w == null || (w.Magazine <= 0 && !_active.Weapons.BeamActive);
                    if (outOfAmmo || _phaseTimer <= 0f) { _phase = Phase.Settle; _phaseTimer = SettleTime; }
                    break;
                case Phase.Settle:
                    if (_phaseTimer <= 0f)
                    {
                        _lastKnocked = CountKnocked();
                        int pts = _lastKnocked + (_lastKnocked == 10 ? 5 : 0);
                        AddScore(_active, pts);
                        _frameScores[_active].Add(pts);
                        if (_lastKnocked == 10) { Announce("STRIKE!!", new Color(1f, 0.85f, 0.2f), 2f); Fx.Confetti(new Vector3(0f, 2f, PinZ), 80); }
                        else Announce(_lastKnocked + " pins", Color.white, 1.5f);
                        ProceduralAudio.PlayUI(Sfx.Ding, 0.8f, 0.8f + _lastKnocked * 0.05f);
                        Feed(_active.DisplayName + ": " + _lastKnocked + " pins (" + pts + " pts)");
                        _phase = Phase.Score;
                        _phaseTimer = 1.6f;
                    }
                    break;
                case Phase.Score:
                    if (_phaseTimer <= 0f) NextTurn();
                    break;
            }
        }

        private void NextTurn()
        {
            _turn++;
            if (_turn >= Ctx.Actors.Count)
            {
                _turn = 0;
                _frame++;
                if (_frame >= Frames)
                {
                    List<RankEntry> r = RankByScore(a => GetScore(a) + " pts");
                    EndRound(r, Ctx.ActorOf(r[0].Participant).DisplayName + " bowls the best game!");
                    return;
                }
                WeaponDefinition next = _frameGun;
                for (int tries = 0; tries < 5 && next == _frameGun; tries++) next = _gunPool[Ctx.Rng.Next(_gunPool.Count)];
                _frameGun = next;
                Announce("Frame " + (_frame + 1) + ": " + _frameGun.DisplayName, new Color(1f, 0.8f, 0.3f), 2f);
            }
            BeginTurn();
        }

        public override bool CanFire(Actor a)
        {
            return State == MinigameState.Playing && a == _active && _phase == Phase.Aim;
        }

        public override void HandleActorDied(Actor victim, Actor killer)
        {
            ScheduleRespawn(victim, 1f);
        }

        public override void DriveBot(BotBrain bot)
        {
            Actor self = bot.Self;
            if (self != _active)
            {
                // Mill about behind the line and watch
                bot.MoveTarget = SpawnPoints[Ctx.Actors.IndexOf(self) % SpawnPoints.Count];
                bot.AimPoint = new Vector3(0f, 0.5f, PinZ);
                bot.AllowBhop = false;
                return;
            }
            bot.AllowBhop = false;
            if (_phase != Phase.Aim) { bot.AimPoint = new Vector3(0f, 0.4f, PinZ); return; }
            // Aim at the centroid of standing pins, with a bit of human wobble
            Vector3 c = Vector3.zero;
            int n = 0;
            for (int i = 0; i < _pins.Count; i++)
            {
                PhysicsProp p = _pins[i];
                if (p == null || p.transform.up.y < 0.8f) continue;
                c += p.transform.position;
                n++;
            }
            c = n > 0 ? c / n : new Vector3(0f, 0.4f, PinZ);
            WeaponDefinition d = _frameGun;
            float lob = d.Mode == FireMode.Projectile && d.ProjectileGravity > 0.2f ? 0.08f * Vector3.Distance(self.EyePosition, c) * d.ProjectileGravity : 0f;
            if (d.PhysicalProjectile) lob = 0f;
            bot.AimPoint = c + Vector3.up * (lob - 0.1f) + new Vector3(Mathf.Sin(Time.time * 1.3f + bot.Skill * 7f) * 0.3f * (1f - bot.Skill), 0f, 0f);
            bot.WantFire = _phaseTimer < AimTime - 1.2f; // take a breath first
            bot.AimActor = null;
        }

        public override void DrawHUD(Hud hud)
        {
            float s = hud.Scale;
            if (_active != null && State == MinigameState.Playing)
            {
                string ph = _phase == Phase.Aim ? "AIM  " + Mathf.CeilToInt(_phaseTimer) + "s" : _phase == Phase.Settle ? "settling..." : "";
                hud.Label(new Rect(0, 50 * s, Screen.width, 34 * s), "Frame " + (_frame + 1) + "/" + Frames + "   " + _active.DisplayName + "'s turn   " + ph,
                    22, _active.Color, TextAnchor.UpperCenter);
                WeaponInstance w = _active.Weapons.Current;
                if (w != null && _phase == Phase.Aim)
                    hud.Label(new Rect(0, 80 * s, Screen.width, 26 * s), "Shots left: " + Mathf.Max(0, w.Magazine), 18, Color.white, TextAnchor.UpperCenter);
                if (_active == Ctx.LocalActor && _phase == Phase.Aim)
                    hud.Label(new Rect(0, Screen.height * 0.7f, Screen.width, 30 * s), "YOUR TURN - knock 'em down!", 24, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
            }
            // Mini scoreboard
            float y = 120 * s;
            hud.Panel(new Rect(Screen.width - 300 * s, y - 6 * s, 280 * s, (Ctx.Actors.Count * 24 + 12) * s));
            foreach (Actor a in Ctx.Actors)
            {
                string frames = string.Join(" ", _frameScores[a].ConvertAll(x => x.ToString()).ToArray());
                hud.Label(new Rect(Screen.width - 290 * s, y, 260 * s, 24 * s), a.DisplayName + "  " + GetScore(a) + "  <size=" + Mathf.RoundToInt(12 * s) + ">[" + frames + "]</size>", 15,
                    a == _active ? a.Color : Color.white);
                y += 24 * s;
            }
        }
    }
}
