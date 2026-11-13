using System.Collections.Generic;
using Daboiz.Actors;
using Daboiz.AI;
using Daboiz.Core;
using Daboiz.UI;
using Daboiz.Weapons;
using UnityEngine;

namespace Daboiz.Minigames
{
    /// <summary>
    /// Chessboard over the void. Guns do no damage, only shove and impair. Tiles shake then fall,
    /// faster and faster. Last one standing wins.
    /// </summary>
    public class FallingChessboard : MinigameBase
    {
        public override string Title { get { return "Falling Chessboard"; } }
        public override string Tagline { get { return "Guns don't hurt. Gravity does. Last one standing wins."; } }
        public override string HowToPlay
        {
            get
            {
                return "Tiles flash red and shake, then drop into the void - faster over time.\n" +
                       "Your guns only SHOVE and IMPAIR: slow, reverse controls, freeze, shrink, launch, float.\n" +
                       "Scroll / 1-9 to swap guns. Knock everyone off. Don't fall.";
            }
        }

        private enum TileState { Solid, Shaking, Falling, Gone }

        private class Tile
        {
            public GameObject Go;
            public Renderer Renderer;
            public Material BaseMat;
            public Vector3 BasePos;
            public TileState State;
            public float Timer;
            public int X, Z;
        }

        private const int N = 10;
        private const float TileSize = 3f;
        private const float ShakeTime = 1.3f;
        private Tile[,] _tiles;
        private float _elapsed;
        private float _nextDrop = 4f;
        private readonly List<Actor> _eliminationOrder = new List<Actor>();
        private Material _warnMat;

        public FallingChessboard()
        {
            RoundDuration = 150f;
            KillY = -8f;
            Rules = GameRules.KnockbackOnly(1.6f);
        }

        protected override void Build()
        {
            Transform root = Ctx.Root;
            LevelKit.Environment(new Color(0.4f, 0.35f, 0.6f), new Color(0.35f, 0.3f, 0.4f), new Color(0.1f, 0.05f, 0.15f), true,
                new Color(0.25f, 0.2f, 0.35f), 0.008f);
            _warnMat = Mats.Emissive(new Color(1f, 0.15f, 0.1f), 1.2f);
            _tiles = new Tile[N, N];
            Color light = new Color(0.93f, 0.88f, 0.78f);
            Color dark = new Color(0.32f, 0.2f, 0.14f);
            Transform board = new GameObject("Board").transform;
            board.SetParent(root, false);
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                {
                    Vector3 c = TileCenter(x, z) + Vector3.down * 0.3f;
                    GameObject g = Prims.Box(board, "Tile_" + x + "_" + z, c, new Vector3(TileSize - 0.06f, 0.6f, TileSize - 0.06f),
                        ((x + z) & 1) == 0 ? light : dark);
                    _tiles[x, z] = new Tile
                    {
                        Go = g, Renderer = g.GetComponent<Renderer>(), BaseMat = g.GetComponent<Renderer>().sharedMaterial,
                        BasePos = c, State = TileState.Solid, X = x, Z = z
                    };
                }
            // Giant decorative chess pieces in the distance
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 45f, -20f, Mathf.Sin(a) * 45f);
                Color pc = i % 2 == 0 ? light : dark;
                Prims.Make(PrimitiveType.Cylinder, root, "PieceBase", p, new Vector3(8f, 10f, 8f), pc, false);
                Prims.Make(PrimitiveType.Sphere, root, "PieceHead", p + Vector3.up * 13f, Vector3.one * 7f, pc, false);
            }
            LevelKit.Lamp(root, new Vector3(0f, 12f, 0f), new Color(1f, 0.9f, 0.8f), 40f, 1.2f);

            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                SpawnPoints.Add(new Vector3(Mathf.Cos(a) * 8f, 0.1f, Mathf.Sin(a) * 8f));
                SpawnYaws.Add(Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg);
            }
            string[] ids = { "persuader", "yeet", "brain_freeze", "mirror", "slowpoke", "bubble_blaster", "shrinkies", "leaf_blower" };
            List<WeaponDefinition> loadout = new List<WeaponDefinition>();
            foreach (string id in ids) loadout.Add(WeaponRegistry.Get(id));
            SpawnAllActors(p => loadout);
            SpectatorPoint = new Vector3(0f, 26f, -24f);
            SpectatorLookAt = Vector3.zero;
        }

        private static Vector3 TileCenter(int x, int z)
        {
            return new Vector3((x - (N - 1) * 0.5f) * TileSize, 0f, (z - (N - 1) * 0.5f) * TileSize);
        }

        private Tile TileAt(Vector3 pos)
        {
            int x = Mathf.FloorToInt(pos.x / TileSize + N * 0.5f);
            int z = Mathf.FloorToInt(pos.z / TileSize + N * 0.5f);
            if (x < 0 || z < 0 || x >= N || z >= N) return null;
            return _tiles[x, z];
        }

        protected override void OnTick(float dt)
        {
            _elapsed += dt;
            _nextDrop -= dt;
            if (_nextDrop <= 0f)
            {
                float t = Mathf.Clamp01(_elapsed / 100f);
                _nextDrop = Mathf.Lerp(2.6f, 0.35f, t);
                int count = 1 + Mathf.FloorToInt(_elapsed / 25f);
                for (int i = 0; i < count; i++) StartDrop();
            }

            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                {
                    Tile tile = _tiles[x, z];
                    switch (tile.State)
                    {
                        case TileState.Shaking:
                            tile.Timer -= dt;
                            float k = 1f - tile.Timer / ShakeTime;
                            tile.Go.transform.position = tile.BasePos + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * (0.06f * k);
                            tile.Renderer.sharedMaterial = Mathf.Repeat(Time.time * (4f + 10f * k), 1f) < 0.5f ? _warnMat : tile.BaseMat;
                            if (tile.Timer <= 0f) Drop(tile);
                            break;
                        case TileState.Falling:
                            if (tile.Go == null || tile.Go.transform.position.y < -60f)
                            {
                                if (tile.Go != null) Object.Destroy(tile.Go);
                                tile.State = TileState.Gone;
                            }
                            break;
                    }
                }

            if (AliveCount() <= 1) Finish();
        }

        private void StartDrop()
        {
            List<Tile> candidates = new List<Tile>();
            // 35%: target a tile under a random player, keeps things spicy
            if (Ctx.Rng.NextDouble() < 0.35)
            {
                List<Actor> alive = new List<Actor>();
                foreach (Actor a in Ctx.Actors) if (a.IsAlive) alive.Add(a);
                if (alive.Count > 0)
                {
                    Tile t = TileAt(alive[Ctx.Rng.Next(alive.Count)].transform.position);
                    if (t != null && t.State == TileState.Solid) { Shake(t); return; }
                }
            }
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                    if (_tiles[x, z].State == TileState.Solid) candidates.Add(_tiles[x, z]);
            if (candidates.Count == 0) return;
            // Prefer the outer ring early on so the arena shrinks inward
            candidates.Sort((a, b) => EdgeDistance(a).CompareTo(EdgeDistance(b)));
            int range = Mathf.Max(1, Mathf.CeilToInt(candidates.Count * 0.5f));
            Shake(candidates[Ctx.Rng.Next(range)]);
        }

        private static int EdgeDistance(Tile t)
        {
            return Mathf.Min(Mathf.Min(t.X, N - 1 - t.X), Mathf.Min(t.Z, N - 1 - t.Z));
        }

        private void Shake(Tile t)
        {
            t.State = TileState.Shaking;
            t.Timer = ShakeTime;
            ProceduralAudio.Play(Sfx.Buzz, t.BasePos, 0.25f, 1.6f);
        }

        private void Drop(Tile t)
        {
            t.State = TileState.Falling;
            t.Renderer.sharedMaterial = t.BaseMat;
            t.Go.transform.position = t.BasePos;
            Rigidbody rb = t.Go.AddComponent<Rigidbody>();
            rb.mass = 40f;
            rb.angularVelocity = Random.insideUnitSphere * 2f;
            RbCompat.SetVelocity(rb, Vector3.down * 2f);
            ProceduralAudio.Play(Sfx.Whoosh, t.BasePos, 0.5f, 0.6f);
        }

        protected override void OnActorFellOut(Actor a)
        {
            if (a.Eliminated) return;
            _eliminationOrder.Add(a);
            Feed(a.DisplayName + " fell into the void" + (a.LastAttacker != null && Time.time - a.LastDamageTime < 5f ? " (thanks, " + a.LastAttacker.DisplayName + ")" : ""));
            if (a.LastAttacker != null && Time.time - a.LastDamageTime < 5f) AddScore(a.LastAttacker, 1);
            a.Eliminate(false);
        }

        public override void HandleActorDied(Actor victim, Actor killer) { /* no respawns */ }

        protected override void OnTimeUp() { Finish(); }

        private void Finish()
        {
            List<RankEntry> r = new List<RankEntry>();
            List<Actor> alive = new List<Actor>();
            foreach (Actor a in Ctx.Actors) if (a.IsAlive && !a.Eliminated) alive.Add(a);
            foreach (Actor a in alive) r.Add(new RankEntry(a.Participant, 1, "survived  (" + GetScore(a) + " shoves off)"));
            int place = alive.Count + 1;
            for (int i = _eliminationOrder.Count - 1; i >= 0; i--)
            {
                Actor a = _eliminationOrder[i];
                r.Add(new RankEntry(a.Participant, place++, "fell at " + Mathf.RoundToInt(a.DeathTime - (Time.time - _elapsed)) + "s"));
            }
            string head = alive.Count == 1 ? alive[0].DisplayName + " is the last one standing!" :
                alive.Count == 0 ? "Everybody fell. Classic." : "Time! " + alive.Count + " survivors";
            EndRound(r, head);
        }

        public override void DriveBot(BotBrain bot)
        {
            Actor self = bot.Self;
            // Re-pick a safe tile periodically or when ours is in danger
            Tile cur = TileAt(self.transform.position);
            Tile goal = bot.Blackboard as Tile;
            bool goalBad = goal == null || goal.State != TileState.Solid;
            if (goalBad || Time.time > bot.Timer || (cur != null && cur.State != TileState.Solid))
            {
                bot.Timer = Time.time + Random.Range(1.5f, 3.5f);
                bot.Blackboard = PickSafeTile(self.transform.position);
                goal = bot.Blackboard as Tile;
            }
            if (goal != null) bot.MoveTarget = goal.BasePos + new Vector3(Mathf.Sin(Time.time + bot.Skill * 9f), 0f, Mathf.Cos(Time.time * 0.8f)) * 0.6f;
            bot.ArriveDistance = 0.4f;
            bot.AllowBhop = false;

            // Hop over holes ahead
            Vector3 vel = self.Motor.Velocity;
            vel.y = 0f;
            if (vel.sqrMagnitude > 4f && self.Motor.Grounded)
            {
                Vector3 ahead = self.transform.position + vel.normalized * 1.4f + Vector3.up * 0.5f;
                if (!Physics.Raycast(ahead, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore)) bot.WantJump = true;
            }

            Actor target = BotBrain.NearestEnemy(self, 35f, true);
            if (target != null)
            {
                bot.AimActor = target;
                bot.WantFire = true;
                if (Time.time > bot.Timer2)
                {
                    bot.Timer2 = Time.time + Random.Range(4f, 9f);
                    bot.WeaponSlot = Random.Range(0, self.Weapons.Inventory.Count);
                }
            }
        }

        private Tile PickSafeTile(Vector3 from)
        {
            Tile best = null;
            float bestScore = float.MaxValue;
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                {
                    Tile t = _tiles[x, z];
                    if (t.State != TileState.Solid) continue;
                    int solidNeighbours = 0;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            int nx = x + dx, nz = z + dz;
                            if (nx >= 0 && nz >= 0 && nx < N && nz < N && _tiles[nx, nz].State == TileState.Solid) solidNeighbours++;
                        }
                    float score = Vector3.Distance(from, t.BasePos) * 0.6f + t.BasePos.magnitude * 0.4f - solidNeighbours * 2f
                                  + Random.Range(0f, 3f);
                    if (score < bestScore) { bestScore = score; best = t; }
                }
            return best;
        }

        public override void DrawHUD(Hud hud)
        {
            float s = hud.Scale;
            hud.Label(new Rect(20 * s, 44 * s, 600 * s, 24 * s), "Alive: " + AliveCount() + "   Drop speed x" +
                (2.6f / Mathf.Lerp(2.6f, 0.35f, Mathf.Clamp01(_elapsed / 100f))).ToString("0.0"), 16, Color.white);
        }

        public override IEnumerable<string> ScoreLines()
        {
            foreach (Actor a in Ctx.Actors)
                yield return a.DisplayName + (a.IsAlive ? "   alive" : "   OUT") + "   shoves: " + GetScore(a);
        }
    }
}
