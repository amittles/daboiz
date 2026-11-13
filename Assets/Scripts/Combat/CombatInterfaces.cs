using UnityEngine;

namespace Daboiz.Combat
{
    public struct DamageInfo
    {
        public float Amount;
        public Actors.Actor Attacker;
        public Vector3 Point;
        public Vector3 Direction;
        public string Source; // weapon name

        public DamageInfo(float amount, Actors.Actor attacker, Vector3 point, Vector3 direction, string source)
        {
            Amount = amount; Attacker = attacker; Point = point; Direction = direction; Source = source;
        }
    }

    /// <summary>Anything that can lose health (players, bots, dummies, explosive barrels).</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }

    /// <summary>Anything that reacts to being shoved (players, bots, physics props).</summary>
    public interface IKnockbackable
    {
        /// <param name="impulse">For actors: velocity change (m/s). For props: physics impulse.</param>
        void ApplyKnockback(Vector3 impulse, Vector3 point, Actors.Actor source);
    }
}
