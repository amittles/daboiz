using Daboiz.Actors;
using Daboiz.Core;
using Daboiz.Weapons;
using UnityEngine;

namespace Daboiz.Player
{
    /// <summary>
    /// First-person presentation for the local actor: camera placement, FOV kick at speed,
    /// strafe tilt, recoil, optional head bob, and the primitive viewmodel. Purely cosmetic.
    /// </summary>
    public class PlayerCameraRig : MonoBehaviour
    {
        public float StrafeTilt = 2.5f;
        public float FovKick = 18f;
        public float FovKickStartSpeed = 9f;
        public float FovKickFullSpeed = 22f;

        private Actor _actor;
        private Camera _cam;
        private GameSettings _settings;
        private float _roll;
        private float _fov;
        private float _recoil;      // degrees, decays
        private float _recoilVel;
        private float _bobPhase;
        private float _landDip;

        private Transform _vmRoot;
        private Transform _vmGun;
        private Transform _vmMuzzle;
        private WeaponDefinition _vmDef;
        private float _vmKick;
        private Vector2 _vmSway;

        public Camera Cam { get { return _cam; } }

        public void Init(Actor actor, Camera cam, GameSettings settings)
        {
            _actor = actor;
            _cam = cam;
            _settings = settings;
            _fov = settings.BaseFov;
            _vmRoot = new GameObject("ViewmodelRoot").transform;
            _vmRoot.SetParent(actor.transform, false);
            actor.Weapons.Fired += OnFired;
            actor.Motor.OnLanded += OnLanded;
            actor.Motor.OnJumped += OnJumped;
            if (_cam != null)
            {
                _cam.nearClipPlane = 0.03f;
                _cam.farClipPlane = 1000f;
            }
        }

        private void OnDestroy()
        {
            if (_actor != null)
            {
                if (_actor.Weapons != null) _actor.Weapons.Fired -= OnFired;
                if (_actor.Motor != null) { _actor.Motor.OnLanded -= OnLanded; _actor.Motor.OnJumped -= OnJumped; }
            }
        }

        private void OnFired(WeaponInstance w)
        {
            _recoilVel += w.Def.RecoilKick * 12f;
            _vmKick = Mathf.Min(_vmKick + 0.06f + w.Def.RecoilKick * 0.01f, 0.25f);
        }

        private void OnLanded(float speed)
        {
            _landDip = Mathf.Clamp(speed * 0.008f, 0f, 0.15f);
            if (speed > 6f) ProceduralAudio.Play(Sfx.Land, transform.position, 0.3f, 1f, 0f);
        }

        private void OnJumped() { ProceduralAudio.Play(Sfx.Jump, transform.position, 0.15f, Random.Range(0.9f, 1.1f), 0f); }

        private void LateUpdate()
        {
            if (_cam == null || _actor == null) return;
            if (GameFlow.Instance != null && GameFlow.Instance.SpectatorActive)
            {
                if (_vmRoot != null) _vmRoot.gameObject.SetActive(false);
                return;
            }
            float dt = Time.deltaTime;

            // Recoil spring (critically damped back to zero)
            _recoil += _recoilVel * dt;
            _recoilVel += (-_recoil * 120f - _recoilVel * 18f) * dt;

            float strafe = _actor.LastCommand.Move.x;
            _roll = Mathf.Lerp(_roll, -strafe * StrafeTilt, 1f - Mathf.Exp(-10f * dt));

            float speed = _actor.Motor.HorizontalSpeed;
            float kick = Mathf.InverseLerp(FovKickStartSpeed, FovKickFullSpeed, speed) * FovKick;
            float targetFov = _settings.BaseFov + kick;
            _fov = Mathf.Lerp(_fov, targetFov, 1f - Mathf.Exp(-6f * dt));
            float aspect = _cam.aspect > 0.1f ? _cam.aspect : 16f / 9f;
            _cam.fieldOfView = Camera.HorizontalToVerticalFieldOfView(Mathf.Clamp(_fov, 60f, 140f), aspect);

            Vector3 bob = Vector3.zero;
            if (_settings.HeadBob && _actor.Motor.Grounded && speed > 1f)
            {
                _bobPhase += dt * speed * 1.4f;
                bob = new Vector3(Mathf.Cos(_bobPhase) * 0.03f, Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.05f, 0f);
            }
            _landDip = Mathf.MoveTowards(_landDip, 0f, dt * 0.6f);

            Vector3 eye = _actor.IsAlive ? _actor.EyePosition : _actor.transform.position + Vector3.up * 0.6f;
            Quaternion rot = Quaternion.Euler(_actor.Pitch - _recoil, _actor.Yaw, _roll);
            eye += rot * bob + Vector3.down * _landDip;
            _cam.transform.SetPositionAndRotation(eye, rot);

            UpdateViewmodel(dt, eye, rot);
        }

        private void UpdateViewmodel(float dt, Vector3 eye, Quaternion rot)
        {
            WeaponHolder h = _actor.Weapons;
            bool show = _actor.IsAlive && h != null && !h.Holstered && h.Current != null;
            _vmRoot.gameObject.SetActive(show);
            if (!show) { h.MuzzleOverride = null; return; }

            if (_vmDef != h.Current.Def || _vmGun == null)
            {
                if (_vmGun != null) Destroy(_vmGun.gameObject);
                _vmDef = h.Current.Def;
                _vmGun = WeaponModelBuilder.Build(_vmDef, _vmRoot, 1f, out _vmMuzzle).transform;
                Prims.NoShadows(_vmGun.gameObject);
                _vmKick = 0.3f; // draw animation
            }
            h.MuzzleOverride = _vmMuzzle;

            Vector2 md = GameFlow.Instance != null && GameFlow.Instance.GameplayInputActive ? GameInput.MouseDelta : Vector2.zero;
            _vmSway = Vector2.Lerp(_vmSway, -md * 0.004f, 1f - Mathf.Exp(-8f * dt));
            _vmKick = Mathf.MoveTowards(_vmKick, 0f, dt * 1.2f);
            float reloadDip = h.Reloading ? Mathf.Sin(h.ReloadProgress * Mathf.PI) * 0.18f : 0f;
            float charge = h.Charging ? h.ChargeAmount : 0f;
            Vector3 shake = charge > 0f ? Random.insideUnitSphere * 0.004f * charge : Vector3.zero;

            _vmRoot.SetPositionAndRotation(eye, rot);
            _vmGun.localPosition = new Vector3(0.22f + _vmSway.x, -0.2f + _vmSway.y - reloadDip, 0.42f - _vmKick) + shake;
            _vmGun.localRotation = Quaternion.Euler(-_vmKick * 60f + reloadDip * 120f, -2f, 0f);
        }
    }
}
