using Daboiz.Combat;
using Daboiz.Core;
using Daboiz.Weapons;
using UnityEngine;
using UnityEngine.Rendering;

namespace Daboiz.Actors
{
    /// <summary>Primitive "bean" body with googly eyes, a held gun and status effect visuals.</summary>
    public class ActorVisual : MonoBehaviour
    {
        private Transform _root;
        private Transform _head;
        private Transform _gunPivot;
        private Transform _gunModel;
        private Transform _muzzle;
        private GameObject _shield;
        private Renderer _bodyRenderer;
        private Renderer _headRenderer;
        private Material _bodyMat;
        private Material _frozenMat;
        private bool _isLocal;
        private bool _dead;
        private WeaponDefinition _shownGun;

        public Transform Muzzle { get { return _muzzle != null ? _muzzle : transform; } }

        public void Build(Color color, bool isLocal)
        {
            _isLocal = isLocal;
            _root = new GameObject("Visual").transform;
            _root.SetParent(transform, false);

            _bodyMat = Mats.Get(color);
            _frozenMat = Mats.Get(new Color(0.6f, 0.9f, 1f), 0.9f);

            GameObject body = Prims.Visual(PrimitiveType.Capsule, _root, new Vector3(0f, 0.72f, 0f),
                new Vector3(0.8f, 0.72f, 0.8f), color);
            _bodyRenderer = body.GetComponent<Renderer>();

            _head = new GameObject("Head").transform;
            _head.SetParent(_root, false);
            _head.localPosition = new Vector3(0f, 1.52f, 0f);
            GameObject headBall = Prims.Visual(PrimitiveType.Sphere, _head, Vector3.zero, Vector3.one * 0.56f,
                Color.Lerp(color, Color.white, 0.35f));
            _headRenderer = headBall.GetComponent<Renderer>();
            // Googly eyes
            for (int i = -1; i <= 1; i += 2)
            {
                Prims.Visual(PrimitiveType.Sphere, _head, new Vector3(0.11f * i, 0.06f, 0.22f), Vector3.one * 0.17f,
                    Color.white);
                Prims.Visual(PrimitiveType.Sphere, _head, new Vector3(0.11f * i, 0.06f, 0.29f), Vector3.one * 0.08f,
                    Color.black);
            }
            // Silly hat
            Prims.Visual(PrimitiveType.Cylinder, _head, new Vector3(0f, 0.3f, 0f), new Vector3(0.36f, 0.08f, 0.36f),
                Color.Lerp(color, Color.black, 0.4f));

            _gunPivot = new GameObject("GunPivot").transform;
            _gunPivot.SetParent(_root, false);
            _gunPivot.localPosition = new Vector3(0.38f, 1.25f, 0.25f);

            _shield = Prims.Visual(PrimitiveType.Sphere, _root, new Vector3(0f, 0.9f, 0f), Vector3.one * 2.1f,
                Color.white);
            _shield.GetComponent<Renderer>().sharedMaterial = Mats.Transparent(new Color(0.3f, 0.8f, 1f, 0.25f));
            _shield.SetActive(false);

            if (isLocal)
            {
                foreach (Renderer r in _root.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        public void ShowGun(WeaponDefinition def)
        {
            if (_shownGun == def) return;
            _shownGun = def;
            if (_gunModel != null) Destroy(_gunModel.gameObject);
            _gunModel = null;
            _muzzle = null;
            if (def == null) return;
            _gunModel = WeaponModelBuilder.Build(def, _gunPivot, 0.55f, out _muzzle).transform;
            if (_isLocal)
            {
                foreach (Renderer r in _gunModel.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        public void SetDead(bool dead)
        {
            _dead = dead;
            if (_root == null) return;
            _root.localRotation = dead ? Quaternion.Euler(-85f, 0f, 0f) : Quaternion.identity;
            _root.localPosition = dead ? new Vector3(0f, 0.4f, 0f) : Vector3.zero;
            if (!dead) transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (_gunPivot != null) _gunPivot.gameObject.SetActive(!dead);
        }

        public void SetHidden(bool hidden)
        {
            if (_root != null) _root.gameObject.SetActive(!hidden);
        }

        public void Tick(Actor a, float dt)
        {
            if (_root == null) return;
            if (_head != null)
            {
                float hs = a.Status.HeadScale;
                float cur = _head.localScale.x;
                float next = Mathf.MoveTowards(cur, hs, dt * 4f);
                _head.localScale = new Vector3(next, next, next);
                if (!_dead) _head.localRotation = Quaternion.Euler(Mathf.Clamp(a.Pitch, -40f, 40f), 0f, 0f);
            }
            bool frozen = a.Status.Frozen;
            if (_bodyRenderer != null) _bodyRenderer.sharedMaterial = frozen ? _frozenMat : _bodyMat;
            if (_shield != null) _shield.SetActive(a.Status.Shielded && !_dead);

            if (_gunPivot != null)
            {
                bool showGun = !_dead && a.Weapons != null && a.Weapons.Current != null && a.Weapons.IsVisibleToOthers;
                if (showGun) ShowGun(a.Weapons.Current.Def);
                if (_gunModel != null) _gunModel.gameObject.SetActive(showGun);
                _gunPivot.localRotation = Quaternion.Euler(a.Pitch, 0f, 0f);
            }

            // Crouch squash
            if (!_dead)
            {
                float h = a.Motor.Crouching ? 0.6f : 1f;
                Vector3 ls = _root.localScale;
                float y = Mathf.MoveTowards(ls.y, h, dt * 5f);
                _root.localScale = new Vector3(1f, y, 1f);
            }
        }
    }
}
