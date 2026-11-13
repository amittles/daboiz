using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Daboiz.Weapons
{
    public enum FireMode { Hitscan, Projectile, Beam, Shotgun, Charge }

    public enum ProjectileKind { Ball, Rocket, Grenade, Bubble, RubberDuck, GooBlob, Plasma, Needle, Banana, Crate, Disc }

    /// <summary>Visual family used to build the viewmodel from primitives.</summary>
    public enum WeaponArchetype
    {
        Pistol, Revolver, SMG, AssaultRifle, Minigun, Shotgun, SuperShotgun, Sniper, Railgun,
        RocketLauncher, GrenadeLauncher, PlasmaRifle, Needler, FlakCannon, LinkBeam, ChargeCannon,
        Crossbow, BioLauncher, Cannon, DiscLauncher, Blower, Stapler
    }

    /// <summary>
    /// Data-driven weapon description. Plain serializable C# so it can be created in code,
    /// cloned, mutated by modifiers, and later mirrored by a ScriptableObject or network id.
    /// </summary>
    [System.Serializable]
    public class WeaponDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public WeaponArchetype Archetype;
        public FireMode Mode;
        public List<string> Tags = new List<string>();

        [Header("Firing")]
        public float Damage = 15f;
        public float FireRate = 5f;    // shots per second (beam: damage ticks per second)
        public bool Automatic;
        public int Pellets = 1;
        public float SpreadDegrees = 0.5f;
        public float Range = 200f;
        public int MagazineSize = 12;
        public float ReloadTime = 1.2f;
        /// <summary>-1 = infinite reserve.</summary>
        public int ReserveAmmo = -1;

        [Header("Knockback (m/s velocity change on actors)")]
        public float Knockback = 3f;
        public float KnockbackUp = 0.2f;   // fraction of knockback added upward
        public float PropForceScale = 2f;  // impulse multiplier for physics props
        public float SelfKnockback;        // recoil applied to the shooter

        [Header("Projectile")]
        public ProjectileKind Projectile = ProjectileKind.Ball;
        public float ProjectileSpeed = 40f;
        public float ProjectileGravity;    // multiplier of Physics.gravity
        public float ProjectileRadius = 0.12f;
        public float ProjectileLifetime = 5f;
        public bool PhysicalProjectile;    // real Rigidbody (bowling balls, crates)
        public float PhysicalMass = 6f;
        public int Bounces;
        public float Bounciness = 0.7f;
        public float HomingStrength;       // radians per second
        public bool ExplodeOnExpire;

        [Header("Hitscan")]
        public int Ricochets;
        public int Pierce;

        [Header("Explosion")]
        public bool Explosive;
        public float ExplosionRadius = 3.5f;
        public float ExplosionDamage = 60f;
        public float ExplosionForce = 14f;

        [Header("Charge")]
        public float ChargeTime = 1f;
        public float ChargeMinScale = 0.25f;
        public bool ChargeFiresProjectile;

        [Header("Feel")]
        public float RecoilKick = 1.5f;
        public float ViewmodelScale = 1f;
        public Color BodyColor = new Color(0.25f, 0.25f, 0.3f);
        public Color AccentColor = new Color(1f, 0.6f, 0.1f);
        public Color ProjectileColor = new Color(1f, 0.9f, 0.3f);
        public Core.Sfx FireSound = Core.Sfx.Shot;
        public float FirePitch = 1f;

        public List<WeaponModifier> Modifiers = new List<WeaponModifier>();

        public bool HasTag(string t) { return Tags.Contains(t); }

        public WeaponDefinition Clone()
        {
            WeaponDefinition c = (WeaponDefinition)MemberwiseClone();
            c.Tags = new List<string>(Tags);
            c.Modifiers = new List<WeaponModifier>(Modifiers);
            return c;
        }

        /// <summary>Add a modifier and let it rewrite stats.</summary>
        public WeaponDefinition With(WeaponModifier m)
        {
            Modifiers.Add(m);
            m.ModifyStats(this);
            return this;
        }

        public string ModifierSummary
        {
            get
            {
                if (Modifiers.Count == 0) return "Stock";
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < Modifiers.Count; i++)
                {
                    if (i > 0) sb.Append(" + ");
                    sb.Append(Modifiers[i].Name);
                }
                return sb.ToString();
            }
        }

        public string ModeLabel
        {
            get
            {
                switch (Mode)
                {
                    case FireMode.Hitscan: return Automatic ? "Hitscan (auto)" : "Hitscan";
                    case FireMode.Projectile: return Pellets > 1 ? "Projectile x" + Pellets : "Projectile";
                    case FireMode.Beam: return "Beam";
                    case FireMode.Shotgun: return "Shotgun x" + Pellets;
                    case FireMode.Charge: return "Charge (hold)";
                }
                return Mode.ToString();
            }
        }
    }
}
