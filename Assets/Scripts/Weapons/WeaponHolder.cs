using System.Collections.Generic;
using Daboiz.Actors;
using Daboiz.Combat;
using Daboiz.Core;
using UnityEngine;

namespace Daboiz.Weapons
{
    /// <summary>Runtime state of one carried weapon.</summary>
    public class WeaponInstance
    {
        public readonly WeaponDefinition Def;
        public int Magazine;
        public int Reserve; // -1 infinite

        public WeaponInstance(WeaponDefinition def)
        {
            Def = def;
            Refill();
        }

        public void Refill()
        {
            Magazine = Def.MagazineSize;
            Reserve = Def.ReserveAmmo;
        }

        public bool HasReserve { get { return Reserve != 0; } }
    }

    /// <summary>
    /// An actor's weapon inventory and firing logic (switching, reloads, fire modes).
    /// Driven entirely by ActorCommand so bots, local players and future network players share it.
    /// </summary>
    public class WeaponHolder : MonoBehaviour
    {
        public readonly List<WeaponInstance> Inventory = new List<WeaponInstance>();
        public int CurrentIndex { get; private set; }
        public WeaponInstance Current
        {
            get { return CurrentIndex >= 0 && CurrentIndex < Inventory.Count ? Inventory[CurrentIndex] : null; }
        }

        /// <summary>No weapon at all (workers in Office HR, Red Light Green Light).</summary>
        public bool Holstered;
        /// <summary>Weapon is hidden from others except briefly after firing (HR).</summary>
        public bool Concealed;
        public bool IsVisibleToOthers
        {
            get { return !Holstered && (!Concealed || Time.time - LastFireTime < 0.8f); }
        }

        /// <summary>Set by the local viewmodel so tracers/projectiles visually leave the viewmodel barrel.</summary>
        public Transform MuzzleOverride;
        public float LastFireTime { get; private set; }
        public float ChargeAmount { get; private set; }
        public bool Charging { get; private set; }
        public bool Reloading { get { return _reloadEnd > 0f; } }
        public float ReloadProgress
        {
            get
            {
                if (!Reloading || Current == null) return 0f;
                return 1f - Mathf.Clamp01((_reloadEnd - Time.time) / Current.Def.ReloadTime);
            }
        }
        public bool BeamActive { get { return _beam != null && _beam.enabled; } }

        public System.Action<WeaponInstance> Fired;
        public System.Action<WeaponInstance> Switched;

        private Actor _actor;
        private float _nextFireTime;
        private float _reloadEnd;
        private float _switchReadyTime;
        private float _chargeStart;
        private float _fireQueuedUntil;
        private float _nextBeamTick;
        private LineRenderer _beam;

        private void Awake() { _actor = GetComponent<Actor>(); }

        public void SetLoadout(IList<WeaponDefinition> defs, int startIndex = 0)
        {
            Inventory.Clear();
            for (int i = 0; i < defs.Count; i++)
                if (defs[i] != null) Inventory.Add(new WeaponInstance(defs[i]));
            CurrentIndex = -1;
            Select(Mathf.Clamp(startIndex, 0, Mathf.Max(0, Inventory.Count - 1)), true);
        }

        public void Give(WeaponDefinition def, bool select)
        {
            for (int i = 0; i < Inventory.Count; i++)
            {
                if (Inventory[i].Def == def)
                {
                    Inventory[i].Refill();
                    if (select) Select(i, true);
                    return;
                }
            }
            Inventory.Add(new WeaponInstance(def));
            if (select || Inventory.Count == 1) Select(Inventory.Count - 1, true);
        }

        public void RefillAll()
        {
            for (int i = 0; i < Inventory.Count; i++) Inventory[i].Refill();
            _reloadEnd = 0f;
        }

        public void Select(int index, bool instant = false)
        {
            if (Inventory.Count == 0) { CurrentIndex = -1; return; }
            index = Mathf.Clamp(index, 0, Inventory.Count - 1);
            if (index == CurrentIndex) return;
            CurrentIndex = index;
            _reloadEnd = 0f;
            Charging = false;
            ChargeAmount = 0f;
            StopBeam();
            _switchReadyTime = Time.time + (instant ? 0f : 0.18f);
            if (Switched != null) Switched(Current);
        }

        public void Tick(ActorCommand cmd, float dt)
        {
            if (Inventory.Count == 0 || Holstered)
            {
                StopBeam();
                Charging = false;
                return;
            }

            if (cmd.WeaponSlot >= 0 && cmd.WeaponSlot < Inventory.Count && cmd.WeaponSlot != CurrentIndex)
                Select(cmd.WeaponSlot);
            else if (cmd.WeaponScroll != 0 && Inventory.Count > 1)
                Select(((CurrentIndex + cmd.WeaponScroll) % Inventory.Count + Inventory.Count) % Inventory.Count);

            WeaponInstance w = Current;
            if (w == null) return;
            WeaponDefinition d = w.Def;

            if (_reloadEnd > 0f && Time.time >= _reloadEnd) FinishReload(w);
            if (cmd.Reload && !Reloading && w.Magazine < d.MagazineSize && w.HasReserve) StartReload(w);
            if (cmd.FirePressed) _fireQueuedUntil = Time.time + 0.18f;

            Minigames.MinigameBase mg = GameFlow.Instance != null ? GameFlow.Instance.Current : null;
            bool allowed = (mg == null || mg.CanFire(_actor)) && Time.time >= _switchReadyTime && !Reloading;
            if (!allowed)
            {
                StopBeam();
                if (!cmd.FireHeld) { Charging = false; ChargeAmount = 0f; }
                return;
            }

            if (w.Magazine <= 0)
            {
                StopBeam();
                Charging = false;
                if (w.HasReserve) StartReload(w);
                else if (cmd.FirePressed) ProceduralAudio.Play(Sfx.Click, transform.position, 0.6f, 0.8f);
                return;
            }

            switch (d.Mode)
            {
                case FireMode.Beam:
                    if (cmd.FireHeld) BeamTick(w); else StopBeam();
                    break;
                case FireMode.Charge:
                    if (cmd.FireHeld && Time.time >= _nextFireTime)
                    {
                        if (!Charging) { Charging = true; _chargeStart = Time.time; }
                        ChargeAmount = Mathf.Clamp01((Time.time - _chargeStart) / Mathf.Max(0.05f, d.ChargeTime));
                    }
                    else if (Charging && !cmd.FireHeld)
                    {
                        Charging = false;
                        Fire(w, Mathf.Lerp(d.ChargeMinScale, 1f, ChargeAmount));
                        ChargeAmount = 0f;
                    }
                    break;
                default:
                    bool trigger = d.Automatic ? cmd.FireHeld : Time.time <= _fireQueuedUntil;
                    if (trigger && Time.time >= _nextFireTime) Fire(w, 1f);
                    break;
            }
        }

        private void StartReload(WeaponInstance w)
        {
            if (Reloading) return;
            _reloadEnd = Time.time + w.Def.ReloadTime;
            StopBeam();
            ProceduralAudio.Play(Sfx.Click, transform.position, 0.5f, 0.7f);
        }

        private void FinishReload(WeaponInstance w)
        {
            _reloadEnd = 0f;
            int need = w.Def.MagazineSize - w.Magazine;
            if (w.Reserve < 0) w.Magazine = w.Def.MagazineSize;
            else
            {
                int take = Mathf.Min(need, w.Reserve);
                w.Magazine += take;
                w.Reserve -= take;
            }
            ProceduralAudio.Play(Sfx.Click, transform.position, 0.5f, 1.2f);
        }

        public Vector3 MuzzlePosition
        {
            get
            {
                if (MuzzleOverride != null) return MuzzleOverride.position;
                if (_actor != null && _actor.Visual != null) return _actor.Visual.Muzzle.position;
                return transform.position + Vector3.up * 1.3f;
            }
        }

        private static Vector3 Spread(Vector3 dir, float degrees)
        {
            if (degrees <= 0.001f) return dir;
            Vector2 r = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            Quaternion q = Quaternion.LookRotation(dir);
            return (q * new Vector3(r.x, r.y, 1f)).normalized;
        }

        private void Fire(WeaponInstance w, float scale)
        {
            WeaponDefinition d = w.Def;
            _nextFireTime = Time.time + 1f / Mathf.Max(0.05f, d.FireRate);
            _fireQueuedUntil = 0f;
            w.Magazine--;
            LastFireTime = Time.time;

            Vector3 origin = _actor.EyePosition;
            Vector3 aim = _actor.AimDirection;
            Vector3 muzzle = MuzzlePosition;
            int pellets = Mathf.Max(1, d.Pellets);
            bool projectile = d.Mode == FireMode.Projectile || (d.Mode == FireMode.Charge && d.ChargeFiresProjectile);

            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = Spread(aim, d.SpreadDegrees);
                if (projectile)
                {
                    if (d.PhysicalProjectile)
                        PhysicalProjectile.Spawn(d, _actor, origin + dir * 1.0f, dir * d.ProjectileSpeed * Mathf.Lerp(0.6f, 1f, scale), scale);
                    else
                        Projectile.Spawn(d, _actor, origin, dir * d.ProjectileSpeed, scale, muzzle - origin);
                }
                else
                {
                    FireHitscan(d, origin, muzzle, dir, scale);
                }
            }

            float vol = _actor.IsLocalPlayer ? 0.55f : 0.4f;
            ProceduralAudio.Play(d.FireSound, muzzle, vol, d.FirePitch * Random.Range(0.93f, 1.07f));
            Fx.Flash(muzzle, d.AccentColor, 2.5f, 5f, 0.05f);
            if (d.SelfKnockback > 0f) _actor.Motor.AddImpulse(-aim * d.SelfKnockback);
            if (Fired != null) Fired(w);
        }

        private void FireHitscan(WeaponDefinition d, Vector3 origin, Vector3 muzzle, Vector3 dir, float scale)
        {
            Vector3 start = origin;
            Vector3 visStart = muzzle;
            int ricochets = d.Ricochets;
            int pierce = d.Pierce;
            float remaining = d.Range;
            Transform ignore = _actor.transform;
            float width = d.Mode == FireMode.Charge ? Mathf.Lerp(0.03f, 0.12f, scale) : 0.025f;
            Color tracer = Color.Lerp(d.AccentColor, Color.white, 0.4f);

            for (int guard = 0; guard < 12 && remaining > 0.1f; guard++)
            {
                RaycastHit hit;
                if (!PhysicsUtil.RaycastIgnoring(start, dir, remaining, ignore, out hit))
                {
                    Fx.Tracer(visStart, start + dir * Mathf.Min(remaining, 150f), tracer, width, 0.07f);
                    return;
                }
                Fx.Tracer(visStart, hit.point, tracer, width, 0.07f);
                Fx.Burst(hit.point, d.AccentColor, 2, 2f, 0.05f);
                HitResolver.ResolveDirectHit(d, _actor, hit.collider, hit.point, hit.normal, dir, scale);
                remaining -= hit.distance;

                Actor hitActor = hit.collider.GetComponentInParent<Actor>();
                if (hitActor != null && pierce > 0)
                {
                    pierce--;
                    ignore = hitActor.transform;
                    start = hit.point + dir * 0.05f;
                    visStart = hit.point;
                    continue;
                }
                if (hitActor == null && ricochets > 0)
                {
                    ricochets--;
                    dir = Vector3.Reflect(dir, hit.normal);
                    start = hit.point + hit.normal * 0.02f;
                    visStart = hit.point;
                    ProceduralAudio.Play(Sfx.Click, hit.point, 0.3f, 2f);
                    continue;
                }
                return;
            }
        }

        private void BeamTick(WeaponInstance w)
        {
            WeaponDefinition d = w.Def;
            if (_beam == null)
            {
                GameObject g = new GameObject("Beam");
                g.transform.SetParent(transform, false);
                _beam = g.AddComponent<LineRenderer>();
                _beam.useWorldSpace = true;
                _beam.positionCount = 2;
                _beam.startWidth = 0.07f;
                _beam.endWidth = 0.12f;
                _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _beam.sharedMaterial = Mats.Line(d.AccentColor);
            _beam.startColor = d.AccentColor;
            _beam.endColor = Color.Lerp(d.AccentColor, Color.white, 0.5f);
            _beam.enabled = true;

            Vector3 origin = _actor.EyePosition;
            Vector3 dir = _actor.AimDirection;
            RaycastHit hit;
            bool got = PhysicsUtil.RaycastIgnoring(origin, dir, d.Range, _actor.transform, out hit);
            Vector3 end = got ? hit.point : origin + dir * d.Range;
            _beam.SetPosition(0, MuzzlePosition);
            _beam.SetPosition(1, end);

            if (Time.time >= _nextBeamTick)
            {
                _nextBeamTick = Time.time + 1f / Mathf.Max(1f, d.FireRate);
                w.Magazine--;
                LastFireTime = Time.time;
                if (got) HitResolver.ResolveDirectHit(d, _actor, hit.collider, hit.point, hit.normal, dir, 1f);
                ProceduralAudio.Play(d.FireSound, MuzzlePosition, 0.25f, d.FirePitch * Random.Range(0.9f, 1.1f));
                if (Fired != null) Fired(w);
            }
        }

        private void StopBeam()
        {
            if (_beam != null) _beam.enabled = false;
        }
    }
}
