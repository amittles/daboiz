using System.Collections.Generic;
using Daboiz.Core;
using Daboiz.Weapons.Modifiers;
using UnityEngine;

namespace Daboiz.Weapons
{
    /// <summary>
    /// Defines every gun at startup by combining a base archetype with composable modifiers.
    /// Add a gun = add one Add(...) line. Tags let minigames pull curated pools.
    /// </summary>
    public static class WeaponRegistry
    {
        private static List<WeaponDefinition> _all;
        private static Dictionary<string, WeaponDefinition> _byId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _all = null; _byId = null; }

        public static IReadOnlyList<WeaponDefinition> All { get { Ensure(); return _all; } }

        /// <summary>Guns shown in Sandbox (excludes mode-specific specials).</summary>
        public static List<WeaponDefinition> SandboxList
        {
            get
            {
                Ensure();
                List<WeaponDefinition> l = new List<WeaponDefinition>();
                foreach (WeaponDefinition d in _all) if (!d.HasTag("special")) l.Add(d);
                return l;
            }
        }

        public static WeaponDefinition Get(string id)
        {
            Ensure();
            WeaponDefinition d;
            if (_byId.TryGetValue(id, out d)) return d;
            Debug.LogWarning("[Daboiz] Unknown weapon id " + id);
            return _all[0];
        }

        public static List<WeaponDefinition> WithTag(string tag)
        {
            Ensure();
            List<WeaponDefinition> l = new List<WeaponDefinition>();
            foreach (WeaponDefinition d in _all) if (d.HasTag(tag)) l.Add(d);
            return l;
        }

        public static WeaponDefinition Random(System.Random rng, string tag = null)
        {
            List<WeaponDefinition> pool = tag == null ? SandboxList : WithTag(tag);
            if (pool.Count == 0) pool = SandboxList;
            return pool[rng.Next(pool.Count)];
        }

        // ------------------------------------------------------------------ archetypes

        public static WeaponDefinition Base(WeaponArchetype a)
        {
            WeaponDefinition d = new WeaponDefinition { Archetype = a };
            switch (a)
            {
                case WeaponArchetype.Pistol:
                    d.Mode = FireMode.Hitscan; d.Damage = 22; d.FireRate = 4.5f; d.MagazineSize = 12; d.Knockback = 3;
                    d.SpreadDegrees = 0.6f; d.BodyColor = new Color(0.3f, 0.3f, 0.35f); break;
                case WeaponArchetype.Revolver:
                    d.Mode = FireMode.Hitscan; d.Damage = 48; d.FireRate = 1.6f; d.MagazineSize = 6; d.Knockback = 7;
                    d.SpreadDegrees = 0.2f; d.ReloadTime = 1.6f; d.RecoilKick = 4f; d.FirePitch = 0.8f;
                    d.BodyColor = new Color(0.55f, 0.4f, 0.25f); break;
                case WeaponArchetype.SMG:
                    d.Mode = FireMode.Hitscan; d.Automatic = true; d.Damage = 9; d.FireRate = 13; d.MagazineSize = 35;
                    d.SpreadDegrees = 2.5f; d.Knockback = 1.2f; d.RecoilKick = 0.6f; d.FirePitch = 1.3f; break;
                case WeaponArchetype.AssaultRifle:
                    d.Mode = FireMode.Hitscan; d.Automatic = true; d.Damage = 15; d.FireRate = 9; d.MagazineSize = 30;
                    d.SpreadDegrees = 1.2f; d.Knockback = 2f; d.RecoilKick = 0.9f; d.BodyColor = new Color(0.25f, 0.35f, 0.2f); break;
                case WeaponArchetype.Minigun:
                    d.Mode = FireMode.Hitscan; d.Automatic = true; d.Damage = 7; d.FireRate = 22; d.MagazineSize = 150;
                    d.SpreadDegrees = 4f; d.Knockback = 1f; d.ReloadTime = 3f; d.RecoilKick = 0.4f; d.FirePitch = 1.4f; d.ViewmodelScale = 1.2f; break;
                case WeaponArchetype.Shotgun:
                    d.Mode = FireMode.Shotgun; d.Pellets = 9; d.Damage = 9; d.FireRate = 1.2f; d.MagazineSize = 6;
                    d.SpreadDegrees = 7f; d.Knockback = 1.6f; d.Range = 60; d.RecoilKick = 4f; d.FirePitch = 0.7f;
                    d.BodyColor = new Color(0.45f, 0.3f, 0.2f); break;
                case WeaponArchetype.SuperShotgun:
                    d.Mode = FireMode.Shotgun; d.Pellets = 16; d.Damage = 7; d.FireRate = 0.9f; d.MagazineSize = 2;
                    d.SpreadDegrees = 10f; d.Knockback = 1.5f; d.Range = 50; d.RecoilKick = 6f; d.SelfKnockback = 4f;
                    d.ReloadTime = 1.5f; d.FirePitch = 0.6f; break;
                case WeaponArchetype.Sniper:
                    d.Mode = FireMode.Hitscan; d.Damage = 90; d.FireRate = 0.8f; d.MagazineSize = 5; d.Knockback = 9;
                    d.SpreadDegrees = 0f; d.ReloadTime = 2f; d.RecoilKick = 5f; d.Range = 400; d.FirePitch = 0.5f; break;
                case WeaponArchetype.Railgun:
                    d.Mode = FireMode.Charge; d.Damage = 110; d.FireRate = 1.2f; d.MagazineSize = 4; d.Knockback = 16;
                    d.SpreadDegrees = 0f; d.ChargeTime = 1f; d.Pierce = 2; d.Range = 400; d.FireSound = Sfx.Laser;
                    d.AccentColor = new Color(0.3f, 0.8f, 1f); d.RecoilKick = 4f; break;
                case WeaponArchetype.RocketLauncher:
                    d.Mode = FireMode.Projectile; d.Projectile = ProjectileKind.Rocket; d.ProjectileSpeed = 28; d.Damage = 25;
                    d.Explosive = true; d.ExplosionRadius = 4f; d.ExplosionDamage = 70; d.ExplosionForce = 17; d.FireRate = 1.1f;
                    d.MagazineSize = 4; d.ReloadTime = 1.8f; d.Knockback = 6; d.ProjectileRadius = 0.15f; d.RecoilKick = 3f;
                    d.FireSound = Sfx.Whoosh; d.BodyColor = new Color(0.35f, 0.4f, 0.3f); break;
                case WeaponArchetype.GrenadeLauncher:
                    d.Mode = FireMode.Projectile; d.Projectile = ProjectileKind.Grenade; d.ProjectileSpeed = 22; d.ProjectileGravity = 1f;
                    d.Bounces = 3; d.Bounciness = 0.55f; d.ProjectileLifetime = 2.2f; d.ExplodeOnExpire = true; d.Explosive = true;
                    d.ExplosionRadius = 3.5f; d.ExplosionDamage = 65; d.ExplosionForce = 15; d.Damage = 20; d.FireRate = 1.5f;
                    d.MagazineSize = 6; d.ProjectileRadius = 0.13f; d.FireSound = Sfx.Pop; d.FirePitch = 0.6f; break;
                case WeaponArchetype.PlasmaRifle:
                    d.Mode = FireMode.Projectile; d.Projectile = ProjectileKind.Plasma; d.Automatic = true; d.ProjectileSpeed = 45;
                    d.Damage = 12; d.FireRate = 10; d.MagazineSize = 40; d.Knockback = 1.5f; d.SpreadDegrees = 1.5f;
                    d.ProjectileColor = new Color(0.3f, 1f, 0.6f); d.AccentColor = new Color(0.3f, 1f, 0.6f); d.FireSound = Sfx.Laser;
                    d.FirePitch = 1.4f; d.BodyColor = new Color(0.2f, 0.25f, 0.45f); break;
                case WeaponArchetype.Needler:
                    d.Mode = FireMode.Projectile; d.Projectile = ProjectileKind.Needle; d.Automatic = true; d.ProjectileSpeed = 32;
                    d.HomingStrength = 4f; d.Damage = 8; d.FireRate = 10; d.MagazineSize = 24; d.Knockback = 1f;
                    d.ProjectileColor = new Color(1f, 0.4f, 0.9f); d.AccentColor = new Color(1f, 0.4f, 0.9f); d.FireSound = Sfx.Zap;
                    d.BodyColor = new Color(0.35f, 0.15f, 0.4f); break;
                case WeaponArchetype.FlakCannon:
                    d.Mode = FireMode.Projectile; d.Pellets = 8; d.Projectile = ProjectileKind.Ball; d.ProjectileSpeed = 45;
                    d.ProjectileGravity = 0.3f; d.Bounces = 1; d.SpreadDegrees = 6f; d.Damage = 9; d.FireRate = 1.1f;
                    d.MagazineSize = 8; d.Knockback = 1.5f; d.ProjectileRadius = 0.06f; d.ProjectileColor = new Color(1f, 0.6f, 0.2f);
                    d.RecoilKick = 3f; d.FirePitch = 0.8f; break;
                case WeaponArchetype.LinkBeam:
                    d.Mode = FireMode.Beam; d.Damage = 5; d.FireRate = 15; d.MagazineSize = 120; d.Range = 25; d.Knockback = 0.7f;
                    d.AccentColor = new Color(0.4f, 1f, 0.3f); d.FireSound = Sfx.Zap; d.RecoilKick = 0.1f; d.ReloadTime = 2f; break;
                case WeaponArchetype.ChargeCannon:
                    d.Mode = FireMode.Charge; d.ChargeFiresProjectile = true; d.Projectile = ProjectileKind.Plasma; d.ProjectileSpeed = 35;
                    d.ProjectileRadius = 0.3f; d.Damage = 70; d.Explosive = true; d.ExplosionRadius = 3f; d.ExplosionDamage = 40;
                    d.ExplosionForce = 14; d.ChargeTime = 1.2f; d.FireRate = 1.5f; d.MagazineSize = 5; d.Knockback = 8;
                    d.ProjectileColor = new Color(1f, 0.3f, 0.2f); d.AccentColor = new Color(1f, 0.3f, 0.2f); d.FireSound = Sfx.Laser;
                    d.FirePitch = 0.6f; break;
                case WeaponArchetype.Crossbow:
                    d.Mode = FireMode.Projectile; d.Projectile = ProjectileKind.Needle; d.ProjectileSpeed = 60; d.ProjectileGravity = 0.35f;
                    d.Damage = 60; d.FireRate = 1.2f; d.MagazineSize = 1; d.ReloadTime = 0.8f; d.Knockback = 6;
                    d.ProjectileColor = new Color(0.8f, 0.7f, 0.5f); d.FireSound = Sfx.Whoosh; d.FirePitch = 1.5f; break;
                case WeaponArchetype.BioLauncher:
                    d.Mode = FireMode.Projectile; d.Projectile = ProjectileKind.GooBlob; d.ProjectileSpeed = 20; d.ProjectileGravity = 1f;
                    d.Damage = 20; d.FireRate = 3f; d.Automatic = true; d.MagazineSize = 20; d.Knockback = 2f; d.ProjectileRadius = 0.18f;
                    d.ProjectileColor = new Color(0.45f, 0.95f, 0.25f); d.FireSound = Sfx.Splat; break;
                case WeaponArchetype.Cannon:
                    d.Mode = FireMode.Projectile; d.PhysicalProjectile = true; d.Projectile = ProjectileKind.Ball; d.ProjectileSpeed = 24;
                    d.ProjectileRadius = 0.25f; d.PhysicalMass = 7f; d.ProjectileLifetime = 8f; d.Damage = 40; d.Knockback = 12;
                    d.FireRate = 0.8f; d.MagazineSize = 3; d.ReloadTime = 2f; d.ProjectileColor = new Color(0.15f, 0.15f, 0.2f);
                    d.RecoilKick = 6f; d.SelfKnockback = 3f; d.FireSound = Sfx.Boom; d.FirePitch = 1.6f; break;
                case WeaponArchetype.DiscLauncher:
                    d.Mode = FireMode.Projectile; d.Projectile = ProjectileKind.Disc; d.ProjectileSpeed = 30; d.Damage = 35;
                    d.Bounces = 1; d.Bounciness = 0.95f; d.FireRate = 1.5f; d.MagazineSize = 6; d.Knockback = 5; d.ProjectileRadius = 0.18f;
                    d.ProjectileColor = new Color(0.2f, 0.9f, 1f); d.AccentColor = new Color(0.2f, 0.9f, 1f); d.FireSound = Sfx.Whoosh; break;
                case WeaponArchetype.Blower:
                    d.Mode = FireMode.Beam; d.Damage = 0; d.FireRate = 20; d.MagazineSize = 200; d.Range = 14; d.Knockback = 2.2f;
                    d.KnockbackUp = 0.35f; d.PropForceScale = 3f; d.AccentColor = new Color(0.9f, 0.9f, 0.9f); d.FireSound = Sfx.Whoosh; break;
                case WeaponArchetype.Stapler:
                    d.Mode = FireMode.Hitscan; d.Damage = 200; d.FireRate = 2f; d.MagazineSize = 99; d.Range = 35; d.Knockback = 4;
                    d.SpreadDegrees = 0f; d.FireSound = Sfx.Click; d.FirePitch = 0.6f; d.BodyColor = new Color(0.15f, 0.15f, 0.15f); break;
            }
            return d;
        }

        private static WeaponDefinition Add(string id, string name, string desc, WeaponArchetype a, string tags,
            params WeaponModifier[] mods)
        {
            WeaponDefinition d = Base(a);
            d.Id = id;
            d.DisplayName = name;
            d.Description = desc;
            for (int i = 0; i < mods.Length; i++) d.With(mods[i]);
            if (!string.IsNullOrEmpty(tags))
                foreach (string t in tags.Split(','))
                    if (t.Trim().Length > 0) d.Tags.Add(t.Trim());
            _all.Add(d);
            _byId[id] = d;
            return d;
        }

        private static void Ensure()
        {
            if (_all != null) return;
            _all = new List<WeaponDefinition>();
            _byId = new Dictionary<string, WeaponDefinition>();

            // Shared stateless modifier instances
            var bubble = new BubbleProjectiles();
            var duck = new RubberDuckProjectiles();
            var goo = new GooSplatter();
            var glue = new GooSplatter(new Color(0.95f, 0.95f, 0.9f), "Glue");
            var bouncy = new Bouncy();
            var boom = new Explosive();
            var rico = new Ricochet();
            var homing = new Homing();
            var bighead = new BigHead();
            var kbOnly = new KnockbackOnly();
            var tiny = new Tiny();
            var huge = new Huge();
            var lowgrav = new LowGravity();
            var shrink = new ShrinkTarget();
            var launch = new LaunchTarget();
            var confetti = new ConfettiOnHit();
            var slow = new SlowTarget();
            var reverse = new ReverseControls();
            var freeze = new FreezeTarget();
            var rapid = new Rapid();
            var zoom = new SpeedOnHit();

            const WeaponArchetype P = WeaponArchetype.Pistol, RV = WeaponArchetype.Revolver, SMG = WeaponArchetype.SMG,
                AR = WeaponArchetype.AssaultRifle, MG = WeaponArchetype.Minigun, SG = WeaponArchetype.Shotgun,
                SSG = WeaponArchetype.SuperShotgun, SN = WeaponArchetype.Sniper, RG = WeaponArchetype.Railgun,
                RL = WeaponArchetype.RocketLauncher, GL = WeaponArchetype.GrenadeLauncher, PL = WeaponArchetype.PlasmaRifle,
                ND = WeaponArchetype.Needler, FK = WeaponArchetype.FlakCannon, LB = WeaponArchetype.LinkBeam,
                CC = WeaponArchetype.ChargeCannon, XB = WeaponArchetype.Crossbow, BIO = WeaponArchetype.BioLauncher,
                CN = WeaponArchetype.Cannon, DL = WeaponArchetype.DiscLauncher, BL = WeaponArchetype.Blower;

            // ---- Stock classics
            Add("pewpew", "Pew Pew Classic", "Your trusty sidearm. Boring. Reliable. Pew.", P, "dm");
            Add("sixer", "Sixer", "A STRAFTAT-grade hand cannon. Six chances to look cool.", RV, "dm");
            Add("smg", "SMG (Slightly Mad Gun)", "Sprays like a garden hose with anger issues.", SMG, "dm");
            Add("assault_pepper", "Assault & Pepper", "Seasoned automatic rifle. Mildly spicy.", AR, "dm");
            Add("brrrt", "BRRRT-9000", "Minigun. The sound is the description.", MG, "dm");
            Add("boomstick", "Boomstick", "Pump shotgun. Shoves people into next week.", SG, "dm,bowling");
            Add("double_trouble", "Double Trouble", "Super shotgun. Two barrels, zero chill, rocket-jumps you backwards.", SSG, "dm,bowling");
            Add("noscope", "No-Scope Nancy", "Sniper rifle with no scope. Good luck.", SN, "dm");
            Add("zap_rail", "Zap Rail", "Hold to charge, release to pierce up to three friends.", RG, "dm");
            Add("rocket_rodeo", "Rocket Rodeo", "Rocket launcher. Shoot your feet to fly. UT-approved.", RL, "dm,bowling");
            Add("lobber", "Lobber", "Bouncing grenades with a 2.2s fuse. Lob responsibly.", GL, "dm");
            Add("plasma_pal", "Plasma Pal", "Halo-ish plasma rifle. Green, glowy, friendly.", PL, "dm");
            Add("needle_haystack", "Needle in a Haystack", "Homing pink needles. They will find you.", ND, "dm");
            Add("flak_attack", "Flak Attack", "UT flak cannon. Hot bouncy chunks.", FK, "dm");
            Add("link_buddy", "Link Buddy", "Continuous beam. Hold the trigger and hug.", LB, "dm");
            Add("spicy_pickle", "Spicy Pickle", "Charge cannon. Hold for a bigger, angrier ball.", CC, "dm,bowling");
            Add("truth_bow", "Crossbow of Truth", "Heavy bolt with a satisfying arc.", XB, "dm");
            Add("sludge", "Sludge Thrower", "UT bio-rifle homage. Leaves slowing goo everywhere.", BIO, "dm", goo);
            Add("disc_jockey", "Disc Jockey", "Bouncing razor frisbees (they're foam, relax).", DL, "dm");
            Add("bowling_bazooka", "Bowling Ball Bazooka", "Fires a REAL physics bowling ball. Strike!", CN, "dm,bowling");

            // ---- Bubbles
            Add("bubble_blaster", "Bubble Blaster 9000", "Machine-gun bubbles. Pop! You're floating now.", SMG, "dm,impair,surf", bubble);
            Add("soap_opera", "Soap Opera", "Shotgun of bubbles. Dramatic and clean.", SG, "dm,impair", bubble);
            Add("bath_bomb", "Bubble Bath Bomb", "Bubble grenades that explode. Relaxing!", GL, "dm", bubble, boom);
            Add("thought_bubble", "Thought Bubble", "A single giant bubble. Contemplative.", SN, "dm,impair", bubble, huge);
            Add("clingy_bubbles", "Clingy Bubbles", "Homing bubbles that just want a hug.", ND, "dm,impair,surf", bubble, homing);

            // ---- Rubber ducks
            Add("quackhammer", "Quackhammer", "Rocket launcher that fires bouncing ducks that explode.", RL, "dm,bowling", duck);
            Add("ducky_revolver", "Rubber Ducky Revolver", "Six squeaky ducks. Each bounces three times.", RV, "dm,bowling", duck);
            Add("duck_hunt", "Duck Hunt", "Homing rubber ducks. Turnabout is fair play.", ND, "dm", duck, homing);
            Add("flak_of_ducks", "Flock of Flak", "Eight ducks per shot. Squeak symphony.", FK, "dm,bowling", duck);
            Add("ducknado", "Ducknado", "Minigun. Ducks. Why.", MG, "dm", duck);
            Add("double_duck", "Double Barrel Duck", "Super shotgun full of ducks.", SSG, "dm,bowling", duck);

            // ---- Goo / glue
            Add("goozooka", "Goo-zooka", "Rocket that splats sticky slowing goo.", RL, "dm", goo);
            Add("glue_gun", "Hot Glue Gun", "Craft-store chaos. Gluey shots slow people down.", SMG, "dm,impair,surf", glue, slow);
            Add("splat_shotty", "Splat Shotty", "Shotgun that paints the floor with goo.", SG, "dm", goo);
            Add("goo_rain", "Goo Rain", "Flak of goo. Forecast: sticky.", FK, "dm", goo);

            // ---- Bounce / ricochet / homing
            Add("rico_rita", "Ricochet Rita", "Pistol whose shots bank off three walls.", P, "dm", rico);
            Add("pinball", "Pinball Wizard", "Ultra-bouncy grenades. TILT!", GL, "dm,bowling", bouncy);
            Add("bouncy_castle", "Bouncy Castle", "Rockets that bounce before exploding.", RL, "dm", bouncy);
            Add("rico_rail", "Ricochet Railgun", "Charge, then bank shots around corners.", RG, "dm", rico);
            Add("homie", "Homing Homie", "Heat-seeking rockets. Your homie, not theirs.", RL, "dm", homing);
            Add("disco_disc", "Disco Disc", "Bouncy confetti frisbees. Party in the face.", DL, "dm", confetti, bouncy);

            // ---- Size / gravity / launch
            Add("peashooter", "Tiny Tim's Peashooter", "Itty-bitty bullets, very fast.", SMG, "dm", tiny);
            Add("absolute_unit", "Absolute Unit", "A revolver the size of a fridge.", RV, "dm,bowling", huge);
            Add("huge_pea", "Huge Pea Shooter", "Big slow peas. Big slow regret.", P, "dm", huge);
            Add("frisbee_fury", "Frisbee of Fury", "Gigantic disc. Mows lawns and people.", DL, "dm,bowling", huge);
            Add("moon_mortar", "Moon Shoes Mortar", "Floaty grenades that give victims low gravity.", GL, "dm", lowgrav);
            Add("space_pistol", "Space Pistol", "Victims get moon gravity for a while.", P, "dm,impair", lowgrav);
            Add("shrinkies", "Honey I Shrunk the Lads", "Railgun that shrinks targets to half size.", RG, "dm,impair", shrink);
            Add("shrink_shotty", "Shrink Shotty", "Shotgun shrink ray. Mass miniaturization.", SG, "dm,impair", shrink);
            Add("yeet", "Yeet Cannon", "No damage. Maximum yeet.", CC, "dm,impair,bowling", launch, kbOnly);
            Add("gravity_grenade", "Up-Grenade", "Grenades that launch everyone skyward.", GL, "dm,impair", launch);
            Add("bounce_house", "Bounce House Blaster", "Plasma that launches without hurting.", PL, "dm,impair,surf", launch, kbOnly);

            // ---- Silly status
            Add("bighead_sniper", "Big Head Mode", "Sniper that inflates heads. Easier to hit next time.", SN, "dm", bighead);
            Add("bighead_buss", "Bighead Blunderbuss", "Super shotgun of head inflation.", SSG, "dm", bighead);
            Add("persuader", "The Persuader", "No damage. Shotgun that shoves VERY hard.", SG, "dm,impair,bowling", kbOnly);
            Add("party_popper", "Party Popper", "Shotgun that rains confetti on every hit.", SG, "dm", confetti);
            Add("birthday_brrrt", "Birthday Minigun", "Happy birthday! Brrrt.", MG, "dm", confetti);
            Add("confetti_cannon", "Confetti Cannon", "Charge cannon of celebratory shoving.", CC, "dm,impair", confetti, kbOnly);
            Add("slowpoke", "Slowpoke Ray", "Beam that slows anything it touches.", LB, "dm,impair,surf", slow, kbOnly);
            Add("mirror", "Mirror Mirror", "Plasma that reverses the target's controls.", PL, "dm,impair,surf", reverse);
            Add("brain_freeze", "Brain Freeze", "Plasma that freezes targets for a moment.", PL, "dm,impair,surf", freeze);
            Add("popsicle", "Popsicle Sniper", "One shot, one popsicle.", SN, "dm,impair", freeze);
            Add("mild_inconvenience", "Minigun of Mild Inconvenience", "Tiny bullets, no damage, lots of nudging.", MG, "dm,impair", tiny, kbOnly);
            Add("leaf_blower", "Leaf Blower 3000", "Blows props and people away. Zero damage.", BL, "dm,impair,surf");
            Add("rapid_rita", "Rapid Rita", "Full-auto ricochet pistol.", P, "dm", rapid, rico);
            Add("zoomies", "Zoomies", "SMG that gives YOU a speed boost on hit.", SMG, "dm,surf", zoom);
            Add("crate_expectations", "Crate Expectations", "Launches physics crates. Great expectations.", CN, "dm,bowling");

            // ---- Mode specials
            WeaponDefinition stapler = Add("termination_stapler", "Termination Stapler", "HR's silent problem-solver. One staple = one firing.",
                WeaponArchetype.Stapler, "special");
            stapler.RecoilKick = 1f;
            WeaponDefinition shove = Add("shove_shotgun", "Shove Shotgun", "Knockback-only shotgun. Limited ammo; grab pickups.",
                SG, "special,surf", kbOnly);
            shove.ReserveAmmo = 6;
            shove.MagazineSize = 3;

            // Physical crate projectile tweak
            WeaponDefinition crate = _byId["crate_expectations"];
            crate.Projectile = ProjectileKind.Crate;
            crate.ProjectileRadius = 0.35f;
            crate.PhysicalMass = 10f;
            crate.ProjectileColor = new Color(0.65f, 0.45f, 0.25f);
            WeaponDefinition ball = _byId["bowling_bazooka"];
            ball.ProjectileColor = new Color(0.15f, 0.2f, 0.6f);
            ball.ProjectileRadius = 0.22f;
        }
    }
}
