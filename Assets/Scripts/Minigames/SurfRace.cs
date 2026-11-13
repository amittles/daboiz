using System.Collections.Generic;
using Daboiz.Actors;
using Daboiz.AI;
using Daboiz.Combat;
using Daboiz.Core;
using Daboiz.UI;
using Daboiz.Weapons;
using UnityEngine;

namespace Daboiz.Minigames
{
    /// <summary>
    /// CS-surf style race: hold strafe INTO a steep ramp to ride it, keep momentum between ramps,
    /// checkpoints respawn you when you fall, 2 laps. Guns shove/impair rivals; pickups buff you.
    /// </summary>
    public class SurfRace : MinigameBase
    {
        public override string Title { get { return "Surf Race PVP"; } }
        public override string Tagline { get { return "Surf the ramps. Shove your friends off them."; } }
        public override string HowToPlay
        {
            get
            {
                return "Drop onto a ramp and HOLD A or D toward the ramp (don't hold W) to surf along it.\n" +
                       "Turn with the mouse while strafing in the air to steer onto the next ramp.\n" +
                       "Fall = back to checkpoint. Pickups: speed, low-grav, shield, ammo. " + Laps + " laps.";
            }
        }

        private class SegmentDef
        {
            public Vector3 RidgeStart;
            public float Length;
            public float Pitch;
            public Vector3 RidgeAt(float z)
            {
                float t = Mathf.Clamp((z - RidgeStart.z) / (Length * Mathf.Cos(Pitch * Mathf.Deg2Rad)), 0f, 1f);
                return RidgeStart + new Vector3(0f, -Mathf.Sin(Pitch * Mathf.Deg2Rad), Mathf.Cos(Pitch * Mathf.Deg2Rad)) * (Length * t);
            }
            public float EndZ { get { return RidgeStart.z + Length * Mathf.Cos(Pitch * Mathf.Deg2Rad); } }
        }

        private enum PickupType { Speed, LowGravity, Shield, Ammo }

        private class Pickup
        {
            public PickupType Type;
            public Vector3 Pos;
            public Transform Visual;
            public float RespawnAt;
        }

        private class Racer
        {
            public int Lap;
            public int Checkpoint;
            public bool Finished;
            public float FinishTime;
        }

        public int Laps = 2;
        private const float FaceWidth = 9f;
        private const float SlopeAngle = 58f;
        private readonly List<SegmentDef> _segments = new List<SegmentDef>();
        private readonly List<Pickup> _pickups = new List<Pickup>();
        private readonly Dictionary<Actor, Racer> _racers = new Dictionary<Actor, Racer>();
        private readonly List<Actor> _finishOrder = new List<Actor>();
        private Vector3 _startPos = new Vector3(0f, 80.1f, -10f);
        private float _finishZ;
        private float _finishY;
        private float _raceStart;

        public SurfRace()
        {
            RoundDuration = 210f;
            KillY = -1000f; // handled per checkpoint
            Rules = new GameRules
            {
                DamageEnabled = false, KnockbackEnabled = true, EffectsEnabled = true, KnockbackScale = 1.3f, RespawnOnDeath = false
            };
        }

        protected override void Build()
        {
            Transform root = Ctx.Root;
            LevelKit.Environment(new Color(0.5f, 0.7f, 1f), new Color(0.6f, 0.65f, 0.75f), new Color(0.2f, 0.25f, 0.35f), true,
                new Color(0.6f, 0.75f, 0.95f), 0.0035f);

            Prims.Box(root, "StartPlatform", new Vector3(0f, 79.5f, -9f), new Vector3(14f, 1f, 14f), new Color(0.9f, 0.9f, 0.95f));
            Prims.Box(root, "StartWall", new Vector3(0f, 81.5f, -16.2f), new Vector3(14f, 4f, 0.5f), new Color(0.7f, 0.7f, 0.8f));
            Prims.Make(PrimitiveType.Cube, root, "StartLine", new Vector3(0f, 80.01f, -2.2f), new Vector3(14f, 0.02f, 0.3f), Color.white, false);

            AddSegment(new Vector3(0f, 77f, -6f), 64f, 7f);
            AddSegment(new Vector3(5f, 65f, 70f), 62f, 7f);
            AddSegment(new Vector3(-3f, 52f, 147f), 70f, 7f);
            AddSegment(new Vector3(4f, 37f, 232f), 62f, 7f);

            Color[] cols = { new Color(0.25f, 0.65f, 1f), new Color(1f, 0.45f, 0.65f), new Color(0.45f, 0.95f, 0.55f), new Color(1f, 0.75f, 0.25f) };
            for (int i = 0; i < _segments.Count; i++)
            {
                SegmentDef s = _segments[i];
                LevelKit.SurfRamp(root, s.RidgeStart, 0f, s.Length, FaceWidth, SlopeAngle, s.Pitch, cols[i % cols.Length]);
                // Pickups on alternating faces, mid-ramp
                for (int k = 0; k < 2; k++)
                {
                    float z = s.RidgeStart.z + s.Length * (0.35f + 0.35f * k);
                    Vector3 ridge = s.RidgeAt(z);
                    float side = (i + k) % 2 == 0 ? -1f : 1f;
                    float d = FaceWidth * 0.45f;
                    Vector3 p = ridge + new Vector3(side * d * Mathf.Cos(SlopeAngle * Mathf.Deg2Rad), -d * Mathf.Sin(SlopeAngle * Mathf.Deg2Rad) + 1.2f, 0f);
                    AddPickup(root, p, (PickupType)((i * 2 + k) % 4));
                }
            }

            SegmentDef last = _segments[_segments.Count - 1];
            _finishZ = last.EndZ + 14f;
            _finishY = last.RidgeAt(last.EndZ).y - 10f;
            Prims.Box(root, "FinishPad", new Vector3(0f, _finishY - 0.5f, _finishZ + 10f), new Vector3(40f, 1f, 36f), new Color(0.95f, 0.95f, 0.95f));
            Prims.Make(PrimitiveType.Cube, root, "FinishLine", new Vector3(0f, _finishY + 0.01f, _finishZ), new Vector3(40f, 0.02f, 1f), Color.black, false);
            Prims.Box(root, "FinishArchL", new Vector3(-20f, _finishY + 4f, _finishZ), new Vector3(1f, 8f, 1f), new Color(1f, 0.3f, 0.3f));
            Prims.Box(root, "FinishArchR", new Vector3(20f, _finishY + 4f, _finishZ), new Vector3(1f, 8f, 1f), new Color(1f, 0.3f, 0.3f));
            Prims.Make(PrimitiveType.Cube, root, "FinishArchTop", new Vector3(0f, _finishY + 8.5f, _finishZ), new Vector3(41f, 1f, 1f), new Color(1f, 0.3f, 0.3f), false);
            // Catch-all safety net far below (re-routes to checkpoint via kill height anyway)
            for (int i = 0; i < 6; i++)
            {
                SpawnPoints.Add(_startPos + new Vector3(-5f + i * 2f, 0f, 0f));
                SpawnYaws.Add(0f);
            }
            WeaponDefinition shove = WeaponRegistry.Get("shove_shotgun");
            WeaponDefinition freeze = Limited("brain_freeze", 10, 10);
            WeaponDefinition mirror = Limited("mirror", 10, 10);
            WeaponDefinition bubbles = Limited("bubble_blaster", 20, 20);
            SpawnAllActors(p => new List<WeaponDefinition> { shove, freeze, mirror, bubbles });
            foreach (Actor a in Ctx.Actors) _racers[a] = new Racer();
            SpectatorPoint = new Vector3(40f, 90f, 60f);
            SpectatorLookAt = new Vector3(0f, 60f, 100f);
        }

        private static WeaponDefinition Limited(string id, int mag, int reserve)
        {
            WeaponDefinition d = WeaponRegistry.Get(id).Clone();
            d.MagazineSize = mag;
            d.ReserveAmmo = reserve;
            return d;
        }

        private void AddSegment(Vector3 ridgeStart, float length, float pitch)
        {
            _segments.Add(new SegmentDef { RidgeStart = ridgeStart, Length = length, Pitch = pitch });
        }

        private void AddPickup(Transform root, Vector3 pos, PickupType type)
        {
            Color c;
            switch (type)
            {
                case PickupType.Speed: c = new Color(1f, 0.9f, 0.2f); break;
                case PickupType.LowGravity: c = new Color(0.7f, 0.5f, 1f); break;
                case PickupType.Shield: c = new Color(0.3f, 0.9f, 1f); break;
                default: c = new Color(1f, 0.5f, 0.2f); break;
            }
            GameObject g = Prims.Make(PrimitiveType.Cube, root, "Pickup_" + type, pos, Vector3.one * 0.8f, c, false, Quaternion.Euler(45f, 0f, 45f));
            g.GetComponent<Renderer>().sharedMaterial = Mats.Emissive(c, 1.5f);
            _pickups.Add(new Pickup { Type = type, Pos = pos, Visual = g.transform });
        }

        protected override void OnRoundStart()
        {
            _raceStart = Time.time;
            Announce("GO! GO! GO!", Color.white, 1.5f);
        }

        private Vector3 CheckpointPos(int cp, out float y)
        {
            if (cp <= 0) { y = _startPos.y; return _startPos; }
            SegmentDef s = _segments[cp];
            Vector3 p = s.RidgeAt(s.RidgeStart.z + 4f) + Vector3.up * 2f;
            y = p.y;
            return p;
        }

        private float CurrentKillHeight(Racer r)
        {
            // Below the current segment's lowest face edge = fell off.
            int idx = Mathf.Clamp(r.Checkpoint, 0, _segments.Count - 1);
            SegmentDef s = _segments[idx];
            float low = s.RidgeAt(s.EndZ).y - FaceWidth * Mathf.Sin(SlopeAngle * Mathf.Deg2Rad);
            if (idx + 1 < _segments.Count)
            {
                SegmentDef n = _segments[idx + 1];
                low = Mathf.Min(low, n.RidgeAt(n.EndZ).y - FaceWidth);
            }
            else low = Mathf.Min(low, _finishY);
            return low - 12f;
        }

        protected override void OnTick(float dt)
        {
            foreach (Pickup p in _pickups)
            {
                bool active = Time.time >= p.RespawnAt;
                p.Visual.gameObject.SetActive(active);
                if (!active) continue;
                p.Visual.Rotate(0f, 120f * dt, 0f, Space.World);
                p.Visual.position = p.Pos + Vector3.up * Mathf.Sin(Time.time * 3f + p.Pos.z) * 0.25f;
                foreach (Actor a in Ctx.Actors)
                {
                    if (!a.IsAlive || (a.CenterPosition - p.Pos).sqrMagnitude > 2.4f * 2.4f) continue;
                    Collect(a, p);
                    break;
                }
            }

            foreach (Actor a in Ctx.Actors)
            {
                Racer r = _racers[a];
                if (r.Finished || !a.IsAlive) continue;
                Vector3 pos = a.transform.position;

                // Checkpoint progress
                while (r.Checkpoint + 1 < _segments.Count)
                {
                    SegmentDef next = _segments[r.Checkpoint + 1];
                    if (pos.z >= next.RidgeStart.z + 2f && pos.y > next.RidgeAt(pos.z).y - FaceWidth - 3f) r.Checkpoint++;
                    else break;
                }

                // Finish / lap
                if (pos.z >= _finishZ && pos.y > _finishY - 2f && pos.y < _finishY + 12f)
                {
                    r.Lap++;
                    if (r.Lap >= Laps)
                    {
                        r.Finished = true;
                        r.FinishTime = Time.time - _raceStart;
                        _finishOrder.Add(a);
                        Feed(a.DisplayName + " finished " + Ordinal(_finishOrder.Count) + " (" + r.FinishTime.ToString("0.0") + "s)");
                        Fx.Confetti(a.transform.position + Vector3.up * 2f, 50);
                        ProceduralAudio.Play(Sfx.Ding, a.transform.position, 1f, 1.2f);
                        a.Weapons.Holstered = true;
                    }
                    else
                    {
                        Feed(a.DisplayName + " - lap " + (r.Lap + 1) + "/" + Laps);
                        r.Checkpoint = 0;
                        a.TeleportTo(_startPos + new Vector3(Random.Range(-4f, 4f), 0f, 0f), 0f);
                        a.Weapons.RefillAll();
                    }
                    continue;
                }

                if (pos.y < CurrentKillHeight(r)) RespawnAtCheckpoint(a, r);
            }

            if (_finishOrder.Count == Ctx.Actors.Count) Finish();
        }

        private void RespawnAtCheckpoint(Actor a, Racer r)
        {
            float y;
            Vector3 p = CheckpointPos(r.Checkpoint, out y);
            a.TeleportTo(p, 0f);
            if (r.Checkpoint > 0) a.Motor.Velocity = new Vector3(0f, 0f, 14f);
            ProceduralAudio.Play(Sfx.Whoosh, p, 0.6f, 0.9f);
        }

        private void Collect(Actor a, Pickup p)
        {
            p.RespawnAt = Time.time + 8f;
            ProceduralAudio.Play(Sfx.Ding, p.Pos, 0.8f, 1.6f);
            Fx.Burst(p.Pos, Color.white, 10, 4f, 0.12f);
            switch (p.Type)
            {
                case PickupType.Speed:
                    Vector3 v = a.Motor.Velocity;
                    Vector3 dir = new Vector3(v.x, 0f, v.z);
                    dir = dir.sqrMagnitude > 1f ? dir.normalized : Vector3.forward;
                    a.Motor.AddImpulse(dir * 9f);
                    a.Status.Apply(StatusType.SpeedBoost, 3f, 0.6f);
                    break;
                case PickupType.LowGravity: a.Status.Apply(StatusType.LowGravity, 6f); break;
                case PickupType.Shield: a.Status.Apply(StatusType.Shield, 6f); break;
                case PickupType.Ammo: a.Weapons.RefillAll(); break;
            }
            if (a.IsLocalPlayer) Announce(p.Type == PickupType.Ammo ? "AMMO!" : p.Type.ToString().ToUpper() + "!", Color.white, 1f);
        }

        public override void HandleActorDied(Actor victim, Actor killer) { }

        protected override void OnTimeUp() { Finish(); }

        private float ProgressScore(Actor a)
        {
            Racer r = _racers[a];
            return r.Lap * 10000f + r.Checkpoint * 1000f + a.transform.position.z;
        }

        private void Finish()
        {
            List<RankEntry> res = new List<RankEntry>();
            int place = 1;
            foreach (Actor a in _finishOrder) res.Add(new RankEntry(a.Participant, place++, _racers[a].FinishTime.ToString("0.0") + "s"));
            List<Actor> rest = new List<Actor>();
            foreach (Actor a in Ctx.Actors) if (!_racers[a].Finished) rest.Add(a);
            rest.Sort((x, y) => ProgressScore(y).CompareTo(ProgressScore(x)));
            foreach (Actor a in rest)
                res.Add(new RankEntry(a.Participant, place++, "lap " + (_racers[a].Lap + 1) + ", ramp " + (_racers[a].Checkpoint + 1)));
            EndRound(res, _finishOrder.Count > 0 ? _finishOrder[0].DisplayName + " wins the surf race!" : "Time's up!");
        }

        public override void DriveBot(BotBrain bot)
        {
            Actor self = bot.Self;
            Racer r = _racers[self];
            if (r.Finished) { bot.HoldStill = true; return; }
            Vector3 pos = self.transform.position;
            bot.AllowBhop = false;
            bot.DesiredYaw = 0f;

            // Which ramp are we over?
            int seg = -1;
            for (int i = 0; i < _segments.Count; i++)
                if (pos.z >= _segments[i].RidgeStart.z - 2f && pos.z <= _segments[i].EndZ + 1f) seg = i;

            if (pos.y > 79.5f && pos.z < -1f)
            {
                // On the start platform: run off the edge
                bot.WorldMove = Vector3.forward;
            }
            else if (seg >= 0)
            {
                SegmentDef s = _segments[seg];
                Vector3 ridge = s.RidgeAt(pos.z);
                bool onRamp = self.Motor.Surfing || pos.y < ridge.y + 0.5f;
                float side = pos.x < ridge.x ? 1f : -1f; // push toward the ridge = into the face
                if (onRamp)
                {
                    bot.WorldMove = new Vector3(side, 0f, 0f);
                    // Rubber-band: bots get a little help to keep pace
                    if (self.Motor.Velocity.z < 16f) self.Motor.Velocity.z += 5f * Time.deltaTime;
                }
                else
                {
                    // Falling toward this ramp: steer to land on a face, not the ridge or the void
                    float targetX = ridge.x + (Mathf.Abs(pos.x - ridge.x) < 1f ? (bot.Skill > 0.5f ? 2.5f : -2.5f) : Mathf.Sign(pos.x - ridge.x) * 2.5f);
                    bot.WorldMove = new Vector3(Mathf.Clamp(targetX - pos.x, -1f, 1f), 0f, 0f);
                }
            }
            else
            {
                // Between ramps / heading to finish: steer toward next ramp's entry
                SegmentDef next = null;
                for (int i = 0; i < _segments.Count; i++) if (_segments[i].RidgeStart.z > pos.z) { next = _segments[i]; break; }
                float tx = next != null ? next.RidgeStart.x + 2.5f : 0f;
                bot.WorldMove = new Vector3(Mathf.Clamp(tx - pos.x, -1f, 1f), 0f, 0.2f);
                if (self.Motor.Velocity.z < 12f) self.Motor.Velocity.z += 4f * Time.deltaTime;
            }

            // Shoot rivals ahead of us
            Actor target = null;
            float best = 28f;
            foreach (Actor a in Ctx.Actors)
            {
                if (a == self || !a.IsAlive || _racers[a].Finished) continue;
                Vector3 to = a.transform.position - pos;
                if (to.z < -2f || to.magnitude > best) continue;
                if (!BotBrain.CanSee(self, a)) continue;
                best = to.magnitude;
                target = a;
            }
            if (target != null)
            {
                bot.AimActor = target;
                bot.WantFire = true;
                if (Time.time > bot.Timer2)
                {
                    bot.Timer2 = Time.time + Random.Range(5f, 10f);
                    bot.WeaponSlot = Random.Range(0, self.Weapons.Inventory.Count);
                }
            }
        }

        public override void DrawHUD(Hud hud)
        {
            float s = hud.Scale;
            Actor local = Ctx.LocalActor;
            if (local != null)
            {
                Racer r = _racers[local];
                int pos = 1;
                foreach (Actor a in Ctx.Actors) if (a != local && ProgressScore(a) > ProgressScore(local)) pos++;
                hud.Label(new Rect(20 * s, 46 * s, 600 * s, 30 * s),
                    (r.Finished ? "FINISHED" : "Lap " + (r.Lap + 1) + "/" + Laps + "   Ramp " + (r.Checkpoint + 1) + "/" + _segments.Count) +
                    "   Position " + Ordinal(pos), 20, Color.white);
                if (State == MinigameState.Playing && Time.time - _raceStart < 6f)
                    hud.Label(new Rect(0, Screen.height * 0.7f, Screen.width, 30 * s), "Hold A or D INTO the ramp to surf. Don't hold W.",
                        20, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter);
            }
            foreach (Pickup p in _pickups)
                if (Time.time >= p.RespawnAt) hud.WorldLabel(p.Pos + Vector3.up * 1.1f, p.Type.ToString(), Color.white, 12, 40f);
        }

        public override IEnumerable<string> ScoreLines()
        {
            List<Actor> sorted = new List<Actor>(Ctx.Actors);
            sorted.Sort((x, y) => ProgressScore(y).CompareTo(ProgressScore(x)));
            foreach (Actor a in sorted)
            {
                Racer r = _racers[a];
                yield return a.DisplayName + "   " + (r.Finished ? "FINISHED " + r.FinishTime.ToString("0.0") + "s" : "lap " + (r.Lap + 1) + " ramp " + (r.Checkpoint + 1));
            }
        }
    }
}
