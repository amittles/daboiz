using System.Collections.Generic;
using Daboiz.Actors;
using Daboiz.AI;
using Daboiz.Core;
using Daboiz.UI;
using Daboiz.Weapons;
using UnityEngine;

namespace Daboiz.Minigames
{
    public enum MinigameState { Intro, Playing, Finished }

    /// <summary>Everything a minigame gets from the flow controller.</summary>
    public class MatchContext
    {
        public Transform Root;
        public List<Participant> Participants;
        public readonly List<Actor> Actors = new List<Actor>();
        public Actor LocalActor;
        public System.Random Rng;
        public GameSettings Settings;

        public Actor ActorOf(Participant p)
        {
            for (int i = 0; i < Actors.Count; i++) if (Actors[i].Participant == p) return Actors[i];
            return null;
        }
    }

    /// <summary>
    /// Base class for every minigame: lifecycle (Intro -> Playing -> Finished), round timer,
    /// kill plane, respawns, scoring helpers, bot hook, HUD hook and final ranking.
    /// Subclasses build their level procedurally in Build().
    /// </summary>
    public abstract class MinigameBase
    {
        public abstract string Title { get; }
        public abstract string Tagline { get; }
        public virtual string HowToPlay { get { return ""; } }

        public GameRules Rules = new GameRules();
        public MatchContext Ctx { get; private set; }
        public MinigameState State { get; protected set; }
        public float RoundDuration = 180f;
        public float TimeLeft { get; protected set; }
        public float IntroDuration = 4f;
        public float IntroLeft { get; private set; }
        public float KillY = -25f;
        public List<RankEntry> Results { get; private set; }
        public string ResultHeadline { get; private set; }

        public virtual bool ShowTimer { get { return RoundDuration > 0f; } }
        public virtual bool WantsCursor { get { return false; } }
        public virtual bool LockDuringIntro { get { return true; } }

        public Vector3 SpectatorPoint = new Vector3(0f, 25f, -25f);
        public Vector3 SpectatorLookAt = Vector3.zero;

        protected readonly Dictionary<Actor, int> Score = new Dictionary<Actor, int>();
        protected readonly List<Vector3> SpawnPoints = new List<Vector3>();
        protected readonly List<float> SpawnYaws = new List<float>();

        private readonly List<KeyValuePair<Actor, float>> _respawnQueue = new List<KeyValuePair<Actor, float>>();
        private bool _locked;

        // ------------------------------------------------------------------ lifecycle

        public void Begin(MatchContext ctx)
        {
            Ctx = ctx;
            Build();
            State = MinigameState.Intro;
            IntroLeft = IntroDuration;
            TimeLeft = RoundDuration;
            SetAllLocked(LockDuringIntro);
            OnIntroStart();
        }

        /// <summary>Build the level and spawn actors (use SpawnAllActors).</summary>
        protected abstract void Build();
        protected virtual void OnIntroStart() { }
        protected virtual void OnRoundStart() { }
        protected abstract void OnTick(float dt);
        protected virtual void OnTimeUp() { EndRound(RankByScore(null), "Time's up!"); }
        public virtual void Teardown() { }

        public void Tick(float dt)
        {
            switch (State)
            {
                case MinigameState.Intro:
                    IntroLeft -= dt;
                    if (IntroLeft <= 0f)
                    {
                        State = MinigameState.Playing;
                        SetAllLocked(false);
                        ProceduralAudio.PlayUI(Sfx.Ding, 0.8f, 1.2f);
                        OnRoundStart();
                    }
                    break;
                case MinigameState.Playing:
                    TimeLeft -= dt;
                    CheckFallouts();
                    ProcessRespawns();
                    OnTick(dt);
                    if (State == MinigameState.Playing && RoundDuration > 0f && TimeLeft <= 0f)
                    {
                        TimeLeft = 0f;
                        OnTimeUp();
                    }
                    break;
            }
        }

        protected void EndRound(List<RankEntry> ranking, string headline)
        {
            if (State == MinigameState.Finished) return;
            State = MinigameState.Finished;
            Results = ranking;
            ResultHeadline = headline;
            SetAllLocked(true);
            ProceduralAudio.PlayUI(Sfx.Ding, 1f, 0.8f);
            if (ranking != null && ranking.Count > 0)
            {
                Actor w = Ctx.ActorOf(ranking[0].Participant);
                if (w != null) Fx.Confetti(w.transform.position + Vector3.up * 2f, 60);
            }
        }

        // ------------------------------------------------------------------ hooks

        /// <summary>Fill the bot's intents for this frame. Default = free-for-all combat.</summary>
        public virtual void DriveBot(BotBrain bot) { bot.DefaultCombat(); }

        public virtual bool CanFire(Actor a) { return State == MinigameState.Playing; }

        public virtual void HandleActorDied(Actor victim, Actor killer)
        {
            if (killer != null && killer != victim) AddScore(killer, 1);
            string msg = killer != null && killer != victim
                ? killer.DisplayName + "  >>  " + victim.DisplayName
                : victim.DisplayName + " had an accident";
            Feed(msg);
            if (Rules.RespawnOnDeath && !victim.Eliminated) ScheduleRespawn(victim, Rules.RespawnDelay);
        }

        /// <summary>Called when an actor drops below KillY.</summary>
        protected virtual void OnActorFellOut(Actor a)
        {
            Actor credit = a.LastAttacker != null && Time.time - a.LastDamageTime < 6f ? a.LastAttacker : null;
            a.Die(credit, false);
        }

        public virtual void DrawHUD(Hud hud) { }

        public virtual IEnumerable<string> ScoreLines()
        {
            List<Actor> sorted = new List<Actor>(Ctx.Actors);
            sorted.Sort((x, y) => GetScore(y).CompareTo(GetScore(x)));
            foreach (Actor a in sorted) yield return a.DisplayName + "   " + GetScore(a);
        }

        // ------------------------------------------------------------------ helpers

        protected void Feed(string msg) { if (GameFlow.Instance != null) GameFlow.Instance.Hud.Feed(msg); }

        protected void Announce(string msg, Color color, float duration = 2f)
        {
            if (GameFlow.Instance != null) GameFlow.Instance.Hud.Announce(msg, color, duration);
        }

        protected void AddScore(Actor a, int delta)
        {
            if (a == null) return;
            int s;
            Score.TryGetValue(a, out s);
            Score[a] = s + delta;
        }

        protected int GetScore(Actor a)
        {
            int s;
            return a != null && Score.TryGetValue(a, out s) ? s : 0;
        }

        protected List<RankEntry> RankByScore(System.Func<Actor, string> detail)
        {
            List<Actor> sorted = new List<Actor>(Ctx.Actors);
            sorted.Sort((x, y) => GetScore(y).CompareTo(GetScore(x)));
            List<RankEntry> r = new List<RankEntry>();
            int place = 0, lastScore = int.MinValue;
            for (int i = 0; i < sorted.Count; i++)
            {
                int s = GetScore(sorted[i]);
                if (s != lastScore) { place = i + 1; lastScore = s; }
                r.Add(new RankEntry(sorted[i].Participant, place, detail != null ? detail(sorted[i]) : s + " pts"));
            }
            return r;
        }

        protected void SetAllLocked(bool locked)
        {
            _locked = locked;
            if (Ctx == null) return;
            for (int i = 0; i < Ctx.Actors.Count; i++) Ctx.Actors[i].MovementLocked = locked;
        }

        /// <summary>Spawn one actor per participant at SpawnPoints (cycled) with the given loadout.</summary>
        protected void SpawnAllActors(System.Func<Participant, IList<WeaponDefinition>> loadout)
        {
            for (int i = 0; i < Ctx.Participants.Count; i++)
            {
                Participant p = Ctx.Participants[i];
                Vector3 pos = SpawnPoints.Count > 0 ? SpawnPoints[i % SpawnPoints.Count] : new Vector3(i * 2f, 1f, 0f);
                float yaw = SpawnYaws.Count > 0 ? SpawnYaws[i % SpawnYaws.Count] : 0f;
                Actor a = ActorFactory.Spawn(p, pos, yaw, Ctx.Root, Ctx.Settings);
                IList<WeaponDefinition> l = loadout != null ? loadout(p) : null;
                if (l != null && l.Count > 0) a.Weapons.SetLoadout(l);
                else a.Weapons.Holstered = true;
                a.MovementLocked = _locked;
                Ctx.Actors.Add(a);
                if (p.IsLocal) Ctx.LocalActor = a;
                Score[a] = 0;
            }
        }

        protected void ScheduleRespawn(Actor a, float delay)
        {
            _respawnQueue.Add(new KeyValuePair<Actor, float>(a, Time.time + delay));
        }

        protected virtual Vector3 GetRespawnPoint(Actor a, out float yaw)
        {
            int i = Ctx.Rng.Next(Mathf.Max(1, SpawnPoints.Count));
            yaw = SpawnYaws.Count > i ? SpawnYaws[i] : 0f;
            return SpawnPoints.Count > i ? SpawnPoints[i] : Vector3.up;
        }

        private void ProcessRespawns()
        {
            for (int i = _respawnQueue.Count - 1; i >= 0; i--)
            {
                if (Time.time < _respawnQueue[i].Value) continue;
                Actor a = _respawnQueue[i].Key;
                _respawnQueue.RemoveAt(i);
                if (a == null || a.IsAlive || a.Eliminated) continue;
                float yaw;
                Vector3 p = GetRespawnPoint(a, out yaw);
                a.Respawn(p, yaw);
                a.Status.Apply(Combat.StatusType.Shield, 1.5f);
            }
        }

        private void CheckFallouts()
        {
            for (int i = 0; i < Ctx.Actors.Count; i++)
            {
                Actor a = Ctx.Actors[i];
                if (a.IsAlive && a.transform.position.y < KillY) OnActorFellOut(a);
            }
        }

        protected int AliveCount()
        {
            int n = 0;
            for (int i = 0; i < Ctx.Actors.Count; i++) if (Ctx.Actors[i].IsAlive && !Ctx.Actors[i].Eliminated) n++;
            return n;
        }

        protected static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return n + "th";
            switch (n % 10) { case 1: return n + "st"; case 2: return n + "nd"; case 3: return n + "rd"; default: return n + "th"; }
        }
    }
}
