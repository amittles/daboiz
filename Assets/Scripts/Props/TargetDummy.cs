using Daboiz.Actors;
using Daboiz.Combat;
using Daboiz.Core;
using UnityEngine;

namespace Daboiz.Props
{
    /// <summary>Wobbly training dummy: shows damage numbers, regenerates, springs back from knockback.</summary>
    public class TargetDummy : MonoBehaviour, IDamageable, IKnockbackable
    {
        public float MaxHealth = 200f;
        public float Health = 200f;
        private Transform _visual;
        private Vector3 _tilt;       // spring state (degrees around x/z)
        private Vector3 _tiltVel;
        private float _lastHit;
        private float _dpsWindowStart;
        private float _dpsAccum;

        public bool IsAlive { get { return true; } }

        public static TargetDummy Create(Transform parent, Vector3 pos, float yaw)
        {
            GameObject root = new GameObject("TargetDummy");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Prims.Make(PrimitiveType.Cylinder, root.transform, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.05f, 0.9f),
                new Color(0.3f, 0.3f, 0.3f), false, null, true);
            Transform vis = new GameObject("Wobble").transform;
            vis.SetParent(root.transform, false);
            GameObject body = Prims.Make(PrimitiveType.Capsule, vis, "Body", new Vector3(0f, 0.95f, 0f), new Vector3(0.8f, 0.85f, 0.8f),
                new Color(0.95f, 0.85f, 0.6f), true, null, true);
            Prims.Make(PrimitiveType.Sphere, vis, "Head", new Vector3(0f, 2.05f, 0f), Vector3.one * 0.55f, new Color(0.95f, 0.85f, 0.6f), true, null, true);
            Prims.Visual(PrimitiveType.Cylinder, vis, new Vector3(0f, 1.2f, 0.36f), new Vector3(0.5f, 0.01f, 0.5f), Color.red, Quaternion.Euler(90f, 0f, 0f));
            Prims.Visual(PrimitiveType.Cylinder, vis, new Vector3(0f, 1.2f, 0.37f), new Vector3(0.3f, 0.01f, 0.3f), Color.white, Quaternion.Euler(90f, 0f, 0f));
            Prims.Visual(PrimitiveType.Cylinder, vis, new Vector3(0f, 1.2f, 0.38f), new Vector3(0.12f, 0.01f, 0.12f), Color.red, Quaternion.Euler(90f, 0f, 0f));
            body.name = "Body";
            TargetDummy d = root.AddComponent<TargetDummy>();
            d._visual = vis;
            return d;
        }

        public void TakeDamage(DamageInfo info)
        {
            Health -= info.Amount;
            _lastHit = Time.time;
            if (Time.time - _dpsWindowStart > 2f) { _dpsWindowStart = Time.time; _dpsAccum = 0f; }
            _dpsAccum += info.Amount;
            if (GameFlow.Instance != null)
                GameFlow.Instance.Hud.PopupText(info.Point, Mathf.RoundToInt(info.Amount).ToString(), new Color(1f, 0.9f, 0.3f));
            if (Health <= 0f)
            {
                Health = MaxHealth;
                Fx.Confetti(transform.position + Vector3.up * 2f, 40);
                ProceduralAudio.Play(Sfx.Ding, transform.position, 0.8f, 1f);
            }
        }

        public void ApplyKnockback(Vector3 impulse, Vector3 point, Actor source)
        {
            Vector3 local = transform.InverseTransformDirection(impulse);
            _tiltVel += new Vector3(local.z, 0f, -local.x) * 25f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            // Damped spring back to upright
            _tiltVel += (-_tilt * 60f - _tiltVel * 6f) * dt;
            _tilt += _tiltVel * dt;
            _tilt = Vector3.ClampMagnitude(_tilt, 70f);
            if (_visual != null) _visual.localRotation = Quaternion.Euler(_tilt.x, 0f, _tilt.z);
            if (Time.time - _lastHit > 3f) Health = Mathf.MoveTowards(Health, MaxHealth, 60f * dt);
        }
    }
}
