using System.Collections.Generic;
using Daboiz.Actors;
using Daboiz.Core;
using Daboiz.Weapons;
using UnityEngine;

namespace Daboiz.Combat
{
    /// <summary>
    /// Single authority for "a weapon hit something": damage, knockback, physics props, modifiers
    /// and explosions all go through here, filtered by the current mode's GameRules.
    /// (When netcode arrives this runs on the server only.)
    /// </summary>
    public static class HitResolver
    {
        /// <summary>attacker, victim (null for dummies/props), damage dealt.</summary>
        public static event System.Action<Actor, Actor, float> ActorHit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { ActorHit = null; }

        public static void ResolveDirectHit(WeaponDefinition def, Actor attacker, Collider col, Vector3 point,
            Vector3 normal, Vector3 dir, float scale)
        {
            GameRules rules = GameFlow.Rules;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            Actor target = col != null ? col.GetComponentInParent<Actor>() : null;
            IDamageable dmg = col != null ? col.GetComponentInParent<IDamageable>() : null;
            IKnockbackable kb = col != null ? col.GetComponentInParent<IKnockbackable>() : null;

            Vector3 impulse = dir * def.Knockback * scale + Vector3.up * (def.Knockback * def.KnockbackUp * scale);

            if (target != null)
            {
                if (rules.KnockbackEnabled && target.IsAlive)
                {
                    float k = rules.KnockbackScale * (target == attacker ? rules.SelfKnockbackScale : 1f);
                    target.ApplyKnockback(impulse * k, point, attacker);
                }
            }
            else if (kb != null)
            {
                kb.ApplyKnockback(impulse * def.PropForceScale, point, attacker);
            }
            else if (col != null && col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic)
            {
                col.attachedRigidbody.AddForceAtPosition(impulse * def.PropForceScale, point, ForceMode.Impulse);
            }

            float damage = def.Damage * scale * rules.DamageScale;
            if (target != null && target == attacker) damage *= rules.SelfDamageScale;
            if (rules.DamageEnabled && dmg != null && damage > 0f && dmg.IsAlive)
            {
                dmg.TakeDamage(new DamageInfo(damage, attacker, point, dir, def.DisplayName));
                if (ActorHit != null && attacker != null && target != attacker) ActorHit(attacker, target, damage);
            }
            else if (target != null && attacker != null && target != attacker && ActorHit != null)
            {
                ActorHit(attacker, target, 0f);
            }

            if (def.Modifiers.Count > 0)
            {
                ImpactContext ctx = new ImpactContext
                {
                    Def = def, Attacker = attacker, TargetActor = target, Collider = col,
                    Point = point, Normal = normal, Direction = dir, Scale = scale
                };
                for (int i = 0; i < def.Modifiers.Count; i++) def.Modifiers[i].OnImpact(ctx);
            }

            if (def.Explosive) Explode(def, attacker, point + normal * 0.15f, scale);
        }

        private static readonly Collider[] OverlapBuffer = new Collider[128];
        private static readonly HashSet<Object> Done = new HashSet<Object>();

        public static void Explode(WeaponDefinition def, Actor attacker, Vector3 center, float scale)
        {
            Explode(center, def.ExplosionRadius * Mathf.Lerp(0.6f, 1.15f, Mathf.Clamp01(scale)),
                def.ExplosionDamage * scale, def.ExplosionForce * Mathf.Lerp(0.6f, 1.15f, Mathf.Clamp01(scale)),
                def.PropForceScale, attacker, def.DisplayName, def.AccentColor);
        }

        public static void Explode(Vector3 center, float radius, float damage, float force, float propScale,
            Actor attacker, string source, Color color)
        {
            GameRules rules = GameFlow.Rules;
            Fx.Explosion(center, radius, Color.Lerp(color, new Color(1f, 0.55f, 0.1f), 0.5f));
            int n = Physics.OverlapSphereNonAlloc(center, radius, OverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            Done.Clear();
            for (int i = 0; i < n; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col == null) continue;
                Actor a = col.GetComponentInParent<Actor>();
                if (a != null)
                {
                    if (!Done.Add(a) || !a.IsAlive) continue;
                    Vector3 c = a.CenterPosition;
                    float dist = Vector3.Distance(center, c);
                    float fall = Mathf.Clamp01(1f - dist / (radius + 0.5f));
                    fall = 0.25f + 0.75f * fall;
                    Vector3 dir = (c - center);
                    dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.up;
                    bool self = a == attacker;
                    if (rules.KnockbackEnabled)
                    {
                        float k = rules.KnockbackScale * (self ? rules.SelfKnockbackScale : 1f);
                        a.ApplyKnockback((dir + Vector3.up * 0.45f).normalized * force * fall * k, c, attacker);
                    }
                    float dmg = damage * fall * rules.DamageScale * (self ? rules.SelfDamageScale : 1f);
                    if (rules.DamageEnabled && dmg > 0f)
                    {
                        a.TakeDamage(new DamageInfo(dmg, attacker, c, dir, source));
                        if (ActorHit != null && attacker != null && !self) ActorHit(attacker, a, dmg);
                    }
                    continue;
                }

                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic && Done.Add(rb))
                    rb.AddExplosionForce(force * propScale, center, radius, 0.6f, ForceMode.Impulse);

                IDamageable d = col.GetComponentInParent<IDamageable>();
                Component dc = d as Component;
                if (d != null && dc != null && Done.Add(dc) && d.IsAlive && damage > 0f)
                {
                    float dist = Vector3.Distance(center, col.ClosestPoint(center));
                    float fall = 0.25f + 0.75f * Mathf.Clamp01(1f - dist / radius);
                    d.TakeDamage(new DamageInfo(damage * fall, attacker, col.ClosestPoint(center), Vector3.up, source));
                    if (ActorHit != null && attacker != null) ActorHit(attacker, null, damage * fall);
                }
            }
        }
    }
}
