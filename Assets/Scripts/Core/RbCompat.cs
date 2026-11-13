using UnityEngine;

namespace Daboiz.Core
{
    /// <summary>Rigidbody API differences between Unity 2022.3 and Unity 6.</summary>
    public static class RbCompat
    {
        public static Vector3 GetVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVelocity(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDrag(Rigidbody rb, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linear;
            rb.angularDamping = angular;
#else
            rb.drag = linear;
            rb.angularDrag = angular;
#endif
        }

        public static void SetBounciness(Collider c, float bounciness, float friction)
        {
#if UNITY_6000_0_OR_NEWER
            var m = new PhysicsMaterial("Daboiz_Bouncy");
            m.bounceCombine = PhysicsMaterialCombine.Maximum;
#else
            var m = new PhysicMaterial("Daboiz_Bouncy");
            m.bounceCombine = PhysicMaterialCombine.Maximum;
#endif
            m.bounciness = bounciness;
            m.dynamicFriction = friction;
            m.staticFriction = friction;
            c.sharedMaterial = m;
        }
    }
}
