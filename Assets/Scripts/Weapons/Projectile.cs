using Daboiz.Actors;
using Daboiz.Combat;
using Daboiz.Core;
using UnityEngine;

namespace Daboiz.Weapons
{
    /// <summary>
    /// Kinematic projectile: integrates its own velocity/gravity, sphere-casts for hits,
    /// bounces off world geometry, homes, and lets modifiers hook every step.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public WeaponDefinition Def;
        public Actor Owner;
        public Vector3 Velocity;
        public float Scale = 1f;
        public float Seed;
        public int BouncesLeft;
        public float Radius;

        private float _age;
        private Vector3 _visualOffset;
        private Transform _visual;
        private Actor _homingTarget;
        private float _nextHomingSearch;
        private bool _done;

        public static Projectile Spawn(WeaponDefinition def, Actor owner, Vector3 position, Vector3 velocity,
            float scale, Vector3 visualOffset)
        {
            GameObject go = new GameObject("Proj_" + def.Projectile);
            if (Fx.Root != null) go.transform.SetParent(Fx.Root, false);
            go.transform.position = position;
            Projectile p = go.AddComponent<Projectile>();
            p.Def = def;
            p.Owner = owner;
            p.Velocity = velocity;
            p.Scale = scale;
            p.Seed = Random.Range(0f, 100f);
            p.BouncesLeft = def.Bounces;
            float sizeScale = def.Mode == FireMode.Charge ? Mathf.Lerp(0.6f, 1.6f, scale) : 1f;
            p.Radius = Mathf.Max(0.03f, def.ProjectileRadius * sizeScale);
            p._visualOffset = visualOffset;
            p._visual = ProjectileVisuals.Build(def.Projectile, def.ProjectileColor, p.Radius, go.transform);
            for (int i = 0; i < def.Modifiers.Count; i++) def.Modifiers[i].OnProjectileSpawned(p);
            return p;
        }

        private void Update()
        {
            if (_done) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _age += dt;
            if (_age >= Def.ProjectileLifetime) { Expire(); return; }

            Velocity += Physics.gravity * (Def.ProjectileGravity * dt);
            if (Def.HomingStrength > 0f) Home(dt);
            for (int i = 0; i < Def.Modifiers.Count; i++) Def.Modifiers[i].OnProjectileTick(this, dt);

            Vector3 pos = transform.position;
            Vector3 delta = Velocity * dt;
            float dist = delta.magnitude;
            if (dist > 0.0001f)
            {
                RaycastHit hit;
                Transform ignore = Owner != null ? Owner.transform : null;
                if (PhysicsUtil.SphereCastIgnoring(pos, Radius, delta / dist, dist, ignore, out hit))
                {
                    Actor hitActor = hit.collider.GetComponentInParent<Actor>();
                    bool prop = hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic;
                    if (hitActor == null && BouncesLeft > 0)
                    {
                        BouncesLeft--;
                        if (prop)
                            hit.collider.attachedRigidbody.AddForceAtPosition(Velocity.normalized * Def.Knockback * Def.PropForceScale * 0.5f,
                                hit.point, ForceMode.Impulse);
                        Velocity = Vector3.Reflect(Velocity, hit.normal) * Def.Bounciness;
                        transform.position = hit.point + hit.normal * (Radius + 0.02f);
                        for (int i = 0; i < Def.Modifiers.Count; i++) Def.Modifiers[i].OnProjectileBounce(this, hit.point, hit.normal);
                        if (Velocity.magnitude < 1.5f && Def.ProjectileGravity > 0f && !Def.ExplodeOnExpire) { Expire(); return; }
                    }
                    else
                    {
                        transform.position = hit.point;
                        Impact(hit);
                        return;
                    }
                }
                else
                {
                    transform.position = pos + delta;
                }
            }
            UpdateVisual(dt);
        }

        private void Home(float dt)
        {
            if (Time.time >= _nextHomingSearch)
            {
                _nextHomingSearch = Time.time + 0.2f;
                _homingTarget = null;
                float best = 45f;
                Vector3 fwd = Velocity.sqrMagnitude > 0.01f ? Velocity.normalized : transform.forward;
                for (int i = 0; i < Actor.All.Count; i++)
                {
                    Actor a = Actor.All[i];
                    if (a == Owner || !a.IsAlive) continue;
                    Vector3 to = a.CenterPosition - transform.position;
                    float d = to.magnitude;
                    if (d > best || Vector3.Angle(fwd, to) > 70f) continue;
                    best = d;
                    _homingTarget = a;
                }
            }
            if (_homingTarget != null && _homingTarget.IsAlive)
            {
                Vector3 to = (_homingTarget.CenterPosition - transform.position).normalized;
                float speed = Velocity.magnitude;
                Velocity = Vector3.RotateTowards(Velocity, to * speed, Def.HomingStrength * dt, 0f);
            }
        }

        private void UpdateVisual(float dt)
        {
            if (_visual == null) return;
            _visualOffset = Vector3.MoveTowards(_visualOffset, Vector3.zero, dt * 8f);
            _visual.position = transform.position + _visualOffset;
            switch (Def.Projectile)
            {
                case ProjectileKind.Disc:
                    _visual.Rotate(0f, 1080f * dt, 0f, Space.Self);
                    break;
                case ProjectileKind.Bubble:
                    float w = 1f + Mathf.Sin(Time.time * 9f + Seed) * 0.08f;
                    _visual.localScale = new Vector3(w, 2f - w, w) * (Radius * 2f);
                    break;
                case ProjectileKind.Grenade:
                case ProjectileKind.Banana:
                case ProjectileKind.Crate:
                    _visual.Rotate(360f * dt, 200f * dt, 0f, Space.Self);
                    break;
                case ProjectileKind.RubberDuck:
                    Vector3 flat = new Vector3(Velocity.x, 0f, Velocity.z);
                    if (flat.sqrMagnitude > 0.01f) _visual.rotation = Quaternion.LookRotation(flat);
                    break;
                default:
                    if (Velocity.sqrMagnitude > 0.01f) _visual.rotation = Quaternion.LookRotation(Velocity);
                    break;
            }
        }

        private void Impact(RaycastHit hit)
        {
            _done = true;
            HitResolver.ResolveDirectHit(Def, Owner, hit.collider, hit.point, hit.normal, Velocity, Scale);
            if (Def.Projectile != ProjectileKind.Bubble)
                Fx.Burst(hit.point, Def.ProjectileColor, 4, 3f, Radius * 0.6f + 0.04f);
            Destroy(gameObject);
        }

        private void Expire()
        {
            _done = true;
            if (Def.ExplodeOnExpire && Def.Explosive)
                HitResolver.Explode(Def, Owner, transform.position, Scale);
            else if (Def.Projectile == ProjectileKind.Bubble)
            {
                ProceduralAudio.Play(Sfx.Pop, transform.position, 0.5f, 1.4f);
                Fx.Burst(transform.position, new Color(0.8f, 0.95f, 1f), 6, 2f, 0.08f);
            }
            Destroy(gameObject);
        }
    }
}
