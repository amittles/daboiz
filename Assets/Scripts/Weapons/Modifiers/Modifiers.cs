using Daboiz.Combat;
using Daboiz.Core;
using Daboiz.Props;
using UnityEngine;

namespace Daboiz.Weapons.Modifiers
{
    /// <summary>Fires slow, floaty soap bubbles that pop and make the target float away.</summary>
    public class BubbleProjectiles : WeaponModifier
    {
        public override string Name { get { return "Bubbles"; } }
        public override void ModifyStats(WeaponDefinition d)
        {
            EnsureProjectile(d, 14f);
            d.Projectile = ProjectileKind.Bubble;
            d.ProjectileSpeed = Mathf.Clamp(d.ProjectileSpeed * 0.4f, 8f, 18f);
            d.ProjectileGravity = -0.08f;
            d.ProjectileLifetime = 6f;
            d.ProjectileRadius = Mathf.Max(0.25f, d.ProjectileRadius * 2.5f);
            d.ProjectileColor = new Color(0.7f, 0.9f, 1f, 0.45f);
            d.FireSound = Sfx.Pop;
            d.Damage *= 0.6f;
            d.Explosive = false;
        }
        public override void OnProjectileTick(Projectile p, float dt)
        {
            // Wobble + air drag
            p.Velocity += new Vector3(Mathf.Sin(Time.time * 7f + p.Seed), Mathf.Cos(Time.time * 5f + p.Seed), 0f) * (2f * dt);
            p.Velocity *= 1f - 0.35f * dt;
        }
        public override void OnImpact(ImpactContext ctx)
        {
            ProceduralAudio.Play(Sfx.Pop, ctx.Point, 0.8f, Random.Range(0.9f, 1.3f));
            Fx.Burst(ctx.Point, new Color(0.8f, 0.95f, 1f), 8, 3f, 0.1f);
            if (ctx.EffectsAllowed)
            {
                ctx.ApplyStatus(StatusType.Floating, 2.2f);
                ctx.TargetActor.Motor.AddImpulse(Vector3.up * 4f);
            }
        }
    }

    /// <summary>Bouncy rubber ducks that squeak on every bounce.</summary>
    public class RubberDuckProjectiles : WeaponModifier
    {
        public override string Name { get { return "Rubber Ducks"; } }
        public override void ModifyStats(WeaponDefinition d)
        {
            EnsureProjectile(d, 28f);
            d.Projectile = ProjectileKind.RubberDuck;
            d.ProjectileGravity = Mathf.Max(d.ProjectileGravity, 1f);
            d.Bounces += 3;
            d.Bounciness = 0.8f;
            d.ProjectileRadius = Mathf.Max(d.ProjectileRadius, 0.2f);
            d.ProjectileColor = new Color(1f, 0.85f, 0.1f);
            d.FireSound = Sfx.Squeak;
            d.Knockback *= 1.3f;
        }
        public override void OnProjectileBounce(Projectile p, Vector3 point, Vector3 normal)
        {
            ProceduralAudio.Play(Sfx.Squeak, point, 0.6f, Random.Range(0.85f, 1.4f));
        }
        public override void OnImpact(ImpactContext ctx)
        {
            ProceduralAudio.Play(Sfx.Squeak, ctx.Point, 0.9f, Random.Range(0.7f, 1.1f));
        }
    }

    /// <summary>Leaves sticky, slowing goo on floors and on whoever it hits.</summary>
    public class GooSplatter : WeaponModifier
    {
        private readonly Color _color;
        private readonly string _name;
        public GooSplatter() : this(new Color(0.45f, 0.95f, 0.25f), "Goo") { }
        public GooSplatter(Color color, string name) { _color = color; _name = name; }
        public override string Name { get { return _name; } }
        public override void ModifyStats(WeaponDefinition d)
        {
            if (d.Mode == FireMode.Projectile || d.ChargeFiresProjectile)
            {
                if (d.Projectile == ProjectileKind.Ball || d.Projectile == ProjectileKind.Plasma)
                    d.Projectile = ProjectileKind.GooBlob;
                d.ProjectileColor = _color;
            }
            d.AccentColor = _color;
        }
        public override void OnImpact(ImpactContext ctx)
        {
            ProceduralAudio.Play(Sfx.Splat, ctx.Point, 0.7f, Random.Range(0.8f, 1.2f));
            GooPuddle.Spawn(ctx.Point, ctx.Normal, _color, 1.1f * Mathf.Max(0.6f, ctx.Scale));
            ctx.ApplyStatus(StatusType.Gooed, 2.5f);
            ctx.ApplyStatus(StatusType.Slow, 2.5f, 0.6f);
        }
    }

    public class Bouncy : WeaponModifier
    {
        public override string Name { get { return "Bouncy"; } }
        public override void ModifyStats(WeaponDefinition d)
        {
            d.Bounces += 3;
            d.Bounciness = Mathf.Max(d.Bounciness, 0.85f);
            d.Ricochets += 2;
            if (d.Mode == FireMode.Projectile) d.ProjectileLifetime += 2f;
        }
        public override void OnProjectileBounce(Projectile p, Vector3 point, Vector3 normal)
        {
            ProceduralAudio.Play(Sfx.Click, point, 0.4f, Random.Range(1.2f, 1.6f));
        }
    }

    public class Explosive : WeaponModifier
    {
        public override string Name { get { return "Explosive"; } }
        public override void ModifyStats(WeaponDefinition d)
        {
            d.Explosive = true;
            d.ExplosionRadius = Mathf.Max(d.ExplosionRadius, 3.5f);
            d.ExplosionDamage = Mathf.Max(d.ExplosionDamage, d.Damage * 1.5f);
            d.ExplosionForce = Mathf.Max(d.ExplosionForce, 12f + d.Knockback);
            if (d.Mode != FireMode.Projectile) d.FireRate = Mathf.Min(d.FireRate, 4f);
        }
    }

    public class Ricochet : WeaponModifier
    {
        public override string Name { get { return "Ricochet"; } }
        public override void ModifyStats(WeaponDefinition d)
        {
            d.Ricochets += 3;
            d.Bounces += 2;
            d.Bounciness = Mathf.Max(d.Bounciness, 0.95f);
        }
    }

    public class Homing : WeaponModifier
    {
        public override string Name { get { return "Homing"; } }
        public override void ModifyStats(WeaponDefinition d)
        {
            EnsureProjectile(d, 26f);
            d.HomingStrength = Mathf.Max(d.HomingStrength, 3.5f);
            if (d.Projectile == ProjectileKind.Ball) d.Projectile = ProjectileKind.Needle;
        }
    }

    public class BigHead : WeaponModifier
    {
        public override string Name { get { return "Big Head"; } }
        public override void OnImpact(ImpactContext ctx) { ctx.ApplyStatus(StatusType.BigHead, 8f); }
    }

    /// <summary>No damage, triple shove.</summary>
    public class KnockbackOnly : WeaponModifier
    {
        public override string Name { get { return "Knockback Only"; } }
        public override void ModifyStats(WeaponDefinition d)
        {
            d.Damage = 0f;
            d.ExplosionDamage = 0f;
            d.Knockback = Mathf.Max(3f, d.Knockback * 3f);
            d.ExplosionForce *= 1.5f;
            d.PropForceScale *= 1.5f;
        }
    }

    public class Tiny : WeaponModifier
    {
        public override string Name { get { return "Tiny"; } }
        public override void ModifyStats(WeaponDefinition d)
        {
            d.ProjectileRadius *= 0.4f;
            d.Damage *= 0.7f;
            d.FireRate *= 1.5f;
            d.ViewmodelScale *= 0.55f;
            d.Knockback *= 0.6f;
            d.FirePitch *= 1.8f;
        }
    }

    public class Huge : WeaponModifier
    {
        public override string Name { get { return "Huge"; } }
        public override void ModifyStats(WeaponDefinition d)
        {
            d.ProjectileRadius *= 3f;
            d.ProjectileSpeed *= 0.65f;
            d.Damage *= 1.5f;
            d.Knockback *= 2f;
            d.FireRate *= 0.6f;
            d.ViewmodelScale *= 1.5f;
            d.PhysicalMass *= 2f;
            d.RecoilKick *= 1.8f;
            d.FirePitch *= 0.6f;
            d.SelfKnockback += 2f;
        }
    }

    public class LowGravity : WeaponModifier
    {
        public override string Name { get { return "Low-Grav"; } }
        public override void ModifyStats(WeaponDefinition d) { d.ProjectileGravity *= 0.2f; }
        public override void OnImpact(ImpactContext ctx) { ctx.ApplyStatus(StatusType.LowGravity, 6f); }
    }

    public class ShrinkTarget : WeaponModifier
    {
        public override string Name { get { return "Shrink Ray"; } }
        public override void ModifyStats(WeaponDefinition d) { d.AccentColor = new Color(0.8f, 0.3f, 1f); }
        public override void OnImpact(ImpactContext ctx) { ctx.ApplyStatus(StatusType.Shrunk, 6f); }
    }

    public class LaunchTarget : WeaponModifier
    {
        public override string Name { get { return "Launch"; } }
        public override void OnImpact(ImpactContext ctx)
        {
            if (ctx.TargetActor != null && ctx.TargetActor.IsAlive && GameFlow.Rules.KnockbackEnabled)
            {
                ctx.TargetActor.ApplyKnockback(Vector3.up * 13f * Mathf.Max(0.5f, ctx.Scale), ctx.Point, ctx.Attacker);
                ProceduralAudio.Play(Sfx.Whoosh, ctx.Point, 0.8f, 1.3f);
            }
        }
    }

    public class ConfettiOnHit : WeaponModifier
    {
        public override string Name { get { return "Confetti"; } }
        public override void OnImpact(ImpactContext ctx)
        {
            Fx.Confetti(ctx.Point, ctx.TargetActor != null ? 30 : 12);
            if (ctx.TargetActor != null) ProceduralAudio.Play(Sfx.Ding, ctx.Point, 0.5f, Random.Range(0.9f, 1.4f));
        }
    }

    public class SlowTarget : WeaponModifier
    {
        public override string Name { get { return "Slow"; } }
        public override void ModifyStats(WeaponDefinition d) { d.AccentColor = new Color(0.4f, 0.6f, 1f); }
        public override void OnImpact(ImpactContext ctx) { ctx.ApplyStatus(StatusType.Slow, 3f, 1f); }
    }

    public class ReverseControls : WeaponModifier
    {
        public override string Name { get { return "Reverse Controls"; } }
        public override void ModifyStats(WeaponDefinition d) { d.AccentColor = new Color(1f, 0.3f, 0.8f); }
        public override void OnImpact(ImpactContext ctx) { ctx.ApplyStatus(StatusType.ReverseControls, 3.5f); }
    }

    public class FreezeTarget : WeaponModifier
    {
        public override string Name { get { return "Freeze"; } }
        public override void ModifyStats(WeaponDefinition d)
        {
            d.AccentColor = new Color(0.6f, 0.95f, 1f);
            d.ProjectileColor = new Color(0.7f, 0.95f, 1f);
        }
        public override void OnImpact(ImpactContext ctx)
        {
            if (!ctx.EffectsAllowed) return;
            ctx.ApplyStatus(StatusType.Frozen, 1.3f);
            ProceduralAudio.Play(Sfx.Zap, ctx.Point, 0.6f, 1.6f);
        }
    }

    public class Rapid : WeaponModifier
    {
        public override string Name { get { return "Rapid"; } }
        public override void ModifyStats(WeaponDefinition d)
        {
            d.FireRate *= 2f;
            d.Damage *= 0.6f;
            d.MagazineSize = Mathf.RoundToInt(d.MagazineSize * 1.5f);
            d.Automatic = true;
        }
    }

    public class SpeedOnHit : WeaponModifier
    {
        public override string Name { get { return "Hit = Zoom"; } }
        public override void OnImpact(ImpactContext ctx)
        {
            if (ctx.Attacker != null && ctx.TargetActor != null && ctx.TargetActor != ctx.Attacker && ctx.Attacker.IsAlive)
                ctx.Attacker.Status.Apply(StatusType.SpeedBoost, 2f, 0.8f);
        }
    }
}
