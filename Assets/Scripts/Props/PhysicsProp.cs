using Daboiz.Actors;
using Daboiz.Combat;
using Daboiz.Core;
using UnityEngine;

namespace Daboiz.Props
{
    public enum PropKind { Can, Crate, Barrel, ExplosiveBarrel, Pin, Ball, Chair, Generic }

    /// <summary>
    /// Physics object that reacts to weapon hits with AddForceAtPosition (realistic spin + travel).
    /// Explosive barrels take damage and blow up.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicsProp : MonoBehaviour, IKnockbackable, IDamageable
    {
        public PropKind Kind = PropKind.Generic;
        public float ForceMultiplier = 1f;
        public float Health = 30f;
        public bool Explosive;
        public Rigidbody Body { get; private set; }
        public Vector3 StartPosition { get; private set; }
        public Quaternion StartRotation { get; private set; }
        private bool _exploding;
        private float _lastHitSound;

        public bool IsAlive { get { return !_exploding; } }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            StartPosition = transform.position;
            StartRotation = transform.rotation;
        }

        public void ApplyKnockback(Vector3 impulse, Vector3 point, Actor source)
        {
            if (Body == null || Body.isKinematic) return;
            Body.AddForceAtPosition(impulse * ForceMultiplier, point, ForceMode.Impulse);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!Explosive || _exploding) return;
            Health -= info.Amount;
            if (Health <= 0f)
            {
                _exploding = true;
                // Defer one frame so chain reactions don't recurse inside another explosion.
                StartCoroutine(ExplodeNextFrame(info.Attacker));
            }
        }

        private System.Collections.IEnumerator ExplodeNextFrame(Actor attacker)
        {
            yield return null;
            yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            HitResolver.Explode(transform.position, 5f, 80f, 18f, 2f, attacker, "Explosive Barrel", new Color(1f, 0.4f, 0.1f));
            Destroy(gameObject);
        }

        public void ResetToStart()
        {
            Body.isKinematic = true;
            transform.SetPositionAndRotation(StartPosition, StartRotation);
            Body.isKinematic = false;
            RbCompat.SetVelocity(Body, Vector3.zero);
            Body.angularVelocity = Vector3.zero;
            Body.Sleep();
        }

        private void OnCollisionEnter(Collision c)
        {
            float v = c.relativeVelocity.magnitude;
            if (v > 4f && Time.time - _lastHitSound > 0.15f)
            {
                _lastHitSound = Time.time;
                float pitch = Kind == PropKind.Can ? 2f : Kind == PropKind.Pin ? 1.4f : 0.7f;
                ProceduralAudio.Play(Sfx.Land, transform.position, Mathf.Clamp01(v / 15f) * 0.6f, pitch);
            }
        }

        // ------------------------------------------------------------- factories

        public static PhysicsProp Create(PropKind kind, Transform parent, Vector3 pos, Quaternion rot)
        {
            GameObject go;
            float mass;
            Color col;
            switch (kind)
            {
                case PropKind.Can:
                    col = Random.value < 0.5f ? new Color(0.85f, 0.1f, 0.1f) : new Color(0.1f, 0.5f, 0.9f);
                    go = Prims.Make(PrimitiveType.Cylinder, parent, "Can", pos, new Vector3(0.14f, 0.12f, 0.14f), col, true, rot);
                    ReplaceWithBox(go, new Vector3(1f, 2f, 1f));
                    mass = 0.35f;
                    break;
                case PropKind.Crate:
                    go = Prims.Make(PrimitiveType.Cube, parent, "Crate", pos, Vector3.one * 0.8f, new Color(0.65f, 0.45f, 0.25f), true, rot);
                    mass = 6f;
                    break;
                case PropKind.Barrel:
                    go = Prims.Make(PrimitiveType.Cylinder, parent, "Barrel", pos, new Vector3(0.6f, 0.45f, 0.6f), new Color(0.2f, 0.45f, 0.3f), true, rot);
                    ReplaceWithBox(go, new Vector3(0.9f, 2f, 0.9f));
                    mass = 10f;
                    break;
                case PropKind.ExplosiveBarrel:
                    go = Prims.Make(PrimitiveType.Cylinder, parent, "ExplosiveBarrel", pos, new Vector3(0.6f, 0.45f, 0.6f), new Color(0.9f, 0.15f, 0.1f), true, rot);
                    Prims.Visual(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(1.02f, 0.3f, 1.02f), Color.yellow);
                    ReplaceWithBox(go, new Vector3(0.9f, 2f, 0.9f));
                    mass = 10f;
                    break;
                case PropKind.Pin:
                    go = Prims.Make(PrimitiveType.Capsule, parent, "Pin", pos, new Vector3(0.24f, 0.38f, 0.24f), Color.white, true, rot);
                    Prims.Visual(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.02f, 0.06f, 1.02f), Color.red);
                    ReplaceWithBox(go, new Vector3(0.75f, 2f, 0.75f));
                    mass = 1.5f;
                    break;
                case PropKind.Ball:
                    go = Prims.Make(PrimitiveType.Sphere, parent, "Ball", pos, Vector3.one * 0.5f, new Color(1f, 0.4f, 0.7f), true, rot);
                    mass = 1f;
                    break;
                case PropKind.Chair:
                    go = Prims.Make(PrimitiveType.Cube, parent, "Chair", pos, new Vector3(0.5f, 0.1f, 0.5f), new Color(0.15f, 0.15f, 0.2f), true, rot);
                    GameObject back = Prims.Make(PrimitiveType.Cube, go.transform, "Back", new Vector3(0f, 4f, -0.45f), new Vector3(1f, 8f, 0.15f),
                        new Color(0.15f, 0.15f, 0.2f), true, null, true);
                    back.transform.localScale = new Vector3(1f, 8f, 0.15f);
                    mass = 4f;
                    break;
                default:
                    go = Prims.Make(PrimitiveType.Cube, parent, "Prop", pos, Vector3.one * 0.5f, Color.gray, true, rot);
                    mass = 2f;
                    break;
            }
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            RbCompat.SetDrag(rb, 0.05f, 0.1f);
            PhysicsProp p = go.AddComponent<PhysicsProp>();
            p.Kind = kind;
            p.Explosive = kind == PropKind.ExplosiveBarrel;
            p.Health = 25f;
            p.ForceMultiplier = kind == PropKind.Can ? 0.35f : 1f;
            return p;
        }

        /// <summary>Cylinders come with capsule colliders; swap to a box so cans stand and stack.</summary>
        private static void ReplaceWithBox(GameObject go, Vector3 size)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            BoxCollider b = go.AddComponent<BoxCollider>();
            b.size = size;
        }
    }
}
