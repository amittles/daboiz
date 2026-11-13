using Daboiz.Actors;
using Daboiz.Combat;
using Daboiz.Core;
using UnityEngine;

namespace Daboiz.Weapons
{
    /// <summary>Everything a modifier needs to react to a hit.</summary>
    public class ImpactContext
    {
        public WeaponDefinition Def;
        public Actor Attacker;
        public Actor TargetActor;     // null if we hit the world / a prop
        public Collider Collider;
        public Vector3 Point;
        public Vector3 Normal;
        public Vector3 Direction;
        public float Scale = 1f;      // charge scale / pellet share

        public bool EffectsAllowed
        {
            get { return GameFlow.Rules.EffectsEnabled && TargetActor != null && TargetActor.IsAlive; }
        }

        public void ApplyStatus(StatusType t, float duration, float magnitude = 1f)
        {
            if (!EffectsAllowed) return;
            TargetActor.Status.Apply(t, duration, magnitude);
        }
    }

    /// <summary>
    /// Composable weapon mutator. Modifiers are stateless and shared between weapons:
    /// they rewrite stats once (ModifyStats) and hook projectile / impact events.
    /// </summary>
    public abstract class WeaponModifier
    {
        public abstract string Name { get; }
        public virtual void ModifyStats(WeaponDefinition d) { }
        public virtual void OnProjectileSpawned(Projectile p) { }
        public virtual void OnProjectileTick(Projectile p, float dt) { }
        public virtual void OnProjectileBounce(Projectile p, Vector3 point, Vector3 normal) { }
        public virtual void OnImpact(ImpactContext ctx) { }

        /// <summary>Utility for modifiers that need a travelling projectile.</summary>
        protected static void EnsureProjectile(WeaponDefinition d, float speed)
        {
            if (d.Mode == FireMode.Hitscan || d.Mode == FireMode.Shotgun || d.Mode == FireMode.Beam)
            {
                if (d.Mode == FireMode.Beam) { d.FireRate = Mathf.Min(d.FireRate, 8f); d.Automatic = true; }
                d.Mode = FireMode.Projectile;
                d.ProjectileSpeed = speed;
            }
            if (d.Mode == FireMode.Charge) d.ChargeFiresProjectile = true;
        }
    }
}
