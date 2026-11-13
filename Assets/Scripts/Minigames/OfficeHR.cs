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
    /// Office HR - short-form social deduction. One player is secretly HR and carries a concealed
    /// one-shot Termination Stapler (long cooldown). Workers have no weapons and must finish their
    /// task stations (hold E) or vote HR out in a meeting (report a body or press the emergency
    /// button in the break room). HR wins by thinning the workforce or running out the clock.
    /// </summary>
    public class OfficeHR : MinigameBase
    {
        public override string Title { get { return "Office HR"; } }
        public override string Tagline { get { return "Someone in this office is HR. Do your tasks. Trust nobody."; } }
        public override string HowToPlay
        {
            get
            {
                return "WORKER: hold E at your task stations. Report bodies (E) or hit the emergency button in the break room, then vote.\n" +
                       "HR: you secretly hold a one-shot stapler (LMB, long cooldown). Fire people without being seen.";
            }
        }

        public override bool WantsCursor
        {
            get { return State == MinigameState.Playing && (_phase == Phase.Meeting || _phase == Phase.Reveal); }
        }

        private enum Phase { Roam, Meeting, Reveal }

        private class Station
        {
            public string Name;
            public string Room;
            public Vector3 Pos;     // interaction point (floor level)
            public float Duration;
            public Renderer Light;
        }

        private class TaskAssign
        {
            public int Station;
            public bool Done;
            public float Progress;
        }

        private class Body
        {
            public Actor Victim;
            public Vector3 Pos;
            public float Time;
            public bool Reported;
        }

        private struct Sighting
        {
            public Actor Who;
            public Vector3 Pos;
            public float Time;
        }

        private const float KillCooldown = 18f;
        private const float FirstKillDelay = 12f;
        private const float MeetingDuration = 22f;
        private const float RevealDuration = 4.5f;
        private const float StationRange = 1.7f;
        private const float ReportRange = 2.6f;
        private const float ButtonRange = 2.2f;
        private const int TasksPerWorker = 3;

        private readonly List<Station> _stations = new List<Station>();
        private readonly Dictionary<Actor, List<TaskAssign>> _tasks = new Dictionary<Actor, List<TaskAssign>>();
        private readonly List<Body> _bodies = new List<Body>();
        private readonly Dictionary<Actor, int> _buttonUses = new Dictionary<Actor, int>();
        private readonly Dictionary<Actor, Actor> _votes = new Dictionary<Actor, Actor>(); // value null = skip
        private readonly Dictionary<Actor, float> _botVoteAt = new Dictionary<Actor, float>();
        private readonly Dictionary<Actor, List<Sighting>> _sightings = new Dictionary<Actor, List<Sighting>>();
        private readonly Dictionary<Actor, Dictionary<Actor, float>> _suspicion = new Dictionary<Actor, Dictionary<Actor, float>>();

        private Actor _hr;
        private Phase _phase = Phase.Roam;
        private float _phaseTimer;
        private float _hrReadyAt;
        private float _sightTimer;
        private int _tasksRequired;
        private int _tasksDone;
        private bool _ejecting;
        private Actor _ejected;
        private string _meetingReason = "";
        private string _revealText = "";
        private Vector3 _buttonPos;
        private Vector3 _tableCenter = new Vector3(3f, 0f, 7.4f);

        // Room graph for bot navigation (doors in interior walls).
        private static readonly Vector3[] NavNodes =
        {
            new Vector3(-11f, 0f, 0f),  // 0 office centre
            new Vector3(-4f, 0f, 0f),   // 1 door office<->hall
            new Vector3(-4f, 0f, 10.4f), // 2 door office<->break
            new Vector3(-4f, 0f, -8f),  // 3 door office<->records
            new Vector3(7f, 0f, 0f),    // 4 hall centre
            new Vector3(7f, 0f, 4f),    // 5 door hall<->break
            new Vector3(7f, 0f, -4f),   // 6 door hall<->records
            new Vector3(10f, 0f, 8f),   // 7 break centre
            new Vector3(9f, 0f, -8f),   // 8 records centre
        };
        private static readonly int[][] NavEdges =
        {
            new[] { 1, 2, 3 }, new[] { 0, 4 }, new[] { 0, 7 }, new[] { 0, 8 },
            new[] { 1, 5, 6 }, new[] { 4, 7 }, new[] { 4, 8 }, new[] { 5, 2 }, new[] { 6, 3 },
        };

        public OfficeHR()
        {
            RoundDuration = 240f;
            IntroDuration = 6f;
            KillY = -20f;
            Rules = new GameRules
            {
                DamageEnabled = true,
                KnockbackEnabled = true,
                KnockbackScale = 0.4f,
                RespawnOnDeath = false,
                ShowNametags = true,
            };
            SpectatorPoint = new Vector3(0f, 26f, -20f);
            SpectatorLookAt = new Vector3(0f, 0f, 0f);
        }

        // =================================================================== build

        protected override void Build()
        {
            Transform root = Ctx.Root;
            LevelKit.Environment(new Color(0.75f, 0.78f, 0.82f), new Color(0.55f, 0.55f, 0.58f), new Color(0.3f, 0.28f, 0.26f), false, Color.gray);
            LevelKit.Sun(root, new Vector3(55f, 30f, 0f), 0.6f, new Color(1f, 0.97f, 0.9f));

            Color carpet = new Color(0.32f, 0.36f, 0.45f);
            Color carpet2 = new Color(0.36f, 0.40f, 0.49f);
            Color wall = new Color(0.86f, 0.84f, 0.78f);
            Color trim = new Color(0.55f, 0.52f, 0.48f);
            const float H = 3.2f, T = 0.3f;

            // Floors: office carpet (checker), linoleum elsewhere
            LevelKit.CheckerFloor(root, new Vector3(-11f, 0f, 0f), 7, 12, 2f, carpet, carpet2);
            LevelKit.Floor(root, new Vector3(7f, 0f, 8f), new Vector2(22f, 8f), new Color(0.78f, 0.74f, 0.62f));
            LevelKit.Floor(root, new Vector3(7f, 0f, 0f), new Vector2(22f, 8f), new Color(0.6f, 0.6f, 0.62f));
            LevelKit.Floor(root, new Vector3(7f, 0f, -8f), new Vector2(22f, 8f), new Color(0.5f, 0.47f, 0.42f));
            // Outside lawn so falling out of a window isn't a void
            LevelKit.Floor(root, new Vector3(0f, -0.05f, 0f), new Vector2(90f, 80f), new Color(0.35f, 0.55f, 0.3f), 0.5f);
            // Ceiling (thin, no shadows so the inside stays lit by ambient + lamps)
            GameObject ceil = Prims.Box(root, "Ceiling", new Vector3(0f, H + 0.1f, 0f), new Vector3(36.6f, 0.2f, 24.6f), new Color(0.92f, 0.92f, 0.9f));
            Prims.NoShadows(ceil);

            // Outer walls
            LevelKit.Wall(root, new Vector3(-18f, 0f, -12f), new Vector3(18f, 0f, -12f), H, T, wall);
            LevelKit.Wall(root, new Vector3(-18f, 0f, 12f), new Vector3(18f, 0f, 12f), H, T, wall);
            LevelKit.Wall(root, new Vector3(-18f, 0f, -12f), new Vector3(-18f, 0f, 12f), H, T, wall);
            LevelKit.Wall(root, new Vector3(18f, 0f, -12f), new Vector3(18f, 0f, 12f), H, T, wall);

            // Interior wall x=-4 with three doors (z = -8, 0, 10.4)
            float[] zs = { -12f, -9.2f, -6.8f, -1.2f, 1.2f, 9.2f, 11.6f, 12f };
            for (int i = 0; i < zs.Length; i += 2)
                LevelKit.Wall(root, new Vector3(-4f, 0f, zs[i]), new Vector3(-4f, 0f, zs[i + 1]), H, T, trim);
            // Interior walls z=+4 and z=-4 (east side), door at x = 7
            LevelKit.Wall(root, new Vector3(-4f, 0f, 4f), new Vector3(5.8f, 0f, 4f), H, T, trim);
            LevelKit.Wall(root, new Vector3(8.2f, 0f, 4f), new Vector3(18f, 0f, 4f), H, T, trim);
            LevelKit.Wall(root, new Vector3(-4f, 0f, -4f), new Vector3(5.8f, 0f, -4f), H, T, trim);
            LevelKit.Wall(root, new Vector3(8.2f, 0f, -4f), new Vector3(18f, 0f, -4f), H, T, trim);
            // Door frames (lintels) for readability
            Lintel(root, new Vector3(-4f, 0f, 0f), true);
            Lintel(root, new Vector3(-4f, 0f, 10.4f), true);
            Lintel(root, new Vector3(-4f, 0f, -8f), true);
            Lintel(root, new Vector3(7f, 0f, 4f), false);
            Lintel(root, new Vector3(7f, 0f, -4f), false);

            // Lights
            LevelKit.Lamp(root, new Vector3(-11f, 2.9f, -6f), new Color(1f, 0.97f, 0.88f), 12f, 1.1f);
            LevelKit.Lamp(root, new Vector3(-11f, 2.9f, 6f), new Color(1f, 0.97f, 0.88f), 12f, 1.1f);
            LevelKit.Lamp(root, new Vector3(9f, 2.9f, 8f), new Color(1f, 0.92f, 0.8f), 13f, 1.1f);
            LevelKit.Lamp(root, new Vector3(7f, 2.9f, 0f), new Color(0.9f, 0.95f, 1f), 13f, 1f);
            LevelKit.Lamp(root, new Vector3(9f, 2.9f, -8f), new Color(0.85f, 1f, 0.9f), 13f, 0.9f);

            // --- Open office: desks + monitors + chairs + plants
            Color desk = new Color(0.62f, 0.48f, 0.32f);
            float[] dx = { -14.5f, -8.5f };
            float[] dz = { -7.5f, 0f, 7.5f };
            foreach (float x in dx)
                foreach (float z in dz)
                {
                    Prims.Box(root, "Desk", new Vector3(x, 0.4f, z), new Vector3(3f, 0.8f, 1.2f), desk);
                    Prims.Make(PrimitiveType.Cube, root, "Monitor", new Vector3(x - 0.6f, 1.1f, z + 0.25f), new Vector3(0.7f, 0.45f, 0.06f), new Color(0.1f, 0.1f, 0.12f), false);
                    Prims.Make(PrimitiveType.Cube, root, "Monitor", new Vector3(x + 0.6f, 1.1f, z + 0.25f), new Vector3(0.7f, 0.45f, 0.06f), new Color(0.1f, 0.1f, 0.12f), false);
                    PhysicsProp.Create(PropKind.Chair, root, new Vector3(x - 0.6f, 0.5f, z - 1.2f), Quaternion.identity);
                    PhysicsProp.Create(PropKind.Can, root, new Vector3(x + 1.1f, 0.95f, z - 0.2f), Quaternion.identity);
                }
            Plant(root, new Vector3(-17f, 0f, -11f));
            Plant(root, new Vector3(-17f, 0f, 11f));
            Plant(root, new Vector3(17f, 0f, -1.5f));

            // --- Break room: table, emergency button, fridge, counter
            Prims.Make(PrimitiveType.Cylinder, root, "Table", _tableCenter + Vector3.up * 0.4f, new Vector3(2.4f, 0.4f, 2.4f), new Color(0.85f, 0.85f, 0.88f));
            _buttonPos = _tableCenter + Vector3.up * 0.85f;
            GameObject btn = Prims.Make(PrimitiveType.Cylinder, root, "EmergencyButton", _buttonPos, new Vector3(0.6f, 0.06f, 0.6f), Color.red, false);
            Prims.SetMaterial(btn, Mats.Emissive(new Color(1f, 0.1f, 0.1f), 1.2f));
            Prims.Box(root, "Counter", new Vector3(12f, 0.5f, 11.4f), new Vector3(10f, 1f, 1f), new Color(0.9f, 0.9f, 0.9f));
            Prims.Box(root, "Fridge", new Vector3(17.3f, 1f, 6.5f), new Vector3(1f, 2f, 1.2f), new Color(0.95f, 0.95f, 0.97f));

            // --- Hall / copy room
            Prims.Box(root, "Printer", new Vector3(15f, 0.6f, 2.8f), new Vector3(1.6f, 1.2f, 1f), new Color(0.82f, 0.82f, 0.8f));
            Prims.Box(root, "Copier", new Vector3(1.5f, 0.65f, 3.2f), new Vector3(1.8f, 1.3f, 1f), new Color(0.7f, 0.7f, 0.72f));
            Prims.Make(PrimitiveType.Cylinder, root, "WaterCooler", new Vector3(-2.8f, 0.7f, -3.1f), new Vector3(0.5f, 0.7f, 0.5f), new Color(0.6f, 0.8f, 1f));
            PhysicsProp.Create(PropKind.Crate, root, new Vector3(12f, 0.5f, -2.5f), Quaternion.Euler(0f, 20f, 0f));
            PhysicsProp.Create(PropKind.Crate, root, new Vector3(12.4f, 1.5f, -2.4f), Quaternion.Euler(0f, 5f, 0f));

            // --- Records room: shelves, server rack, shredder, mail table
            for (int i = 0; i < 3; i++)
                Prims.Box(root, "Shelf", new Vector3(-1f + i * 3.5f, 1.1f, -11.4f), new Vector3(2.6f, 2.2f, 0.8f), new Color(0.45f, 0.35f, 0.25f));
            Prims.Box(root, "ServerRack", new Vector3(17.2f, 1.1f, -9f), new Vector3(1.2f, 2.2f, 2f), new Color(0.12f, 0.12f, 0.14f));
            Prims.Box(root, "Shredder", new Vector3(14f, 0.45f, -11.3f), new Vector3(0.8f, 0.9f, 0.6f), new Color(0.25f, 0.25f, 0.28f));
            Prims.Box(root, "MailTable", new Vector3(5f, 0.45f, -7.5f), new Vector3(2.4f, 0.9f, 1.2f), desk);

            // --- Task stations (interaction point = where you stand)
            AddStation("Answer Emails", "Open Office", new Vector3(-13.4f, 0f, -8.9f), 4f);
            AddStation("Update Spreadsheet", "Open Office", new Vector3(-7.4f, 0f, 6.1f), 5f);
            AddStation("File Reports", "Open Office", new Vector3(-16.4f, 0f, 3.5f), 4f);
            AddStation("Water The Plant", "Open Office", new Vector3(-16.2f, 0f, 10f), 3f);
            AddStation("Make Coffee", "Break Room", new Vector3(14f, 0f, 10.4f), 5f);
            AddStation("Clean Microwave", "Break Room", new Vector3(9f, 0f, 10.4f), 4f);
            AddStation("Restock Fridge", "Break Room", new Vector3(16.1f, 0f, 6.9f), 3.5f);
            AddStation("Fix Printer", "Hallway", new Vector3(15f, 0f, 1.7f), 6f);
            AddStation("Unjam Copier", "Hallway", new Vector3(1.5f, 0f, 2.1f), 4.5f);
            AddStation("Refill Water Cooler", "Hallway", new Vector3(-2.6f, 0f, -2.2f), 3f);
            AddStation("Shred Documents", "Records", new Vector3(14f, 0f, -10.4f), 4f);
            AddStation("Reboot Server", "Records", new Vector3(16f, 0f, -7f), 6f);
            AddStation("Sort Mail", "Records", new Vector3(5f, 0f, -6.3f), 4f);
            // Filing cabinet visual for File Reports
            Prims.Box(root, "FilingCabinet", new Vector3(-17.4f, 0.7f, 3.5f), new Vector3(0.8f, 1.4f, 1f), new Color(0.5f, 0.5f, 0.55f));

            // --- Spawns: around the open office
            for (int i = 0; i < 6; i++)
            {
                SpawnPoints.Add(new Vector3(-11.5f + (i % 3) * 1.8f - 1.8f, 0.1f, i < 3 ? -3.5f : 3.5f));
                SpawnYaws.Add(i < 3 ? 0f : 180f);
            }

            // --- Roles
            int hrIndex = Ctx.Rng.Next(Ctx.Participants.Count);
            Participant hrP = Ctx.Participants[hrIndex];
            WeaponDefinition stapler = WeaponRegistry.Get("termination_stapler");
            SpawnAllActors(p => p == hrP && stapler != null ? new List<WeaponDefinition> { stapler } : null);

            foreach (Actor a in Ctx.Actors)
            {
                _buttonUses[a] = 1;
                _sightings[a] = new List<Sighting>();
                _suspicion[a] = new Dictionary<Actor, float>();
                if (a.Participant == hrP)
                {
                    _hr = a;
                    a.Weapons.Concealed = true;
                    a.Weapons.Fired += OnHrFired;
                    continue;
                }
                // Assign distinct tasks
                List<int> pool = new List<int>();
                for (int i = 0; i < _stations.Count; i++) pool.Add(i);
                List<TaskAssign> list = new List<TaskAssign>();
                for (int k = 0; k < TasksPerWorker && pool.Count > 0; k++)
                {
                    int pick = Ctx.Rng.Next(pool.Count);
                    list.Add(new TaskAssign { Station = pool[pick] });
                    pool.RemoveAt(pick);
                }
                _tasks[a] = list;
                _tasksRequired += list.Count;
            }
        }

        private void Lintel(Transform root, Vector3 doorCenter, bool alongZ)
        {
            Vector3 size = alongZ ? new Vector3(0.34f, 0.7f, 2.4f) : new Vector3(2.4f, 0.7f, 0.34f);
            Prims.Box(root, "Lintel", doorCenter + Vector3.up * 2.85f, size, new Color(0.4f, 0.36f, 0.3f));
        }

        private void Plant(Transform root, Vector3 pos)
        {
            Prims.Make(PrimitiveType.Cylinder, root, "Pot", pos + Vector3.up * 0.3f, new Vector3(0.6f, 0.3f, 0.6f), new Color(0.6f, 0.35f, 0.2f));
            Prims.Make(PrimitiveType.Sphere, root, "Leaves", pos + Vector3.up * 1.1f, new Vector3(1.1f, 1.2f, 1.1f), new Color(0.2f, 0.6f, 0.25f), false);
        }

        private void AddStation(string name, string room, Vector3 pos, float duration)
        {
            GameObject pad = Prims.Make(PrimitiveType.Cylinder, Ctx.Root, "Station_" + name, pos + Vector3.up * 0.02f,
                new Vector3(1.2f, 0.01f, 1.2f), Color.yellow, false);
            Prims.SetMaterial(pad, Mats.Emissive(new Color(1f, 0.85f, 0.2f), 0.6f));
            Prims.NoShadows(pad);
            _stations.Add(new Station { Name = name, Room = room, Pos = pos, Duration = duration, Light = pad.GetComponent<Renderer>() });
        }

        public override void Teardown()
        {
            if (_hr != null && _hr.Weapons != null) _hr.Weapons.Fired -= OnHrFired;
        }

        // =================================================================== flow

        protected override void OnIntroStart()
        {
            Actor local = Ctx.LocalActor;
            if (local == null) return;
            if (local == _hr) Announce("You are HR. Fire people quietly.", new Color(1f, 0.3f, 0.3f), 5f);
            else Announce("You are a Worker. Do your tasks. Find HR.", new Color(0.5f, 0.85f, 1f), 5f);
        }

        protected override void OnRoundStart()
        {
            _hrReadyAt = Time.time + FirstKillDelay;
            _phase = Phase.Roam;
        }

        protected override void OnTick(float dt)
        {
            switch (_phase)
            {
                case Phase.Roam: TickRoam(dt); break;
                case Phase.Meeting:
                    TimeLeft += dt; // meetings pause the work-day clock
                    TickMeeting(dt);
                    break;
                case Phase.Reveal:
                    TimeLeft += dt;
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) FinishReveal();
                    break;
            }
        }

        private void TickRoam(float dt)
        {
            // Witness bookkeeping for bot suspicion
            _sightTimer -= dt;
            if (_sightTimer <= 0f)
            {
                _sightTimer = 0.4f;
                RecordSightings();
            }

            foreach (Actor a in Ctx.Actors)
            {
                bool pressed = a.ConsumeInteractPressed();
                if (!a.IsAlive) continue;
                Vector3 pos = a.transform.position;

                if (pressed && a != _hr)
                {
                    Body b = BodyNear(pos, ReportRange);
                    if (b != null) { StartMeeting(a, b); return; }
                }
                if (pressed && FlatDist(pos, _buttonPos) < ButtonRange)
                {
                    if (_buttonUses[a] > 0)
                    {
                        _buttonUses[a]--;
                        StartMeeting(a, null);
                        return;
                    }
                    if (a.IsLocalPlayer) Announce("You already used your emergency meeting", Color.gray, 1.5f);
                }

                // Task progress (hold E inside a station you own)
                List<TaskAssign> list;
                if (!_tasks.TryGetValue(a, out list)) continue;
                bool holding = a.LastCommand.InteractHeld && !a.MovementLocked;
                foreach (TaskAssign t in list)
                {
                    if (t.Done) continue;
                    Station s = _stations[t.Station];
                    bool inRange = FlatDist(pos, s.Pos) < StationRange && Mathf.Abs(pos.y - s.Pos.y) < 1.5f;
                    if (holding && inRange)
                    {
                        t.Progress += dt;
                        if (t.Progress >= s.Duration)
                        {
                            t.Done = true;
                            _tasksDone++;
                            AddScore(a, 1);
                            ProceduralAudio.Play(Sfx.Ding, s.Pos + Vector3.up, 0.6f, 1.3f);
                            if (a.IsLocalPlayer) Announce("Task done: " + s.Name, new Color(0.5f, 1f, 0.5f), 1.5f);
                        }
                    }
                    else t.Progress = 0f; // Among Us rules: leave and you start over
                }
            }

            if (_tasksRequired > 0 && _tasksDone >= _tasksRequired)
            {
                WorkersWin("Every task is done. The office survives another quarter!");
                return;
            }
            CheckHrWin();
        }

        private void OnHrFired(WeaponInstance w)
        {
            _hrReadyAt = Time.time + KillCooldown;
            if (_hr == null) return;
            // Anyone who can see HR at that moment becomes very suspicious.
            foreach (Actor a in Ctx.Actors)
            {
                if (a == _hr || !a.IsAlive || a.IsLocalPlayer) continue;
                if (FlatDist(a.transform.position, _hr.transform.position) > 26f) continue;
                if (!BotBrain.CanSee(a, _hr)) continue;
                AddSuspicion(a, _hr, 120f);
            }
        }

        public override bool CanFire(Actor a)
        {
            return State == MinigameState.Playing && _phase == Phase.Roam && a == _hr && Time.time >= _hrReadyAt;
        }

        public override void HandleActorDied(Actor victim, Actor killer)
        {
            if (_ejecting) return; // ejection handled by the reveal
            if (State != MinigameState.Playing) return;
            Feed(victim.DisplayName + " was let go");
            _bodies.Add(new Body { Victim = victim, Pos = victim.transform.position, Time = Time.time });
            RemoveUnfinishedTasks(victim);
            if (victim == _hr) { WorkersWin("HR had an unfortunate accident. Workers win!"); return; }
            CheckHrWin();
        }

        protected override void OnActorFellOut(Actor a)
        {
            a.Die(null, false);
        }

        private void RemoveUnfinishedTasks(Actor a)
        {
            List<TaskAssign> list;
            if (!_tasks.TryGetValue(a, out list)) return;
            foreach (TaskAssign t in list) if (!t.Done) _tasksRequired--;
            list.RemoveAll(t => !t.Done);
        }

        private int AliveWorkers()
        {
            int n = 0;
            foreach (Actor a in Ctx.Actors) if (a != _hr && a.IsAlive) n++;
            return n;
        }

        private void CheckHrWin()
        {
            if (State != MinigameState.Playing) return;
            if (_hr != null && _hr.IsAlive && AliveWorkers() <= 1)
                HrWins("HR downsized the whole office. " + (_hr != null ? _hr.DisplayName : "HR") + " wins!");
        }

        protected override void OnTimeUp()
        {
            HrWins("5 o'clock! Deadlines missed - " + (_hr != null ? _hr.DisplayName : "HR") + " (HR) wins.");
        }

        private void WorkersWin(string headline)
        {
            List<RankEntry> r = new List<RankEntry>();
            foreach (Actor a in Ctx.Actors)
            {
                if (a == _hr) continue;
                int place = a.IsAlive ? 1 : 2;
                r.Add(new RankEntry(a.Participant, place, (a.IsAlive ? "Worker (survived)" : "Worker (fired)") + "  " + GetScore(a) + " tasks"));
            }
            r.Sort((x, y) => x.Place.CompareTo(y.Place));
            if (_hr != null) r.Add(new RankEntry(_hr.Participant, 3, "HR (caught)"));
            EndRound(r, headline);
        }

        private void HrWins(string headline)
        {
            List<RankEntry> r = new List<RankEntry>();
            if (_hr != null) r.Add(new RankEntry(_hr.Participant, 1, "HR - " + _hr.Kills + " firings"));
            foreach (Actor a in Ctx.Actors)
            {
                if (a == _hr) continue;
                r.Add(new RankEntry(a.Participant, a.IsAlive ? 2 : 3, (a.IsAlive ? "Worker" : "Worker (fired)") + "  " + GetScore(a) + " tasks"));
            }
            r.Sort((x, y) => x.Place.CompareTo(y.Place));
            EndRound(r, headline);
        }

        // =================================================================== meetings

        private void StartMeeting(Actor caller, Body body)
        {
            _phase = Phase.Meeting;
            _phaseTimer = MeetingDuration;
            _votes.Clear();
            _botVoteAt.Clear();
            _meetingReason = body != null
                ? caller.DisplayName + " found " + body.Victim.DisplayName + "'s body!"
                : caller.DisplayName + " called an emergency meeting!";
            ProceduralAudio.PlayUI(Sfx.Buzz, 0.9f, 0.8f);
            Announce(body != null ? "BODY REPORTED" : "EMERGENCY MEETING", new Color(1f, 0.3f, 0.25f), 2.5f);

            if (body != null) ApplyBodyEvidence(body);

            // Clear all bodies (Among Us style)
            foreach (Body b in _bodies)
            {
                b.Reported = true;
                if (b.Victim != null && b.Victim.Visual != null) b.Victim.Visual.SetHidden(true);
            }

            // Seat everyone around the break-room table
            int seat = 0;
            foreach (Actor a in Ctx.Actors)
            {
                if (!a.IsAlive) continue;
                float ang = seat * 60f * Mathf.Deg2Rad;
                Vector3 p = _tableCenter + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * 2.4f + Vector3.up * 0.1f;
                Vector3 look = _tableCenter - p;
                a.TeleportTo(p, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
                a.MovementLocked = true;
                seat++;
                if (a.IsBot) _botVoteAt[a] = Time.time + Random.Range(4f, MeetingDuration - 3f);
            }
            // Clear any props that got kicked into the meeting area
            foreach (PhysicsProp prop in Ctx.Root.GetComponentsInChildren<PhysicsProp>())
                if (FlatDist(prop.transform.position, _tableCenter) < 4f) prop.ResetToStart();
        }

        private void ApplyBodyEvidence(Body body)
        {
            foreach (Actor bot in Ctx.Actors)
            {
                if (!bot.IsBot || !bot.IsAlive) continue;
                foreach (Sighting s in _sightings[bot])
                {
                    if (s.Who == bot || s.Who == body.Victim) continue;
                    if (s.Time < body.Time - 12f || s.Time > body.Time + 2f) continue;
                    if (FlatDist(s.Pos, body.Pos) > 9f) continue;
                    AddSuspicion(bot, s.Who, 18f);
                }
            }
        }

        private void TickMeeting(float dt)
        {
            _phaseTimer -= dt;
            foreach (Actor a in Ctx.Actors)
            {
                a.ConsumeInteractPressed();
                float at;
                if (a.IsBot && a.IsAlive && !_votes.ContainsKey(a) && _botVoteAt.TryGetValue(a, out at) && Time.time >= at)
                    CastVote(a, BotChooseVote(a));
            }
            int alive = 0;
            foreach (Actor a in Ctx.Actors) if (a.IsAlive) alive++;
            if (_votes.Count >= alive || _phaseTimer <= 0f) ResolveVotes();
        }

        private void CastVote(Actor voter, Actor target)
        {
            if (_votes.ContainsKey(voter)) return;
            _votes[voter] = target;
            ProceduralAudio.PlayUI(Sfx.Click, 0.6f, 1.1f);
            Feed(voter.DisplayName + " voted");
        }

        private Actor BotChooseVote(Actor bot)
        {
            List<Actor> candidates = new List<Actor>();
            foreach (Actor a in Ctx.Actors) if (a.IsAlive && a != bot) candidates.Add(a);
            if (candidates.Count == 0) return null;

            if (bot == _hr)
            {
                // HR blends in: pile onto whoever already has votes, else random or skip.
                Actor pile = MostVoted(bot);
                if (pile != null && Random.value < 0.7f) return pile;
                return Random.value < 0.5f ? candidates[Random.Range(0, candidates.Count)] : null;
            }

            Actor best = null;
            float bestScore = 0f;
            foreach (Actor c in candidates)
            {
                float s = GetSuspicion(bot, c) + Random.Range(0f, 8f);
                if (s > bestScore) { bestScore = s; best = c; }
            }
            if (best != null && bestScore >= 24f) return best;
            Actor bandwagon = MostVoted(bot);
            if (bandwagon != null && Random.value < 0.4f) return bandwagon;
            return Random.value < 0.3f ? candidates[Random.Range(0, candidates.Count)] : null;
        }

        private Actor MostVoted(Actor exclude)
        {
            Dictionary<Actor, int> counts = new Dictionary<Actor, int>();
            foreach (KeyValuePair<Actor, Actor> kv in _votes)
            {
                if (kv.Value == null || kv.Value == exclude) continue;
                int c;
                counts.TryGetValue(kv.Value, out c);
                counts[kv.Value] = c + 1;
            }
            Actor best = null;
            int bestC = 0;
            foreach (KeyValuePair<Actor, int> kv in counts) if (kv.Value > bestC) { bestC = kv.Value; best = kv.Key; }
            return best;
        }

        private void ResolveVotes()
        {
            Dictionary<Actor, int> counts = new Dictionary<Actor, int>();
            int skips = 0;
            foreach (KeyValuePair<Actor, Actor> kv in _votes)
            {
                if (kv.Value == null || !kv.Value.IsAlive) { skips++; continue; }
                int c;
                counts.TryGetValue(kv.Value, out c);
                counts[kv.Value] = c + 1;
            }
            Actor top = null;
            int topC = 0;
            bool tie = false;
            foreach (KeyValuePair<Actor, int> kv in counts)
            {
                if (kv.Value > topC) { topC = kv.Value; top = kv.Key; tie = false; }
                else if (kv.Value == topC) tie = true;
            }
            if (top == null || tie || topC <= skips)
            {
                _ejected = null;
                _revealText = tie ? "It's a tie. Nobody was let go." : "No one was let go. (Skipped)";
            }
            else
            {
                _ejected = top;
                _revealText = top.DisplayName + " was let go... and " + (top == _hr ? "WAS HR!" : "was NOT HR.");
            }
            _phase = Phase.Reveal;
            _phaseTimer = RevealDuration;
            ProceduralAudio.PlayUI(Sfx.Whoosh, 0.8f, 0.9f);
        }

        private void FinishReveal()
        {
            if (_ejected != null)
            {
                _ejecting = true;
                _ejected.Eliminate(true);
                _ejecting = false;
                RemoveUnfinishedTasks(_ejected);
                Feed(_ejected.DisplayName + " was escorted out");
                if (_ejected == _hr) { WorkersWin("HR was voted out. Workers win!"); return; }
            }
            if (_tasksRequired > 0 && _tasksDone >= _tasksRequired)
            {
                WorkersWin("Every task is done. The office survives another quarter!");
                return;
            }
            CheckHrWin();
            if (State != MinigameState.Playing) return;

            _phase = Phase.Roam;
            _hrReadyAt = Mathf.Max(_hrReadyAt, Time.time + 10f);
            foreach (Actor a in Ctx.Actors) if (a.IsAlive) a.MovementLocked = false;
            ProceduralAudio.PlayUI(Sfx.Ding, 0.6f, 1.1f);
        }

        // =================================================================== suspicion

        private void RecordSightings()
        {
            float now = Time.time;
            foreach (Actor bot in Ctx.Actors)
            {
                if (!bot.IsBot || !bot.IsAlive) continue;
                List<Sighting> list = _sightings[bot];
                list.RemoveAll(s => now - s.Time > 40f);
                foreach (Actor o in Ctx.Actors)
                {
                    if (o == bot || !o.IsAlive) continue;
                    if (FlatDist(o.transform.position, bot.transform.position) > 22f) continue;
                    if (!BotBrain.CanSee(bot, o)) continue;
                    list.Add(new Sighting { Who = o, Pos = o.transform.position, Time = now });
                }
            }
        }

        private void AddSuspicion(Actor bot, Actor target, float amount)
        {
            Dictionary<Actor, float> d;
            if (!_suspicion.TryGetValue(bot, out d)) return;
            float v;
            d.TryGetValue(target, out v);
            d[target] = v + amount;
        }

        private float GetSuspicion(Actor bot, Actor target)
        {
            Dictionary<Actor, float> d;
            float v;
            return _suspicion.TryGetValue(bot, out d) && d.TryGetValue(target, out v) ? v : 0f;
        }

        // =================================================================== bots

        public override void DriveBot(BotBrain bot)
        {
            Actor self = bot.Self;
            if (_phase != Phase.Roam || !self.IsAlive) { bot.HoldStill = true; return; }
            bot.ArriveDistance = 0.6f;
            if (self == _hr) DriveHr(bot);
            else DriveWorker(bot);
        }

        private void DriveWorker(BotBrain bot)
        {
            Actor self = bot.Self;
            Vector3 pos = self.transform.position;

            // 1) Report a visible body
            foreach (Body b in _bodies)
            {
                if (b.Reported) continue;
                if (FlatDist(pos, b.Pos) > 11f) continue;
                if (!PhysicsUtil.LineOfSight(self.EyePosition, b.Pos + Vector3.up * 0.4f, self.transform, b.Victim != null ? b.Victim.transform : null)) continue;
                if (FlatDist(pos, b.Pos) < ReportRange - 0.6f) { bot.InteractPressed = true; bot.HoldStill = true; }
                else GoTo(bot, b.Pos);
                return;
            }

            // 2) Saw HR shoot someone -> run to the emergency button
            if (_buttonUses[self] > 0 && _hr != null && GetSuspicion(self, _hr) >= 100f)
            {
                if (FlatDist(pos, _buttonPos) < ButtonRange - 0.5f) { bot.InteractPressed = true; bot.HoldStill = true; }
                else GoTo(bot, _tableCenter + new Vector3(0f, 0f, -1.6f));
                return;
            }

            // 3) Do tasks in order
            List<TaskAssign> list;
            if (_tasks.TryGetValue(self, out list))
            {
                foreach (TaskAssign t in list)
                {
                    if (t.Done) continue;
                    Station s = _stations[t.Station];
                    if (FlatDist(pos, s.Pos) < StationRange * 0.6f)
                    {
                        bot.HoldStill = true;
                        bot.InteractHeld = true;
                        bot.AimPoint = s.Pos + Vector3.up * 0.8f + (s.Pos - pos).normalized;
                    }
                    else GoTo(bot, s.Pos);
                    return;
                }
            }

            // 4) Done: wander between stations (makes for alibis/witnesses)
            Wander(bot);
        }

        private void DriveHr(BotBrain bot)
        {
            Actor self = bot.Self;
            Vector3 pos = self.transform.position;
            bool ready = Time.time >= _hrReadyAt;

            if (ready)
            {
                Actor victim = FindIsolatedVictim(self);
                if (victim != null)
                {
                    bot.AimActor = victim;
                    float d = FlatDist(pos, victim.transform.position);
                    if (d > 7f) GoTo(bot, victim.transform.position);
                    else bot.HoldStill = d < 4f;
                    bot.WantFire = d < 12f && BotBrain.CanSee(self, victim);
                    return;
                }
                // Stalk the nearest worker loosely
                Actor near = null;
                float best = float.MaxValue;
                foreach (Actor a in Ctx.Actors)
                {
                    if (a == self || !a.IsAlive) continue;
                    float d = FlatDist(pos, a.transform.position);
                    if (d < best) { best = d; near = a; }
                }
                if (near != null && best > 6f) { GoTo(bot, near.transform.position); return; }
            }

            // Fake tasks: go to a random station and stand there a few seconds
            Wander(bot);
        }

        private Actor FindIsolatedVictim(Actor hr)
        {
            Actor best = null;
            float bestD = 14f;
            foreach (Actor v in Ctx.Actors)
            {
                if (v == hr || !v.IsAlive) continue;
                float d = FlatDist(hr.transform.position, v.transform.position);
                if (d > bestD) continue;
                if (!BotBrain.CanSee(hr, v)) continue;
                bool witnessed = false;
                foreach (Actor w in Ctx.Actors)
                {
                    if (w == hr || w == v || !w.IsAlive) continue;
                    if (FlatDist(w.transform.position, hr.transform.position) < 20f && BotBrain.CanSee(w, hr)) { witnessed = true; break; }
                }
                if (witnessed) continue;
                bestD = d;
                best = v;
            }
            return best;
        }

        private void Wander(BotBrain bot)
        {
            Vector3 pos = bot.Self.transform.position;
            if (bot.Timer <= 0f || bot.Index < 0 || bot.Index >= _stations.Count)
            {
                bot.Index = Random.Range(0, _stations.Count);
                bot.Timer = 1f;
                bot.Timer2 = Random.Range(2f, 5f);
            }
            Station s = _stations[bot.Index];
            if (FlatDist(pos, s.Pos) < StationRange * 0.7f)
            {
                bot.HoldStill = true;
                bot.InteractHeld = true; // looks busy
                bot.Timer2 -= Time.deltaTime;
                if (bot.Timer2 <= 0f) bot.Timer = 0f;
            }
            else GoTo(bot, s.Pos);
        }

        private void GoTo(BotBrain bot, Vector3 target)
        {
            Vector3 from = bot.Self.transform.position;
            Vector3 wp = NextWaypoint(from, target);
            bot.MoveTarget = AvoidTable(from, wp);
        }

        /// <summary>Detour around the break-room table if the straight line clips it.</summary>
        private Vector3 AvoidTable(Vector3 from, Vector3 to)
        {
            Vector3 c = _tableCenter;
            const float clear = 2.0f;
            if (FlatDist(from, c) < clear + 0.3f || FlatDist(to, c) < clear + 0.3f) return to;
            Vector3 a = new Vector3(from.x, 0f, from.z), b = new Vector3(to.x, 0f, to.z), cc = new Vector3(c.x, 0f, c.z);
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 0.01f) return to;
            float t = Mathf.Clamp01(Vector3.Dot(cc - a, ab) / len2);
            Vector3 closest = a + ab * t;
            if ((closest - cc).sqrMagnitude > clear * clear) return to;
            Vector3 side = Vector3.Cross(Vector3.up, ab.normalized);
            if (Vector3.Dot(closest - cc, side) < 0f) side = -side;
            Vector3 detour = cc + side * (clear + 0.8f);
            detour.z = Mathf.Clamp(detour.z, 4.8f, 11.2f);
            return new Vector3(detour.x, from.y, detour.z);
        }

        private static int RoomOf(Vector3 p)
        {
            if (p.x < -4f) return 0;
            if (p.z > 4f) return 7;
            if (p.z < -4f) return 8;
            return 4;
        }

        private static Vector3 NextWaypoint(Vector3 from, Vector3 to)
        {
            int a = RoomOf(from), b = RoomOf(to);
            if (a == b) return to;
            // BFS over the room/door graph
            int n = NavNodes.Length;
            int[] prev = new int[n];
            for (int i = 0; i < n; i++) prev[i] = -2;
            Queue<int> q = new Queue<int>();
            q.Enqueue(a);
            prev[a] = -1;
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                if (c == b) break;
                foreach (int nb in NavEdges[c]) if (prev[nb] == -2) { prev[nb] = c; q.Enqueue(nb); }
            }
            if (prev[b] == -2) return to;
            List<int> path = new List<int>();
            for (int c = b; c != -1; c = prev[c]) path.Add(c);
            path.Reverse();
            // Head to the first node (skipping the start room centre) we haven't reached yet.
            for (int i = 1; i < path.Count; i++)
            {
                if (i == path.Count - 1) return to;
                Vector3 node = NavNodes[path[i]];
                if (FlatDist(from, node) > 1.0f) return node + Vector3.up * from.y;
            }
            return to;
        }

        // =================================================================== helpers

        private Body BodyNear(Vector3 pos, float range)
        {
            foreach (Body b in _bodies) if (!b.Reported && FlatDist(pos, b.Pos) < range) return b;
            return null;
        }

        private static float FlatDist(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // =================================================================== HUD

        public override void DrawHUD(Hud hud)
        {
            float s = hud.Scale;
            Actor local = Ctx.LocalActor;
            bool isHr = local != null && local == _hr;

            // Role banner
            if (State == MinigameState.Intro)
            {
                string role = isHr ? "YOU ARE  HR" : "YOU ARE A  WORKER";
                Color rc = isHr ? new Color(1f, 0.3f, 0.3f) : new Color(0.5f, 0.85f, 1f);
                hud.Rect(new Rect(0, Screen.height * 0.3f, Screen.width, 90 * s), new Color(0f, 0f, 0f, 0.6f));
                hud.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 60 * s), role, 44, rc, TextAnchor.MiddleCenter);
                hud.Label(new Rect(0, Screen.height * 0.3f + 55 * s, Screen.width, 30 * s),
                    isHr ? "Stapler is hidden until you fire. One staple = one firing. Don't get seen."
                         : "Hold E at your yellow task pads. Report bodies with E. Emergency button: break room table.",
                    18, Color.white, TextAnchor.MiddleCenter);
                return;
            }
            if (State != MinigameState.Playing) return;

            // Global task bar
            float frac = _tasksRequired > 0 ? (float)_tasksDone / _tasksRequired : 0f;
            hud.Label(new Rect(20 * s, 96 * s, 300 * s, 22 * s), "Office tasks " + _tasksDone + "/" + _tasksRequired, 15, Color.white);
            hud.Bar(new Rect(20 * s, 118 * s, 260 * s, 10 * s), frac, new Color(0.4f, 1f, 0.4f), new Color(0f, 0f, 0f, 0.5f));

            if (local != null)
            {
                string roleLine = isHr
                    ? (Time.time >= _hrReadyAt ? "HR - STAPLER READY" : "HR - stapler in " + Mathf.CeilToInt(_hrReadyAt - Time.time) + "s")
                    : "WORKER";
                hud.Label(new Rect(20 * s, 134 * s, 400 * s, 24 * s), roleLine, 18, isHr ? new Color(1f, 0.35f, 0.3f) : new Color(0.5f, 0.85f, 1f));

                float y = 160 * s;
                List<TaskAssign> mine;
                if (_tasks.TryGetValue(local, out mine))
                {
                    foreach (TaskAssign t in mine)
                    {
                        Station st = _stations[t.Station];
                        hud.Label(new Rect(20 * s, y, 420 * s, 22 * s), (t.Done ? "[x] " : "[ ] ") + st.Name + "  (" + st.Room + ")", 15,
                            t.Done ? new Color(0.5f, 1f, 0.5f) : Color.white);
                        y += 22 * s;
                        if (!t.Done && _phase == Phase.Roam && local.IsAlive)
                            hud.WorldLabel(st.Pos + Vector3.up * 1.6f, st.Name, new Color(1f, 0.9f, 0.3f), 15, 40f);
                        if (!t.Done && t.Progress > 0f)
                        {
                            hud.Bar(new Rect(Screen.width * 0.5f - 150 * s, Screen.height * 0.62f, 300 * s, 16 * s), t.Progress / st.Duration,
                                new Color(1f, 0.85f, 0.2f), new Color(0f, 0f, 0f, 0.6f));
                            hud.Label(new Rect(0, Screen.height * 0.62f - 26 * s, Screen.width, 24 * s), st.Name + "...", 18, Color.white, TextAnchor.MiddleCenter);
                        }
                    }
                }
                else if (isHr)
                {
                    hud.Label(new Rect(20 * s, y, 420 * s, 22 * s), "Fake tasks to blend in. Fire isolated workers.", 14, new Color(1f, 0.7f, 0.7f));
                }

                if (_phase == Phase.Roam && local.IsAlive) DrawPrompt(hud, local, isHr);
            }

            // Unreported bodies
            if (_phase == Phase.Roam)
                foreach (Body b in _bodies)
                    if (!b.Reported) hud.WorldLabel(b.Pos + Vector3.up * 0.9f, "BODY", new Color(1f, 0.2f, 0.2f), 16, 14f);

            if (_phase == Phase.Meeting) DrawMeeting(hud, local);
            else if (_phase == Phase.Reveal) DrawReveal(hud);
        }

        private void DrawPrompt(Hud hud, Actor local, bool isHr)
        {
            Vector3 pos = local.transform.position;
            string prompt = null;
            if (!isHr && BodyNear(pos, ReportRange) != null) prompt = "[E] REPORT BODY";
            else if (FlatDist(pos, _buttonPos) < ButtonRange)
                prompt = _buttonUses[local] > 0 ? "[E] Emergency meeting (1 left)" : "Emergency meeting used";
            else
            {
                List<TaskAssign> mine;
                if (_tasks.TryGetValue(local, out mine))
                    foreach (TaskAssign t in mine)
                        if (!t.Done && FlatDist(pos, _stations[t.Station].Pos) < StationRange)
                        { prompt = "Hold [E]  " + _stations[t.Station].Name; break; }
            }
            if (prompt != null)
                hud.Label(new Rect(0, Screen.height * 0.68f, Screen.width, 28 * hud.Scale), prompt, 20, Color.white, TextAnchor.MiddleCenter);
        }

        private void DrawMeeting(Hud hud, Actor local)
        {
            float s = hud.Scale;
            float w = 520 * s, h = 470 * s;
            Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            hud.Panel(panel);
            hud.Label(new Rect(panel.x, panel.y + 10 * s, w, 34 * s), "MEETING", 30, new Color(1f, 0.35f, 0.3f), TextAnchor.MiddleCenter);
            hud.Label(new Rect(panel.x, panel.y + 44 * s, w, 24 * s), _meetingReason, 16, Color.white, TextAnchor.MiddleCenter);
            hud.Label(new Rect(panel.x, panel.y + 68 * s, w, 22 * s), "Who is HR?   " + Mathf.CeilToInt(Mathf.Max(0f, _phaseTimer)) + "s", 15,
                new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleCenter);

            bool canVote = local != null && local.IsAlive && !_votes.ContainsKey(local);
            float y = panel.y + 100 * s;
            foreach (Actor a in Ctx.Actors)
            {
                Rect row = new Rect(panel.x + 30 * s, y, w - 60 * s, 40 * s);
                if (!a.IsAlive)
                {
                    hud.Label(row, "  " + a.DisplayName + "  (fired)", 17, new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleLeft);
                }
                else
                {
                    string label = a.DisplayName + (a == local ? " (you)" : "") + (_votes.ContainsKey(a) ? "   - voted" : "");
                    if (canVote && a != local)
                    {
                        if (hud.Button(row, label, 18)) CastVote(local, a);
                    }
                    else
                    {
                        hud.Rect(row, new Color(a.Color.r, a.Color.g, a.Color.b, 0.25f));
                        hud.Label(row, "  " + label, 18, Color.white, TextAnchor.MiddleLeft);
                    }
                }
                y += 46 * s;
            }
            Rect skip = new Rect(panel.x + 30 * s, panel.y + h - 56 * s, w - 60 * s, 40 * s);
            if (canVote) { if (hud.Button(skip, "Skip vote", 18)) CastVote(local, null); }
            else hud.Label(skip, local != null && local.IsAlive ? "Vote cast. Waiting..." : "You are fired. Spectating the meeting.", 16,
                new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleCenter);
        }

        private void DrawReveal(Hud hud)
        {
            float s = hud.Scale;
            hud.Rect(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.75f));
            hud.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 50 * s), _revealText, 34,
                _ejected == _hr && _ejected != null ? new Color(0.5f, 1f, 0.5f) : Color.white, TextAnchor.MiddleCenter);
            // Vote breakdown
            float y = Screen.height * 0.4f + 60 * s;
            foreach (Actor a in Ctx.Actors)
            {
                Actor v;
                if (!_votes.TryGetValue(a, out v)) continue;
                hud.Label(new Rect(0, y, Screen.width, 22 * s), a.DisplayName + "  ->  " + (v != null ? v.DisplayName : "skip"), 15,
                    new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleCenter);
                y += 22 * s;
            }
        }

        public override IEnumerable<string> ScoreLines()
        {
            foreach (Actor a in Ctx.Actors)
                yield return a.DisplayName + (a.IsAlive ? "" : "  (fired)") + "   tasks " + GetScore(a);
        }
    }
}
