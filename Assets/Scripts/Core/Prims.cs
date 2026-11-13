using UnityEngine;
using UnityEngine.Rendering;

namespace Daboiz.Core
{
    /// <summary>Helpers that build level geometry and visuals from Unity primitives.</summary>
    public static class Prims
    {
        public static GameObject Make(PrimitiveType type, Transform parent, string name, Vector3 position,
            Vector3 scale, Color color, bool collider = true, Quaternion? rotation = null, bool local = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            if (local)
            {
                go.transform.localPosition = position;
                go.transform.localRotation = rotation ?? Quaternion.identity;
            }
            else
            {
                go.transform.position = position;
                go.transform.rotation = rotation ?? Quaternion.identity;
            }
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mats.Get(color);
            if (!collider) StripCollider(go);
            return go;
        }

        /// <summary>Solid world box (has a BoxCollider).</summary>
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Color color,
            Quaternion? rotation = null)
        {
            return Make(PrimitiveType.Cube, parent, name, center, size, color, true, rotation);
        }

        /// <summary>Collider-free visual attached in local space.</summary>
        public static GameObject Visual(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale,
            Color color, Quaternion? localRot = null)
        {
            return Make(type, parent, type.ToString(), localPos, localScale, color, false, localRot, true);
        }

        public static void StripCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            // DestroyImmediate so the collider can't be hit by queries later this frame
            // or merge into a parent Rigidbody's compound collider.
            if (c != null) Object.DestroyImmediate(c);
        }

        public static void SetMaterial(GameObject go, Material m)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = m;
        }

        public static void NoShadows(GameObject root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        /// <summary>Invisible collider-only box (kill walls, blockers).</summary>
        public static GameObject InvisibleBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            return go;
        }

        public static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta;
        }
    }
}
