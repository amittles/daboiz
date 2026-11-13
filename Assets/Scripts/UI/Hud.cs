using System.Collections.Generic;
using Daboiz.Actors;
using Daboiz.Combat;
using Daboiz.Core;
using UnityEngine;

namespace Daboiz.UI
{
    /// <summary>IMGUI drawing helpers + transient HUD state (kill feed, popups, hit markers, announcements).</summary>
    public class Hud : MonoBehaviour
    {
        private Texture2D _white;
        private readonly Dictionary<int, GUIStyle> _styles = new Dictionary<int, GUIStyle>();

        private class Popup { public Vector3 Pos; public string Text; public Color Color; public float Born; }
        private class FeedLine { public string Text; public float Born; }

        private readonly List<Popup> _popups = new List<Popup>();
        private readonly List<FeedLine> _feed = new List<FeedLine>();
        private float _hitMarkerTime = -10f;
        private string _announce;
        private Color _announceColor;
        private float _announceUntil;

        public float Scale { get { return Mathf.Max(0.75f, Screen.height / 1080f); } }

        private void Awake()
        {
            _white = new Texture2D(1, 1);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            HitResolver.ActorHit += OnActorHit;
        }

        private void OnDestroy() { HitResolver.ActorHit -= OnActorHit; }

        private void OnActorHit(Actor attacker, Actor victim, float dmg)
        {
            if (attacker == null || !attacker.IsLocalPlayer) return;
            _hitMarkerTime = Time.time;
            if (victim != null && dmg > 0f)
                PopupText(victim.EyePosition + Vector3.up * 0.4f, Mathf.RoundToInt(dmg).ToString(), new Color(1f, 0.4f, 0.3f));
            ProceduralAudio.PlayUI(Sfx.Click, 0.35f, 2.2f);
        }

        // ------------------------------------------------------------------ transient state

        public void Feed(string text)
        {
            _feed.Add(new FeedLine { Text = text, Born = Time.unscaledTime });
            if (_feed.Count > 6) _feed.RemoveAt(0);
        }

        public void PopupText(Vector3 pos, string text, Color color)
        {
            _popups.Add(new Popup { Pos = pos + Random.insideUnitSphere * 0.2f, Text = text, Color = color, Born = Time.time });
            if (_popups.Count > 40) _popups.RemoveAt(0);
        }

        public void Announce(string text, Color color, float duration)
        {
            _announce = text;
            _announceColor = color;
            _announceUntil = Time.unscaledTime + duration;
        }

        public void ClearTransient()
        {
            _popups.Clear();
            _feed.Clear();
            _announce = null;
        }

        // ------------------------------------------------------------------ primitives

        public GUIStyle Style(int size, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = true)
        {
            int px = Mathf.RoundToInt(size * Scale);
            int key = px * 100 + (int)anchor * 2 + (bold ? 1 : 0);
            GUIStyle s;
            if (_styles.TryGetValue(key, out s)) return s;
            s = new GUIStyle(GUI.skin.label)
            {
                fontSize = px,
                alignment = anchor,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = true,
                richText = true
            };
            s.normal.textColor = Color.white;
            _styles[key] = s;
            return s;
        }

        public void Rect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        public void Label(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool shadow = true)
        {
            GUIStyle s = Style(size, anchor);
            if (shadow)
            {
                Color sc = new Color(0f, 0f, 0f, color.a * 0.8f);
                s.normal.textColor = sc;
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, s);
            }
            s.normal.textColor = color;
            GUI.Label(r, text, s);
            s.normal.textColor = Color.white;
        }

        public void Bar(Rect r, float frac, Color fill, Color back)
        {
            Rect(r, back);
            Rect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), fill);
        }

        public bool WorldToGui(Vector3 world, out Vector2 gui)
        {
            gui = Vector2.zero;
            Camera cam = GameFlow.Instance != null ? GameFlow.Instance.Cam : Camera.main;
            if (cam == null) return false;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0.1f) return false;
            gui = new Vector2(sp.x, Screen.height - sp.y);
            return true;
        }

        public void WorldLabel(Vector3 world, string text, Color color, int size = 16, float maxDistance = 60f)
        {
            Camera cam = GameFlow.Instance != null ? GameFlow.Instance.Cam : Camera.main;
            if (cam == null) return;
            if ((cam.transform.position - world).sqrMagnitude > maxDistance * maxDistance) return;
            Vector2 g;
            if (!WorldToGui(world, out g)) return;
            Label(new Rect(g.x - 150, g.y - 15, 300, 30), text, size, color, TextAnchor.MiddleCenter);
        }

        public void Panel(Rect r) { Rect(r, new Color(0.05f, 0.05f, 0.08f, 0.82f)); }

        public bool Button(Rect r, string text, int size = 20)
        {
            GUIStyle s = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(size * Scale), fontStyle = FontStyle.Bold, wordWrap = true };
            return GUI.Button(r, text, s);
        }

        // ------------------------------------------------------------------ shared overlays

        public void DrawTransient()
        {
            // Damage popups
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                Popup p = _popups[i];
                float age = Time.time - p.Born;
                if (age > 0.9f) { _popups.RemoveAt(i); continue; }
                Color c = p.Color;
                c.a = 1f - age / 0.9f;
                WorldLabel(p.Pos + Vector3.up * age * 1.2f, p.Text, c, 18, 80f);
            }

            // Kill feed (top right)
            float y = 70f * Scale;
            for (int i = _feed.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - _feed[i].Born;
                if (age > 6f) { _feed.RemoveAt(i); continue; }
                Label(new Rect(Screen.width - 520 * Scale, y, 500 * Scale, 26 * Scale), _feed[i].Text, 15,
                    new Color(1f, 1f, 1f, Mathf.Clamp01(6f - age)), TextAnchor.UpperRight);
                y += 24f * Scale;
            }

            if (_announce != null && Time.unscaledTime < _announceUntil)
            {
                Label(new Rect(0, Screen.height * 0.24f, Screen.width, 80 * Scale), _announce, 40, _announceColor, TextAnchor.MiddleCenter);
            }
        }

        public void DrawCrosshair(Actor local)
        {
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            float gap = 5f * Scale, len = 7f * Scale, th = 2f * Scale;
            Color c = new Color(1f, 1f, 1f, 0.9f);
            if (local != null && local.Weapons != null && local.Weapons.Current != null && !local.Weapons.Holstered)
                gap += local.Weapons.Current.Def.SpreadDegrees * 2f * Scale;
            Rect(new Rect(cx - th * 0.5f, cy - gap - len, th, len), c);
            Rect(new Rect(cx - th * 0.5f, cy + gap, th, len), c);
            Rect(new Rect(cx - gap - len, cy - th * 0.5f, len, th), c);
            Rect(new Rect(cx + gap, cy - th * 0.5f, len, th), c);
            Rect(new Rect(cx - 1f, cy - 1f, 2f, 2f), c);

            float hm = Time.time - _hitMarkerTime;
            if (hm < 0.15f)
            {
                Color hc = new Color(1f, 0.3f, 0.2f, 1f - hm / 0.15f);
                float o = 10f * Scale;
                Rect(new Rect(cx - o - 4, cy - o - 4, 6, 6), hc);
                Rect(new Rect(cx + o - 2, cy - o - 4, 6, 6), hc);
                Rect(new Rect(cx - o - 4, cy + o - 2, 6, 6), hc);
                Rect(new Rect(cx + o - 2, cy + o - 2, 6, 6), hc);
            }

            if (local != null && local.Weapons != null && local.Weapons.Charging)
                Bar(new Rect(cx - 40 * Scale, cy + 24 * Scale, 80 * Scale, 6 * Scale), local.Weapons.ChargeAmount,
                    new Color(1f, 0.7f, 0.2f), new Color(0f, 0f, 0f, 0.5f));
        }

        public void DrawPlayerHud(Actor a)
        {
            if (a == null) return;
            float s = Scale;
            float W = Screen.width, H = Screen.height;

            // Speed (Source units/sec and m/s)
            float speed = a.Motor.HorizontalSpeed;
            int ups = Mathf.RoundToInt(speed / 0.01905f);
            Color sc = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.1f), Mathf.InverseLerp(8f, 25f, speed));
            Label(new Rect(0, H * 0.58f, W, 40 * s), ups + " u/s", 26, sc, TextAnchor.MiddleCenter);
            Label(new Rect(0, H * 0.58f + 30 * s, W, 24 * s), speed.ToString("0.0") + " m/s" + (a.Motor.Surfing ? "   SURF" : a.Motor.Sliding ? "   SLIDE" : ""),
                14, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);

            // Health
            if (GameFlow.Rules.DamageEnabled)
            {
                Panel(new Rect(20 * s, H - 70 * s, 260 * s, 50 * s));
                Bar(new Rect(30 * s, H - 45 * s, 240 * s, 14 * s), a.Health / a.MaxHealth, new Color(0.3f, 1f, 0.4f), new Color(0.3f, 0f, 0f, 0.8f));
                Label(new Rect(30 * s, H - 68 * s, 240 * s, 24 * s), "HP " + Mathf.CeilToInt(Mathf.Max(0f, a.Health)), 16, Color.white);
            }

            // Status effects
            float sy = H - 110 * s;
            foreach (KeyValuePair<StatusType, float> kv in a.Status.Describe())
            {
                Label(new Rect(24 * s, sy, 400 * s, 24 * s), kv.Key + "  " + kv.Value.ToString("0.0") + "s", 15, new Color(0.7f, 0.9f, 1f));
                sy -= 22 * s;
            }

            // Weapon
            if (a.Weapons != null && a.Weapons.Current != null && !a.Weapons.Holstered)
            {
                Weapons.WeaponInstance w = a.Weapons.Current;
                Panel(new Rect(W - 470 * s, H - 130 * s, 450 * s, 110 * s));
                Label(new Rect(W - 460 * s, H - 128 * s, 430 * s, 30 * s),
                    "[" + (a.Weapons.CurrentIndex + 1) + "/" + a.Weapons.Inventory.Count + "] " + w.Def.DisplayName, 20,
                    w.Def.AccentColor.grayscale < 0.2f ? Color.white : Color.Lerp(w.Def.AccentColor, Color.white, 0.4f));
                Label(new Rect(W - 460 * s, H - 100 * s, 430 * s, 40 * s), w.Def.Description, 13, new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperLeft, false);
                Label(new Rect(W - 460 * s, H - 66 * s, 300 * s, 22 * s), w.Def.ModeLabel + " | " + w.Def.ModifierSummary, 12, new Color(0.8f, 0.9f, 1f, 0.8f), TextAnchor.UpperLeft, false);
                string ammo = a.Weapons.Reloading ? "RELOADING" : w.Magazine + " / " + (w.Reserve < 0 ? "inf" : w.Reserve.ToString());
                Label(new Rect(W - 200 * s, H - 52 * s, 170 * s, 30 * s), ammo, 22, Color.white, TextAnchor.UpperRight);
            }
        }
    }
}
