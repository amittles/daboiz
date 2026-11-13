using System.Collections.Generic;
using Daboiz.Actors;
using Daboiz.Combat;
using Daboiz.Core;
using UnityEngine;

namespace Daboiz.Weapons
{
    /// <summary>A real Rigidbody projectile (bowling ball, crate) that physically bowls things over.</summary>
    public class PhysicalProjectile : MonoBehaviour
    {
        public WeaponDefinition Def;
        public Actor Owner;
        public float Scale = 1f;
        private float _age;
        private Rigidbody _rb;
        private readonly HashSet<Actor> _hitActors = new HashSet<Actor>();

        public static PhysicalProjectile Spawn(WeaponDefinition def, Actor owner, Vector3 pos, Vector3 velocity, float scale)
        {
            bool crate = def.Projectile == ProjectileKind.Crate;
            float size = Mathf.Max(0.1f, def.ProjectileRadius * 2f);
            GameObject go = Prims.Make(crate ? PrimitiveType.Cube : PrimitiveType.Sphere, Fx.Root, "PhysProj_" + def.Projectile,
                pos, Vector3.one * size, def.ProjectileColor, true, Random.rotation);
            if (crate) go.GetComponent<Renderer>().sharedMaterial = Mats.Get(new Color(0.65f, 0.45f, 0.25f));
            else go.GetComponent<Renderer>().sharedMaterial = Mats.Get(def.ProjectileColor, 0.85f);
            PhysicalProjectile p = go.AddComponent<PhysicalProjectile>();
            p.Def = def;
            p.Owner = owner;
            p.Scale = scale;
            p._rb = go.AddComponent<Rigidbody>();
            p._rb.mass = def.PhysicalMass;
            p._rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            p._rb.interpolation = RigidbodyInterpolation.Interpolate;
            RbCompat.SetVelocity(p._rb, velocity);
            p._rb.angularVelocity = Random.insideUnitSphere * 5f;
            Collider c = go.GetComponent<Collider>();
            if (owner != null && owner.Motor != null && owner.Motor.Controller != null)
                Physics.IgnoreCollision(c, owner.Motor.Controller, true);
            return p;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age > Def.ProjectileLifetime || transform.position.y < -80f) Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            Actor a = collision.collider.GetComponentInParent<Actor>();
            if (a != null && a != Owner && a.IsAlive && _hitActors.Add(a))
            {
                Vector3 v = RbCompat.GetVelocity(_rb);
                float speedScale = Mathf.Clamp01(v.magnitude / Mathf.Max(1f, Def.ProjectileSpeed));
                Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : a.CenterPosition;
                HitResolver.ResolveDirectHit(Def, Owner, collision.collider, point, Vector3.up, v, Scale * Mathf.Max(0.3f, speedScale));
            }
            else if (a == null && collision.relativeVelocity.magnitude > 6f)
            {
                ProceduralAudio.Play(Sfx.Land, transform.position, Mathf.Clamp01(collision.relativeVelocity.magnitude / 20f), 0.6f);
            }
        }
    }
}
