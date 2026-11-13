using Daboiz.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Daboiz.Minigames
{
    /// <summary>Procedural level building blocks.</summary>
    public static class LevelKit
    {
        public static void Environment(Color sky, Color equator, Color ground, bool fog, Color fogColor, float fogDensity = 0.01f)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }

        public static GameObject Floor(Transform root, Vector3 topCenter, Vector2 size, Color color, float thickness = 1f)
        {
            return Prims.Box(root, "Floor", topCenter - Vector3.up * (thickness * 0.5f), new Vector3(size.x, thickness, size.y), color);
        }

        /// <summary>Checkerboard floor made of a few large tiles (cheap: one collider underneath).</summary>
        public static void CheckerFloor(Transform root, Vector3 topCenter, int tilesX, int tilesZ, float tile, Color a, Color b)
        {
            Prims.Box(root, "FloorCollider", topCenter - Vector3.up * 0.5f, new Vector3(tilesX * tile, 1f, tilesZ * tile), a);
            Transform vis = new GameObject("FloorTiles").transform;
            vis.SetParent(root, false);
            for (int x = 0; x < tilesX; x++)
                for (int z = 0; z < tilesZ; z++)
                {
                    if (((x + z) & 1) == 0) continue;
                    Vector3 c = topCenter + new Vector3((x - (tilesX - 1) * 0.5f) * tile, 0.005f, (z - (tilesZ - 1) * 0.5f) * tile);
                    Prims.Make(PrimitiveType.Cube, vis, "Tile", c, new Vector3(tile, 0.01f, tile), b, false);
                }
        }

        public static GameObject Wall(Transform root, Vector3 from, Vector3 to, float height, float thickness, Color color)
        {
            Vector3 d = to - from;
            d.y = 0f;
            Vector3 center = (from + to) * 0.5f + Vector3.up * (height * 0.5f);
            Quaternion rot = d.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(d) : Quaternion.identity;
            return Prims.Box(root, "Wall", center, new Vector3(thickness, height, d.magnitude + thickness), color, rot);
        }

        /// <summary>Straight ramp from 'bottom' (at ground level) rising 'height' over 'length' along +Z of yaw.</summary>
        public static GameObject Ramp(Transform root, Vector3 bottom, float yaw, float length, float width, float height, Color color)
        {
            float angle = Mathf.Atan2(height, length) * Mathf.Rad2Deg;
            float slopeLen = Mathf.Sqrt(length * length + height * height);
            Quaternion rot = Quaternion.Euler(-angle, yaw, 0f);
            Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            const float t = 0.5f;
            Vector3 center = bottom + fwd * (length * 0.5f) + Vector3.up * (height * 0.5f) - (rot * Vector3.up) * (t * 0.5f);
            return Prims.Box(root, "Ramp", center, new Vector3(width, t, slopeLen), color, rot);
        }

        /// <summary>
        /// CS-surf style roof ramp: two slabs at +-slopeAngle meeting at a ridge, running along 'dir'.
        /// ridgeStart is the top-centre at the start. Pitch makes the ridge descend for speed.
        /// </summary>
        public static Transform SurfRamp(Transform root, Vector3 ridgeStart, float yaw, float length, float faceWidth,
            float slopeAngle, float pitchDown, Color color)
        {
            GameObject g = new GameObject("SurfRamp");
            g.transform.SetParent(root, false);
            Quaternion frame = Quaternion.Euler(pitchDown, yaw, 0f);
            g.transform.SetPositionAndRotation(ridgeStart + frame * (Vector3.forward * (length * 0.5f)), frame);
            const float t = 0.6f;
            for (int side = -1; side <= 1; side += 2)
            {
                // side -1: left face (normal points -x), side +1: right face
                float roll = -side * slopeAngle;
                Quaternion lr = Quaternion.Euler(0f, 0f, roll);
                Vector3 localUp = lr * Vector3.up;
                Vector3 localRight = lr * Vector3.right;
                // Edge of the slab touching the ridge is at local x = -side * faceWidth/2 of the slab
                Vector3 center = (side < 0 ? -localRight : localRight) * (faceWidth * 0.5f) - localUp * (t * 0.5f);
                GameObject slab = Prims.Make(PrimitiveType.Cube, g.transform, side < 0 ? "LeftFace" : "RightFace", center,
                    new Vector3(faceWidth, t, length), color, true, lr, true);
                slab.transform.localScale = new Vector3(faceWidth, t, length);
            }
            // Ridge cap stripe for readability
            GameObject cap = Prims.Make(PrimitiveType.Cube, g.transform, "Ridge", new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.1f, length),
                Color.Lerp(color, Color.white, 0.5f), false, null, true);
            cap.transform.localScale = new Vector3(0.3f, 0.1f, length);
            return g.transform;
        }

        public static Light Sun(Transform root, Vector3 euler, float intensity, Color color)
        {
            GameObject g = new GameObject("LevelSun");
            g.transform.SetParent(root, false);
            g.transform.rotation = Quaternion.Euler(euler);
            Light l = g.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.Soft;
            return l;
        }

        public static Light Lamp(Transform root, Vector3 pos, Color color, float range, float intensity)
        {
            GameObject g = new GameObject("Lamp");
            g.transform.SetParent(root, false);
            g.transform.position = pos;
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }
    }
}
