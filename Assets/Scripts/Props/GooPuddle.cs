using System.Collections.Generic;
using Daboiz.Core;
using UnityEngine;

namespace Daboiz.Props
{
    /// <summary>Sticky goo splat on a surface. Floor puddles slow anyone standing in them.</summary>
    public class GooPuddle : MonoBehaviour
    {
        private static readonly List<GooPuddle> Active = new List<GooPuddle>();
        private const int MaxPuddles = 48;

        public float Radius = 1f;
        public float Life = 12f;
        private float _age;
        private bool _floor;
        private Vector3 _baseScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active.Clear(); }

        public static void Spawn(Vector3 point, Vector3 normal, Color color, float radius)
        {
            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            while (Active.Count >= MaxPuddles)
            {
                GooPuddle old = Active[0];
                Active.RemoveAt(0);
                if (old != null) Destroy(old.gameObject);
            }
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject g = Prims.Make(PrimitiveType.Cylinder, Fx.Root, "Goo", point + normal * 0.02f,
                new Vector3(radius * 2f, 0.01f, radius * 2f * Random.Range(0.8f, 1.1f)), color, false, rot);
            g.GetComponent<Renderer>().sharedMaterial = Mats.Get(color, 0.95f);
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GooPuddle p = g.AddComponent<GooPuddle>();
            p.Radius = radius;
            p._floor = normal.y > 0.6f;
            p._baseScale = g.transform.localScale;
            Active.Add(p);
        }

        public static bool IsInGoo(Vector3 feet)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                GooPuddle p = Active[i];
                if (p == null || !p._floor) continue;
                Vector3 d = feet - p.transform.position;
                if (Mathf.Abs(d.y) > 0.5f) continue;
                d.y = 0f;
                if (d.sqrMagnitude <= p.Radius * p.Radius) return true;
            }
            return false;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float fade = Mathf.InverseLerp(Life, Life - 1.5f, _age);
            transform.localScale = new Vector3(_baseScale.x * fade, _baseScale.y, _baseScale.z * fade);
            if (_age >= Life) Destroy(gameObject);
        }

        private void OnDestroy() { Active.Remove(this); }
    }
}
