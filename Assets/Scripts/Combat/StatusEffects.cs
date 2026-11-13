using System.Collections.Generic;
using UnityEngine;

namespace Daboiz.Combat
{
    public enum StatusType
    {
        Slow, SpeedBoost, ReverseControls, Frozen, Shrunk, BigHead, LowGravity, Shield, Gooed, Floating
    }

    /// <summary>
    /// Timed status effects on an actor. Pure logic: outputs multipliers that the Actor feeds into
    /// the motor and visuals. Deterministic given the same Apply/Tick calls (netcode friendly).
    /// </summary>
    public class StatusEffects
    {
        private class Entry { public float Remaining; public float Magnitude; }

        private readonly Dictionary<StatusType, Entry> _active = new Dictionary<StatusType, Entry>();

        public float SpeedMultiplier { get; private set; }
        public float GravityMultiplier { get; private set; }
        public float JumpMultiplier { get; private set; }
        public float ScaleTarget { get; private set; }
        public float HeadScale { get; private set; }

        public StatusEffects() { Recompute(); }

        public bool Has(StatusType t) { return _active.ContainsKey(t); }
        public bool Frozen { get { return Has(StatusType.Frozen); } }
        public bool Reversed { get { return Has(StatusType.ReverseControls); } }
        public bool Shielded { get { return Has(StatusType.Shield); } }

        public float Remaining(StatusType t)
        {
            Entry e;
            return _active.TryGetValue(t, out e) ? e.Remaining : 0f;
        }

        /// <summary>Apply or refresh an effect. Magnitude meaning depends on type (see Recompute).</summary>
        public void Apply(StatusType t, float duration, float magnitude = 1f)
        {
            if (Shielded && t != StatusType.Shield && t != StatusType.SpeedBoost && t != StatusType.LowGravity)
                return;
            Entry e;
            if (_active.TryGetValue(t, out e))
            {
                e.Remaining = Mathf.Max(e.Remaining, duration);
                e.Magnitude = magnitude;
            }
            else
            {
                _active[t] = new Entry { Remaining = duration, Magnitude = magnitude };
            }
            Recompute();
        }

        public void Remove(StatusType t) { if (_active.Remove(t)) Recompute(); }

        public void Clear() { _active.Clear(); Recompute(); }

        private static readonly List<StatusType> Expired = new List<StatusType>();

        public void Tick(float dt)
        {
            if (_active.Count == 0) return;
            Expired.Clear();
            foreach (KeyValuePair<StatusType, Entry> kv in _active)
            {
                kv.Value.Remaining -= dt;
                if (kv.Value.Remaining <= 0f) Expired.Add(kv.Key);
            }
            for (int i = 0; i < Expired.Count; i++) _active.Remove(Expired[i]);
            if (Expired.Count > 0) Recompute();
        }

        private void Recompute()
        {
            float speed = 1f, grav = 1f, jump = 1f, scale = 1f, head = 1f;
            foreach (KeyValuePair<StatusType, Entry> kv in _active)
            {
                float m = kv.Value.Magnitude;
                switch (kv.Key)
                {
                    case StatusType.Slow: speed *= Mathf.Clamp(1f - 0.5f * m, 0.15f, 1f); break;
                    case StatusType.Gooed: speed *= 0.55f; jump *= 0.7f; break;
                    case StatusType.SpeedBoost: speed *= 1f + 0.6f * m; break;
                    case StatusType.LowGravity: grav *= 0.35f; break;
                    case StatusType.Floating: grav *= -0.12f; break;
                    case StatusType.Shrunk: scale *= 0.5f; speed *= 1.1f; break;
                    case StatusType.BigHead: head *= 2.6f; break;
                }
            }
            SpeedMultiplier = speed;
            GravityMultiplier = grav;
            JumpMultiplier = jump;
            ScaleTarget = scale;
            HeadScale = head;
        }

        public IEnumerable<KeyValuePair<StatusType, float>> Describe()
        {
            foreach (KeyValuePair<StatusType, Entry> kv in _active)
                yield return new KeyValuePair<StatusType, float>(kv.Key, kv.Value.Remaining);
        }
    }
}
