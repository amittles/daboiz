using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Daboiz.Core
{
    /// <summary>Runtime material factory/cache (built-in render pipeline, Standard shader).</summary>
    public static class Mats
    {
        private static Material _base;
        private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _base = null;
            Cache.Clear();
        }

        private static Material Base
        {
            get
            {
                if (_base != null) return _base;
                Shader s = Shader.Find("Standard");
                if (s != null)
                {
                    _base = new Material(s);
                }
                else
                {
                    // Fallback: clone Unity's default material from a throwaway primitive.
                    GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _base = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
                    Object.Destroy(tmp);
                }
                _base.name = "Daboiz_Base";
                return _base;
            }
        }

        private static int Key(Color c, int kind)
        {
            Color32 k = c;
            return (k.r << 24) ^ (k.g << 16) ^ (k.b << 8) ^ k.a ^ (kind * 7919);
        }

        /// <summary>Opaque lit colour.</summary>
        public static Material Get(Color c, float smoothness = 0.2f)
        {
            int key = Key(c, 1 + Mathf.RoundToInt(smoothness * 10f));
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(Base);
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            Cache[key] = m;
            return m;
        }

        /// <summary>Lit colour that also glows.</summary>
        public static Material Emissive(Color c, float intensity = 1.5f)
        {
            int key = Key(c, 50 + Mathf.RoundToInt(intensity * 10f));
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(Base);
            m.color = c;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * intensity);
            Cache[key] = m;
            return m;
        }

        /// <summary>Alpha-blended Standard material (bubbles, shields, ghosts).</summary>
        public static Material Transparent(Color c)
        {
            int key = Key(c, 99);
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(Base);
            m.color = c;
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.9f);
            m.renderQueue = (int)RenderQueue.Transparent;
            Cache[key] = m;
            return m;
        }

        /// <summary>Unlit material for LineRenderers (tracers, beams).</summary>
        public static Material Line(Color c)
        {
            int key = Key(c, 77);
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            Shader s = Shader.Find("Sprites/Default");
            m = s != null ? new Material(s) : new Material(Emissive(c, 2f));
            m.color = c;
            Cache[key] = m;
            return m;
        }
    }
}
