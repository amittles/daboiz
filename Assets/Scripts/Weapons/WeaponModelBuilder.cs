using Daboiz.Core;
using UnityEngine;

namespace Daboiz.Weapons
{
    /// <summary>Builds a chunky toy-like gun from primitives. +Z is forward; returns the muzzle transform.</summary>
    public static class WeaponModelBuilder
    {
        public static GameObject Build(WeaponDefinition d, Transform parent, float scale, out Transform muzzle)
        {
            GameObject root = new GameObject("Gun_" + d.Id);
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * (scale * d.ViewmodelScale);
            Transform r = root.transform;
            Color body = d.BodyColor;
            Color acc = d.AccentColor;
            float barrelLen = 0.5f;
            float barrelRadius = 0.07f;
            int barrels = 1;
            float bodyLen = 0.35f;
            Vector3 bodySize = new Vector3(0.12f, 0.16f, bodyLen);

            switch (d.Archetype)
            {
                case WeaponArchetype.Pistol: bodyLen = 0.25f; barrelLen = 0.18f; break;
                case WeaponArchetype.Revolver: bodyLen = 0.22f; barrelLen = 0.32f; break;
                case WeaponArchetype.SMG: bodyLen = 0.4f; barrelLen = 0.2f; break;
                case WeaponArchetype.AssaultRifle: bodyLen = 0.55f; barrelLen = 0.35f; break;
                case WeaponArchetype.Minigun: bodyLen = 0.5f; barrelLen = 0.55f; barrels = 6; barrelRadius = 0.035f; break;
                case WeaponArchetype.Shotgun: bodyLen = 0.45f; barrelLen = 0.55f; barrelRadius = 0.06f; barrels = 1; break;
                case WeaponArchetype.SuperShotgun: bodyLen = 0.35f; barrelLen = 0.5f; barrels = 2; barrelRadius = 0.055f; break;
                case WeaponArchetype.Sniper: bodyLen = 0.6f; barrelLen = 0.75f; barrelRadius = 0.04f; break;
                case WeaponArchetype.Railgun: bodyLen = 0.6f; barrelLen = 0.6f; barrels = 2; barrelRadius = 0.03f; break;
                case WeaponArchetype.RocketLauncher: bodyLen = 0.3f; barrelLen = 0.9f; barrelRadius = 0.13f; break;
                case WeaponArchetype.GrenadeLauncher: bodyLen = 0.35f; barrelLen = 0.4f; barrelRadius = 0.11f; break;
                case WeaponArchetype.PlasmaRifle: bodyLen = 0.45f; barrelLen = 0.15f; barrelRadius = 0.09f; break;
                case WeaponArchetype.Needler: bodyLen = 0.35f; barrelLen = 0.1f; barrelRadius = 0.05f; break;
                case WeaponArchetype.FlakCannon: bodyLen = 0.45f; barrelLen = 0.45f; barrels = 3; barrelRadius = 0.06f; break;
                case WeaponArchetype.LinkBeam: bodyLen = 0.5f; barrelLen = 0.25f; barrelRadius = 0.05f; break;
                case WeaponArchetype.ChargeCannon: bodyLen = 0.45f; barrelLen = 0.35f; barrelRadius = 0.12f; break;
                case WeaponArchetype.Crossbow: bodyLen = 0.55f; barrelLen = 0.1f; break;
                case WeaponArchetype.BioLauncher: bodyLen = 0.4f; barrelLen = 0.3f; barrelRadius = 0.1f; break;
                case WeaponArchetype.Cannon: bodyLen = 0.4f; barrelLen = 0.6f; barrelRadius = 0.16f; break;
                case WeaponArchetype.DiscLauncher: bodyLen = 0.4f; barrelLen = 0.15f; barrelRadius = 0.14f; break;
                case WeaponArchetype.Blower: bodyLen = 0.4f; barrelLen = 0.6f; barrelRadius = 0.09f; break;
                case WeaponArchetype.Stapler: bodyLen = 0.3f; barrelLen = 0.05f; barrelRadius = 0.04f; break;
            }
            bodySize.z = bodyLen;

            V(PrimitiveType.Cube, r, new Vector3(0f, 0f, 0f), bodySize, body);
            // Grip
            V(PrimitiveType.Cube, r, new Vector3(0f, -0.13f, -bodyLen * 0.3f), new Vector3(0.09f, 0.18f, 0.08f),
                Color.Lerp(body, Color.black, 0.3f), Quaternion.Euler(15f, 0f, 0f));
            // Accent stripe
            V(PrimitiveType.Cube, r, new Vector3(0f, 0.085f, 0f), new Vector3(0.13f, 0.02f, bodyLen * 0.8f), acc);

            float barrelZ = bodyLen * 0.5f + barrelLen * 0.5f;
            for (int i = 0; i < barrels; i++)
            {
                Vector3 off = Vector3.zero;
                if (barrels == 2) off = new Vector3(i == 0 ? -0.04f : 0.04f, 0f, 0f);
                else if (barrels > 2)
                {
                    float a = i * Mathf.PI * 2f / barrels;
                    off = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.06f;
                }
                V(PrimitiveType.Cylinder, r, new Vector3(off.x, 0.02f + off.y, barrelZ),
                    new Vector3(barrelRadius * 2f, barrelLen * 0.5f, barrelRadius * 2f), Color.Lerp(body, Color.gray, 0.4f),
                    Quaternion.Euler(90f, 0f, 0f));
            }

            switch (d.Archetype)
            {
                case WeaponArchetype.Sniper:
                case WeaponArchetype.Railgun:
                    V(PrimitiveType.Cylinder, r, new Vector3(0f, 0.15f, 0.05f), new Vector3(0.07f, 0.12f, 0.07f), Color.black, Quaternion.Euler(90f, 0f, 0f));
                    break;
                case WeaponArchetype.Revolver:
                    V(PrimitiveType.Cylinder, r, new Vector3(0f, 0.02f, 0.05f), new Vector3(0.16f, 0.06f, 0.16f), acc, Quaternion.Euler(90f, 0f, 0f));
                    break;
                case WeaponArchetype.PlasmaRifle:
                case WeaponArchetype.ChargeCannon:
                case WeaponArchetype.LinkBeam:
                    E(PrimitiveType.Sphere, r, new Vector3(0f, 0.12f, -0.05f), Vector3.one * 0.12f, acc);
                    E(PrimitiveType.Sphere, r, new Vector3(0f, 0.02f, bodyLen * 0.5f), Vector3.one * 0.1f, acc);
                    break;
                case WeaponArchetype.Needler:
                    for (int i = 0; i < 5; i++)
                        E(PrimitiveType.Capsule, r, new Vector3(-0.04f + i * 0.02f, 0.13f, -0.05f + i * 0.03f), new Vector3(0.02f, 0.06f, 0.02f),
                            new Color(1f, 0.4f, 0.9f), Quaternion.Euler(-20f, 0f, 0f));
                    break;
                case WeaponArchetype.Crossbow:
                    V(PrimitiveType.Cube, r, new Vector3(0f, 0.02f, bodyLen * 0.4f), new Vector3(0.55f, 0.03f, 0.04f), acc);
                    break;
                case WeaponArchetype.BioLauncher:
                    E(PrimitiveType.Capsule, r, new Vector3(0f, 0.15f, 0f), new Vector3(0.1f, 0.12f, 0.1f), acc, Quaternion.Euler(90f, 0f, 0f));
                    break;
                case WeaponArchetype.Minigun:
                    V(PrimitiveType.Cube, r, new Vector3(0f, 0.15f, -0.05f), new Vector3(0.03f, 0.1f, 0.25f), Color.black);
                    break;
                case WeaponArchetype.Blower:
                    V(PrimitiveType.Cylinder, r, new Vector3(0f, 0.08f, -0.1f), new Vector3(0.22f, 0.12f, 0.22f), acc);
                    break;
                case WeaponArchetype.Stapler:
                    V(PrimitiveType.Cube, r, new Vector3(0f, 0.11f, 0.02f), new Vector3(0.11f, 0.05f, 0.32f), new Color(0.85f, 0.1f, 0.1f));
                    break;
                case WeaponArchetype.DiscLauncher:
                    E(PrimitiveType.Cylinder, r, new Vector3(0f, 0.12f, 0f), new Vector3(0.22f, 0.02f, 0.22f), acc);
                    break;
            }

            GameObject m = new GameObject("Muzzle");
            m.transform.SetParent(r, false);
            m.transform.localPosition = new Vector3(0f, 0.02f, bodyLen * 0.5f + barrelLen + 0.03f);
            muzzle = m.transform;
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
