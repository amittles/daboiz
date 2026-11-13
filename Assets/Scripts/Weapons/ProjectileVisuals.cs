using Daboiz.Core;
using UnityEngine;

namespace Daboiz.Weapons
{
    /// <summary>Builds projectile visuals from primitives. Children are authored for a diameter of 1.</summary>
    public static class ProjectileVisuals
    {
        public static Transform Build(ProjectileKind kind, Color color, float radius, Transform parent)
        {
            Transform root = new GameObject("Visual").transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * (radius * 2f);
            switch (kind)
            {
                case ProjectileKind.Rocket:
                    V(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.6f, 1.2f, 0.6f), new Color(0.85f, 0.85f, 0.85f), Quaternion.Euler(90f, 0f, 0f));
                    V(PrimitiveType.Sphere, root, new Vector3(0f, 0f, 1.1f), new Vector3(0.6f, 0.6f, 0.9f), Color.red);
                    E(PrimitiveType.Sphere, root, new Vector3(0f, 0f, -1.3f), Vector3.one * 0.7f, new Color(1f, 0.6f, 0.1f));
                    for (int i = 0; i < 4; i++)
                        V(PrimitiveType.Cube, root, Quaternion.Euler(0f, 0f, i * 90f) * new Vector3(0.4f, 0f, -0.9f),
                            new Vector3(0.5f, 0.06f, 0.5f), Color.red, Quaternion.Euler(0f, 0f, i * 90f));
                    break;
                case ProjectileKind.Grenade:
                    V(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one, new Color(0.2f, 0.4f, 0.15f));
                    E(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(1.05f, 0.08f, 1.05f), color);
                    break;
                case ProjectileKind.Bubble:
                    GameObject b = V(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one, color);
                    b.GetComponent<Renderer>().sharedMaterial = Mats.Transparent(new Color(0.75f, 0.9f, 1f, 0.35f));
                    V(PrimitiveType.Sphere, root, new Vector3(-0.2f, 0.25f, -0.3f), Vector3.one * 0.15f, Color.white);
                    break;
                case ProjectileKind.RubberDuck:
                    V(PrimitiveType.Sphere, root, Vector3.zero, new Vector3(0.9f, 0.7f, 1.1f), new Color(1f, 0.85f, 0.1f));
                    V(PrimitiveType.Sphere, root, new Vector3(0f, 0.5f, 0.35f), Vector3.one * 0.55f, new Color(1f, 0.85f, 0.1f));
                    V(PrimitiveType.Cube, root, new Vector3(0f, 0.45f, 0.7f), new Vector3(0.3f, 0.1f, 0.3f), new Color(1f, 0.45f, 0f));
                    V(PrimitiveType.Sphere, root, new Vector3(0.15f, 0.6f, 0.55f), Vector3.one * 0.1f, Color.black);
                    V(PrimitiveType.Sphere, root, new Vector3(-0.15f, 0.6f, 0.55f), Vector3.one * 0.1f, Color.black);
                    V(PrimitiveType.Cube, root, new Vector3(0f, 0.25f, -0.5f), new Vector3(0.3f, 0.3f, 0.2f), new Color(1f, 0.85f, 0.1f), Quaternion.Euler(-30f, 0f, 0f));
                    break;
                case ProjectileKind.GooBlob:
                    GameObject g = V(PrimitiveType.Sphere, root, Vector3.zero, new Vector3(1f, 0.85f, 1.2f), color);
                    g.GetComponent<Renderer>().sharedMaterial = Mats.Get(color, 0.95f);
                    V(PrimitiveType.Sphere, root, new Vector3(0.2f, 0.2f, -0.5f), Vector3.one * 0.5f, color);
                    break;
                case ProjectileKind.Plasma:
                    E(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one, color);
                    break;
                case ProjectileKind.Needle:
                    E(PrimitiveType.Capsule, root, Vector3.zero, new Vector3(0.35f, 1f, 0.35f), color, Quaternion.Euler(90f, 0f, 0f));
                    break;
                case ProjectileKind.Banana:
                    V(PrimitiveType.Capsule, root, new Vector3(0f, 0f, 0.25f), new Vector3(0.4f, 0.6f, 0.4f), new Color(1f, 0.9f, 0.2f), Quaternion.Euler(60f, 0f, 0f));
                    V(PrimitiveType.Capsule, root, new Vector3(0f, 0f, -0.25f), new Vector3(0.4f, 0.6f, 0.4f), new Color(1f, 0.9f, 0.2f), Quaternion.Euler(120f, 0f, 0f));
                    break;
                case ProjectileKind.Crate:
                    V(PrimitiveType.Cube, root, Vector3.zero, Vector3.one, new Color(0.65f, 0.45f, 0.25f));
                    break;
                case ProjectileKind.Disc:
                    E(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(1.6f, 0.08f, 1.6f), color);
                    break;
                default:
                    V(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one, color);
                    break;
            }
            Prims.NoShadows(root.gameObject);
            return root;
        }

        private static GameObject V(PrimitiveType t, Transform p, Vector3 pos, Vector3 scale, Color c, Quaternion? rot = null)
        {
            return Prims.Visual(t, p, pos, scale, c, rot);
        }

        private static GameObject E(PrimitiveType t, Transform p, Vector3 pos, Vector3 scale, Color c, Quaternion? rot = null)
        {
            GameObject g = Prims.Visual(t, p, pos, scale, c, rot);
            g.GetComponent<Renderer>().sharedMaterial = Mats.Emissive(c, 2f);
            return g;
        }
    }
}
