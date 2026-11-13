using UnityEngine;

namespace Daboiz.Core
{
    /// <summary>Simple self-animating effect piece (no physics, no colliders).</summary>
    public class FxPiece : MonoBehaviour
    {
        public Vector3 Velocity;
        public float Gravity = 9.81f;
        public float Life = 1f;
        public float Drag = 1f;
        public Vector3 Spin;
        public bool ShrinkOut = true;
        public float GrowTo; // >0: scale up towards this (explosion sphere)
        private float _age;
        private Vector3 _startScale;

        private void Start() { _startScale = transform.localScale; }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= Life) { Destroy(gameObject); return; }
            Velocity.y -= Gravity * dt;
            Velocity *= Mathf.Clamp01(1f - Drag * dt);
            transform.position += Velocity * dt;
            if (Spin != Vector3.zero) transform.Rotate(Spin * dt, Space.Self);
            float u = _age / Life;
            if (GrowTo > 0f) transform.localScale = Vector3.one * Mathf.Lerp(_startScale.x, GrowTo, Mathf.Sqrt(u)) * (1f - u * u);
            else if (ShrinkOut) transform.localScale = _startScale * (1f - u * u);
        }
    }

    public class FxLine : MonoBehaviour
    {
        public float Life = 0.08f;
        private float _age;
        private LineRenderer _lr;
        private float _w;
        private void Start() { _lr = GetComponent<LineRenderer>(); _w = _lr.startWidth; }
        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Life) { Destroy(gameObject); return; }
            float k = 1f - _age / Life;
            _lr.startWidth = _w * k;
            _lr.endWidth = _w * k;
        }
    }

    public class FxLight : MonoBehaviour
    {
        public float Life = 0.06f;
        private float _age;
        private Light _l;
        private float _i;
        private void Start() { _l = GetComponent<Light>(); _i = _l.intensity; }
        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Life) { Destroy(gameObject); return; }
            _l.intensity = _i * (1f - _age / Life);
        }
    }

    /// <summary>Static effect spawners. Everything is parented to the current level so it gets cleaned up.</summary>
    public static class Fx
    {
        public static Transform Root
        {
            get { return GameFlow.Instance != null ? GameFlow.Instance.LevelRoot : null; }
        }

        public static void Burst(Vector3 pos, Color color, int count, float speed, float size)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject g = Prims.Make(PrimitiveType.Cube, Root, "fx", pos, Vector3.one * size, color, false);
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                FxPiece p = g.AddComponent<FxPiece>();
                p.Velocity = Random.onUnitSphere * speed * Random.Range(0.5f, 1f);
                p.Life = Random.Range(0.3f, 0.6f);
                p.Gravity = 4f;
            }
        }

        private static readonly Color[] ConfettiColors =
        {
            new Color(1f, 0.2f, 0.3f), new Color(1f, 0.85f, 0.1f), new Color(0.2f, 0.8f, 1f),
            new Color(0.4f, 1f, 0.3f), new Color(0.9f, 0.3f, 1f), Color.white
        };

        public static void Confetti(Vector3 pos, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Color c = ConfettiColors[Random.Range(0, ConfettiColors.Length)];
                GameObject g = Prims.Make(PrimitiveType.Cube, Root, "confetti", pos, new Vector3(0.12f, 0.01f, 0.07f), c, false);
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                FxPiece p = g.AddComponent<FxPiece>();
                Vector3 v = Random.onUnitSphere; v.y = Mathf.Abs(v.y) + 0.5f;
                p.Velocity = v * Random.Range(3f, 7f);
                p.Gravity = 3f;
                p.Drag = 2.5f;
                p.Life = Random.Range(1.2f, 2.2f);
                p.ShrinkOut = false;
                p.Spin = Random.insideUnitSphere * 900f;
            }
        }

        public static void Tracer(Vector3 from, Vector3 to, Color color, float width, float life)
        {
            GameObject g = new GameObject("tracer");
            if (Root != null) g.transform.SetParent(Root, false);
            LineRenderer lr = g.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = width;
            lr.endWidth = width * 0.6f;
            lr.sharedMaterial = Mats.Line(color);
            lr.startColor = color;
            lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            g.AddComponent<FxLine>().Life = life;
        }

        public static void Flash(Vector3 pos, Color color, float intensity, float range, float life)
        {
            GameObject g = new GameObject("flash");
            if (Root != null) g.transform.SetParent(Root, false);
            g.transform.position = pos;
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            g.AddComponent<FxLight>().Life = life;
        }

        public static void Explosion(Vector3 pos, float radius, Color color)
        {
            GameObject s = Prims.Make(PrimitiveType.Sphere, Root, "boom", pos, Vector3.one * 0.5f, color, false);
            s.GetComponent<Renderer>().sharedMaterial = Mats.Emissive(color, 2f);
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            FxPiece p = s.AddComponent<FxPiece>();
            p.Gravity = 0f;
            p.Life = 0.35f;
            p.GrowTo = radius * 2f;
            Flash(pos, color, 6f, radius * 4f, 0.25f);
            Burst(pos, Color.Lerp(color, Color.gray, 0.5f), 10, radius * 3f, 0.25f);
            ProceduralAudio.Play(Sfx.Boom, pos, 1f, Random.Range(0.85f, 1.1f));
        }
    }
}
