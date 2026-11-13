using System.Collections.Generic;
using Daboiz.Actors;
using Daboiz.Minigames;
using Daboiz.UI;
using UnityEngine;

namespace Daboiz.Core
{
    /// <summary>
    /// The single persistent game object: main menu, settings, lobby (local player + bots),
    /// minigame loading/teardown, pause, results, party mode tally and spectating.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }
        private static readonly GameRules MenuRules = new GameRules();

        public static GameRules Rules
        {
            get { return Instance != null && Instance.Current != null ? Instance.Current.Rules : MenuRules; }
        }

        public class ModeEntry
        {
            public string Title;
            public string Tagline;
            public System.Func<MinigameBase> Create;
            public ModeEntry(string t, string g, System.Func<MinigameBase> c) { Title = t; Tagline = g; Create = c; }
        }

        public static readonly ModeEntry[] Modes =
        {
            new ModeEntry("Gun Bowling", "Knock down pins with a gun instead of a ball.", () => new GunBowling()),
            new ModeEntry("Office HR", "Short-form social deduction. Find HR before you're 'let go'.", () => new OfficeHR()),
            new ModeEntry("Red Light Green Light", "Movement obstacle course. Freeze when the doll looks.", () => new RedLightGreenLight()),
            new ModeEntry("Surf Race PVP", "CS-surf ramps, checkpoints, pickups and shove guns.", () => new SurfRace()),
            new ModeEntry("Falling Chessboard", "Tiles fall, guns shove. Last one standing.", () => new FallingChessboard()),
        };

        public GameSettings Settings = new GameSettings();
        public Camera Cam { get; private set; }
        public Hud Hud { get; private set; }
        public Transform LevelRoot { get; private set; }
        public MinigameBase Current { get; private set; }
        public MatchContext Ctx { get; private set; }
        public bool Paused { get; private set; }
        public bool SpectatorActive { get; private set; }

        public bool GameplayInputActive
        {
            get
            {
                return Current != null && !Paused && Current.State != MinigameState.Finished && !Current.WantsCursor
                       && !SpectatorActive && Cursor.lockState == CursorLockMode.Locked;
            }
        }

        private enum ScreenId { Menu, Settings, InGame, PartySummary }
        private ScreenId _screen = ScreenId.Menu;
        private readonly List<Participant> _participants = new List<Participant>();
        private bool _partyActive;
        private int _partyIndex;
        private System.Func<MinigameBase> _lastFactory;
        private Transform _menuBackdrop;
        private int _spectateIndex;
        private bool _showScores;
        private System.Random _rng = new System.Random();

        private static readonly string[] BotNames =
        {
            "Big Steve", "Karen.exe", "Duckman", "Slop Lord", "Tiny Tim", "Gregory", "xX_Bean_Xx", "Sir Yeets",
            "Mayo Mike", "Chad Strafe", "Wobbles", "HR Dave"
        };

        private static readonly Color[] PlayerColors =
        {
            new Color(0.2f, 0.6f, 1f), new Color(1f, 0.35f, 0.3f), new Color(0.35f, 0.9f, 0.35f),
            new Color(1f, 0.8f, 0.2f), new Color(0.75f, 0.4f, 1f), new Color(1f, 0.55f, 0.15f)
        };

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Settings.Load();
            ProceduralAudio.MasterVolume = Settings.Volume;
            Hud = gameObject.AddComponent<Hud>();
            SetupCamera();
            SetupSun();
            BuildMenuBackdrop();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void SetupCamera()
        {
            // Disable whatever cameras/listeners the open scene has; we bring our own.
            foreach (Camera c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
            foreach (AudioListener l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) l.enabled = false;
            GameObject go = new GameObject("DaboizCamera");
            go.transform.SetParent(transform, false);
            Cam = go.AddComponent<Camera>();
            Cam.tag = "MainCamera";
            Cam.clearFlags = CameraClearFlags.Skybox;
            Cam.backgroundColor = new Color(0.45f, 0.65f, 0.9f);
            Cam.nearClipPlane = 0.03f;
            go.AddComponent<AudioListener>();
        }

        private void SetupSun()
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.enabled) return;
            GameObject g = new GameObject("DaboizSun");
            g.transform.SetParent(transform, false);
            g.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light sun = g.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.96f, 0.88f);
        }

        private void BuildParticipants()
        {
            _participants.Clear();
            _participants.Add(new Participant(0, "You", PlayerColors[0], true, 1f));
            List<string> names = new List<string>(BotNames);
            for (int i = 0; i < Mathf.Clamp(Settings.BotCount, 0, 5); i++)
            {
                int n = _rng.Next(names.Count);
                string nm = names[n];
                names.RemoveAt(n);
                float skill = 0.3f + (float)_rng.NextDouble() * 0.5f;
                _participants.Add(new Participant(i + 1, nm, PlayerColors[(i + 1) % PlayerColors.Length], false, skill));
            }
        }

        private void BuildMenuBackdrop()
        {
            if (_menuBackdrop != null) return;
            _menuBackdrop = new GameObject("MenuBackdrop").transform;
            Minigames.LevelKit.Environment(new Color(0.6f, 0.7f, 0.9f), new Color(0.5f, 0.5f, 0.55f), new Color(0.25f, 0.22f, 0.2f), false, Color.gray);
            Minigames.LevelKit.CheckerFloor(_menuBackdrop, Vector3.zero, 12, 12, 3f, new Color(0.9f, 0.9f, 0.95f), new Color(0.25f, 0.6f, 1f));
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * 2f / 9f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 6f, 1.2f + (i % 3) * 0.6f, Mathf.Sin(a) * 6f + 4f);
                Transform t = Weapons.ProjectileVisuals.Build(i % 3 == 0 ? Weapons.ProjectileKind.RubberDuck :
                    i % 3 == 1 ? Weapons.ProjectileKind.Bubble : Weapons.ProjectileKind.Banana, new Color(0.7f, 0.9f, 1f, 0.4f), 0.6f, _menuBackdrop);
                t.position = p;
                FxPiece spin = t.gameObject.AddComponent<FxPiece>();
                spin.Gravity = 0f; spin.Life = 1e9f; spin.ShrinkOut = false; spin.Drag = 0f; spin.Spin = new Vector3(20f, 60f + i * 10f, 0f);
            }
            Cam.transform.SetPositionAndRotation(new Vector3(0f, 3f, -8f), Quaternion.Euler(10f, 0f, 0f));
            Cam.fieldOfView = 60f;
        }

        // ------------------------------------------------------------------ mode control

        public void StartSingle(System.Func<MinigameBase> factory)
        {
            _partyActive = false;
            BuildParticipants();
            Load(factory);
        }

        public void StartParty()
        {
            _partyActive = true;
            _partyIndex = 0;
            BuildParticipants();
            foreach (Participant p in _participants) p.PartyPoints = 0;
            Load(Modes[0].Create);
        }

        private void Load(System.Func<MinigameBase> factory)
        {
            Teardown();
            if (_menuBackdrop != null) { Destroy(_menuBackdrop.gameObject); _menuBackdrop = null; }
            _lastFactory = factory;
            LevelRoot = new GameObject("Level").transform;
            Ctx = new MatchContext
            {
                Root = LevelRoot,
                Participants = _participants,
                Rng = new System.Random(_rng.Next()),
                Settings = Settings
            };
            Current = factory();
            _screen = ScreenId.InGame;
            Hud.ClearTransient();
            Current.Begin(Ctx);
        }

        private void Teardown()
        {
            if (Current != null) Current.Teardown();
            Current = null;
            Ctx = null;
            if (LevelRoot != null)
            {
                LevelRoot.gameObject.SetActive(false); // fires OnDisable on everything right now
                Destroy(LevelRoot.gameObject);
                LevelRoot = null;
            }
            Actor.All.Clear();
            Paused = false;
            SpectatorActive = false;
            Time.timeScale = 1f;
        }

        public void ReturnToMenu()
        {
            Teardown();
            _partyActive = false;
            _screen = ScreenId.Menu;
            BuildMenuBackdrop();
        }

        private void ContinueAfterResults()
        {
            if (_partyActive && Current != null && Current.Results != null)
            {
                AwardPartyPoints(Current.Results);
                _partyIndex++;
                if (_partyIndex < Modes.Length) { Load(Modes[_partyIndex].Create); return; }
                Teardown();
                _screen = ScreenId.PartySummary;
                BuildMenuBackdrop();
                return;
            }
            ReturnToMenu();
        }

        private static readonly int[] PlacePoints = { 10, 7, 5, 3, 2, 1 };

        private void AwardPartyPoints(List<RankEntry> results)
        {
            foreach (RankEntry r in results)
            {
                int idx = Mathf.Clamp(r.Place - 1, 0, PlacePoints.Length - 1);
                r.Participant.PartyPoints += PlacePoints[idx];
            }
        }

        // ------------------------------------------------------------------ frame

        private void Update()
        {
            if (Current != null)
            {
                if (GameInput.GetKeyDown(KeyCode.Escape) && Current.State != MinigameState.Finished) SetPaused(!Paused);
                _showScores = GameInput.GetKey(KeyCode.Tab);
                if (!Paused) Current.Tick(Time.deltaTime);
                UpdateSpectator();
            }
            UpdateCursor();
        }

        public void SetPaused(bool p)
        {
            Paused = p;
            Time.timeScale = p ? 0f : 1f;
        }

        private void UpdateCursor()
        {
            bool wantLock = Current != null && !Paused && Current.State != MinigameState.Finished && !Current.WantsCursor;
            if (wantLock && Cursor.lockState != CursorLockMode.Locked && GameInput.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (wantLock && Application.isFocused && Cursor.lockState != CursorLockMode.Locked && Time.frameCount % 30 == 0)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (!wantLock && Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void UpdateSpectator()
        {
            Actor local = Ctx != null ? Ctx.LocalActor : null;
            bool spectate = local != null && !local.IsAlive && Time.time - local.DeathTime > 1.5f
                            && Current.State != MinigameState.Finished;
            SpectatorActive = spectate;
        }

        private void LateUpdate()
        {
            if (!SpectatorActive || Ctx == null) return;
            if (GameInput.GetMouseButtonDown(0) || GameInput.GetKeyDown(KeyCode.Space)) _spectateIndex++;
            List<Actor> alive = new List<Actor>();
            foreach (Actor a in Ctx.Actors) if (a.IsAlive && !a.IsLocalPlayer) alive.Add(a);
            Vector3 pos;
            Vector3 look;
            if (alive.Count > 0)
            {
                Actor t = alive[Mathf.Abs(_spectateIndex) % alive.Count];
                Vector3 back = Quaternion.Euler(0f, t.Yaw, 0f) * Vector3.back;
                look = t.EyePosition;
                pos = look + back * 4.5f + Vector3.up * 1.6f;
            }
            else
            {
                pos = Current.SpectatorPoint;
                look = Current.SpectatorLookAt;
            }
            Vector3 cp = Vector3.Lerp(Cam.transform.position, pos, 1f - Mathf.Exp(-6f * Time.deltaTime));
            Cam.transform.SetPositionAndRotation(cp, Quaternion.LookRotation((look - cp).normalized + Vector3.down * 0.01f));
            Cam.fieldOfView = 70f;
        }

        // ------------------------------------------------------------------ GUI

        private void OnGUI()
        {
            switch (_screen)
            {
                case ScreenId.Menu: DrawMenu(); break;
                case ScreenId.Settings: DrawSettings(false); break;
                case ScreenId.PartySummary: DrawPartySummary(); break;
                case ScreenId.InGame: DrawInGame(); break;
            }
        }

        private void DrawMenu()
        {
            float s = Hud.Scale;
            float w = 640 * s, h = Mathf.Min(Screen.height - 40, 820 * s);
            Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Hud.Panel(r);
            Hud.Label(new Rect(r.x, r.y + 14 * s, w, 60 * s), "DA BOIZ", 52, new Color(1f, 0.8f, 0.2f), TextAnchor.MiddleCenter);
            Hud.Label(new Rect(r.x, r.y + 70 * s, w, 26 * s), "friendslop party demo  -  " + (1 + Settings.BotCount) + " player lobby",
                16, Color.white, TextAnchor.MiddleCenter);

            float y = r.y + 110 * s;
            float bw = w - 60 * s, bh = 58 * s, x = r.x + 30 * s;
            if (Hud.Button(new Rect(x, y, bw, bh), "PARTY MODE  (all 5 in a row, points tallied)", 22)) StartParty();
            y += bh + 14 * s;
            foreach (ModeEntry m in Modes)
            {
                if (Hud.Button(new Rect(x, y, bw, bh * 0.85f), m.Title + "\n<size=" + Mathf.RoundToInt(13 * s) + ">" + m.Tagline + "</size>", 19))
                    StartSingle(m.Create);
                y += bh * 0.85f + 8 * s;
            }
            y += 6 * s;
            if (Hud.Button(new Rect(x, y, bw, bh * 0.85f), "Sandbox / Range\n<size=" + Mathf.RoundToInt(13 * s) + ">All " +
                                                             Weapons.WeaponRegistry.SandboxList.Count + " guns, physics props, dummies, movement course</size>", 19))
                StartSingle(() => new SandboxRange());
            y += bh * 0.85f + 14 * s;
            if (Hud.Button(new Rect(x, y, bw * 0.49f, bh * 0.7f), "Settings", 18)) _screen = ScreenId.Settings;
            if (Hud.Button(new Rect(x + bw * 0.51f, y, bw * 0.49f, bh * 0.7f), "Quit", 18))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        private void DrawSettings(bool inGame)
        {
            float s = Hud.Scale;
            float w = 560 * s, h = 470 * s;
            Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Hud.Panel(r);
            Hud.Label(new Rect(r.x, r.y + 10 * s, w, 40 * s), inGame ? "PAUSED" : "SETTINGS", 30, Color.white, TextAnchor.MiddleCenter);
            float y = r.y + 60 * s, x = r.x + 30 * s, lw = 200 * s, cw = w - 60 * s - lw;
            Hud.Label(new Rect(x, y, lw, 26 * s), "Bots: " + Settings.BotCount, 16, Color.white);
            if (!inGame) Settings.BotCount = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + lw, y + 8 * s, cw, 20 * s), Settings.BotCount, 0, 5));
            else Hud.Label(new Rect(x + lw, y, cw, 26 * s), "(change from main menu)", 13, Color.gray);
            y += 40 * s;
            Hud.Label(new Rect(x, y, lw, 26 * s), "Mouse sens: " + Settings.MouseSensitivity.ToString("0.00"), 16, Color.white);
            Settings.MouseSensitivity = GUI.HorizontalSlider(new Rect(x + lw, y + 8 * s, cw, 20 * s), Settings.MouseSensitivity, 0.2f, 8f);
            y += 40 * s;
            Hud.Label(new Rect(x, y, lw, 26 * s), "FOV: " + Mathf.RoundToInt(Settings.BaseFov), 16, Color.white);
            Settings.BaseFov = GUI.HorizontalSlider(new Rect(x + lw, y + 8 * s, cw, 20 * s), Settings.BaseFov, 70f, 120f);
            y += 40 * s;
            Hud.Label(new Rect(x, y, lw, 26 * s), "Volume: " + Mathf.RoundToInt(Settings.Volume * 100f) + "%", 16, Color.white);
            Settings.Volume = GUI.HorizontalSlider(new Rect(x + lw, y + 8 * s, cw, 20 * s), Settings.Volume, 0f, 1f);
            ProceduralAudio.MasterVolume = Settings.Volume;
            y += 40 * s;
            GUIStyle tg = new GUIStyle(GUI.skin.toggle) { fontSize = Mathf.RoundToInt(16 * s) };
            bool bhop = GUI.Toggle(new Rect(x, y, w - 60 * s, 28 * s), Settings.AutoBhop, " Auto-bhop (hold jump to hop)", tg);
            if (bhop != Settings.AutoBhop)
            {
                Settings.AutoBhop = bhop;
                if (Ctx != null && Ctx.LocalActor != null) Ctx.LocalActor.Motor.Settings.AutoBhop = bhop;
            }
            y += 32 * s;
            Settings.HeadBob = GUI.Toggle(new Rect(x, y, w - 60 * s, 28 * s), Settings.HeadBob, " Head bob", tg);
            y += 32 * s;
            Settings.InvertY = GUI.Toggle(new Rect(x, y, w - 60 * s, 28 * s), Settings.InvertY, " Invert mouse Y", tg);
            y += 44 * s;

            float bw = (w - 80 * s) / 3f;
            if (inGame)
            {
                if (Hud.Button(new Rect(x, y, bw, 44 * s), "Resume")) { Settings.Save(); SetPaused(false); }
                if (Hud.Button(new Rect(x + bw + 10 * s, y, bw, 44 * s), "Restart")) { Settings.Save(); RestartCurrent(); }
                if (Hud.Button(new Rect(x + 2 * (bw + 10 * s), y, bw, 44 * s), "Main Menu")) { Settings.Save(); ReturnToMenu(); }
            }
            else
            {
                if (Hud.Button(new Rect(x, y, w - 60 * s, 44 * s), "Back")) { Settings.Save(); _screen = ScreenId.Menu; }
            }
        }

        private void RestartCurrent()
        {
            if (_partyActive && _partyIndex < Modes.Length) Load(Modes[_partyIndex].Create);
            else if (_lastFactory != null) Load(_lastFactory);
        }

        private void DrawInGame()
        {
            if (Current == null) return;
            float s = Hud.Scale;
            Actor local = Ctx.LocalActor;

            // Nametags
            if (Current.Rules.ShowNametags)
            {
                foreach (Actor a in Ctx.Actors)
                {
                    if (a == local || !a.IsAlive) continue;
                    Vector3 head = a.transform.position + Vector3.up * (2.2f * a.transform.localScale.y);
                    if (local != null && local.IsAlive && !Daboiz.AI.BotBrain.CanSee(local, a)) continue;
                    Hud.WorldLabel(head, a.DisplayName, a.Color, 14, 45f);
                }
            }

            if (local != null && local.IsAlive && !SpectatorActive)
            {
                Hud.DrawCrosshair(local);
                Hud.DrawPlayerHud(local);
            }
            else if (SpectatorActive)
            {
                Hud.Label(new Rect(0, Screen.height - 80 * s, Screen.width, 30 * s), "SPECTATING  (click to cycle)", 18,
                    new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
            }

            // Top bar: title + timer
            string title = Current.Title + (_partyActive ? "   (Party " + (_partyIndex + 1) + "/" + Modes.Length + ")" : "");
            Hud.Label(new Rect(20 * s, 12 * s, 700 * s, 30 * s), title, 20, new Color(1f, 0.85f, 0.3f));
            if (Current.ShowTimer && Current.State == MinigameState.Playing)
            {
                int t = Mathf.CeilToInt(Current.TimeLeft);
                Hud.Label(new Rect(0, 10 * s, Screen.width, 40 * s), (t / 60) + ":" + (t % 60).ToString("00"), 30,
                    t <= 10 ? new Color(1f, 0.3f, 0.3f) : Color.white, TextAnchor.UpperCenter);
            }

            Current.DrawHUD(Hud);
            Hud.DrawTransient();

            if (Current.State == MinigameState.Intro) DrawIntro();
            if (_showScores && Current.State != MinigameState.Finished) DrawScoreboard();
            if (Current.State == MinigameState.Finished) DrawResults();
            if (Paused) DrawSettings(true);

            if (!Paused && Current.State == MinigameState.Playing && !Current.WantsCursor && Cursor.lockState != CursorLockMode.Locked)
                Hud.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 40 * s), "Click to capture mouse", 22, Color.yellow, TextAnchor.MiddleCenter);
        }

        private void DrawIntro()
        {
            float s = Hud.Scale;
            float w = 760 * s, h = 300 * s;
            Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.18f, w, h);
            Hud.Panel(r);
            Hud.Label(new Rect(r.x, r.y + 14 * s, w, 50 * s), Current.Title, 40, new Color(1f, 0.8f, 0.2f), TextAnchor.MiddleCenter);
            Hud.Label(new Rect(r.x + 30 * s, r.y + 70 * s, w - 60 * s, 30 * s), Current.Tagline, 18, Color.white, TextAnchor.MiddleCenter);
            Hud.Label(new Rect(r.x + 30 * s, r.y + 110 * s, w - 60 * s, 140 * s), Current.HowToPlay, 15, new Color(0.85f, 0.9f, 1f), TextAnchor.UpperCenter, false);
            Hud.Label(new Rect(r.x, r.y + h - 50 * s, w, 40 * s), "Starting in " + Mathf.CeilToInt(Current.IntroLeft), 26, Color.white, TextAnchor.MiddleCenter);
        }

        private void DrawScoreboard()
        {
            float s = Hud.Scale;
            float w = 460 * s, h = 320 * s;
            Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Hud.Panel(r);
            Hud.Label(new Rect(r.x, r.y + 8 * s, w, 34 * s), "SCORES", 24, Color.white, TextAnchor.MiddleCenter);
            float y = r.y + 50 * s;
            foreach (string line in Current.ScoreLines())
            {
                Hud.Label(new Rect(r.x + 30 * s, y, w - 60 * s, 26 * s), line, 17, Color.white);
                y += 28 * s;
            }
            if (_partyActive)
            {
                y += 10 * s;
                Hud.Label(new Rect(r.x + 30 * s, y, w - 60 * s, 26 * s), "Party points:", 15, new Color(1f, 0.85f, 0.3f));
                y += 24 * s;
                string pts = "";
                foreach (Participant p in _participants) pts += p.Name + " " + p.PartyPoints + "   ";
                Hud.Label(new Rect(r.x + 30 * s, y, w - 60 * s, 50 * s), pts, 13, Color.white);
            }
        }

        private void DrawResults()
        {
            float s = Hud.Scale;
            float w = 620 * s, h = 470 * s;
            Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Hud.Panel(r);
            Hud.Label(new Rect(r.x, r.y + 12 * s, w, 50 * s), Current.ResultHeadline ?? "Round over", 32, new Color(1f, 0.8f, 0.2f), TextAnchor.MiddleCenter);
            float y = r.y + 76 * s;
            if (Current.Results != null)
            {
                foreach (RankEntry e in Current.Results)
                {
                    Color c = e.Place == 1 ? new Color(1f, 0.85f, 0.3f) : Color.white;
                    Hud.Rect(new Rect(r.x + 30 * s, y + 4 * s, 16 * s, 16 * s), e.Participant.Color);
                    string pts = _partyActive ? "   +" + PlacePoints[Mathf.Clamp(e.Place - 1, 0, PlacePoints.Length - 1)] + " party pts" : "";
                    Hud.Label(new Rect(r.x + 56 * s, y, w - 90 * s, 28 * s),
                        Ordinal(e.Place) + "   " + e.Participant.Name + "   <size=" + Mathf.RoundToInt(14 * s) + ">" + e.Detail + pts + "</size>", 19, c);
                    y += 34 * s;
                }
            }
            float by = r.y + h - 70 * s;
            if (_partyActive)
            {
                string next = _partyIndex + 1 < Modes.Length ? "Next: " + Modes[_partyIndex + 1].Title : "See party results";
                if (Hud.Button(new Rect(r.x + 30 * s, by, w - 60 * s, 50 * s), next, 20)) ContinueAfterResults();
            }
            else
            {
                float bw = (w - 70 * s) * 0.5f;
                if (Hud.Button(new Rect(r.x + 30 * s, by, bw, 50 * s), "Play Again", 20)) RestartCurrent();
                if (Hud.Button(new Rect(r.x + 40 * s + bw, by, bw, 50 * s), "Main Menu", 20)) ReturnToMenu();
            }
        }

        private void DrawPartySummary()
        {
            float s = Hud.Scale;
            float w = 600 * s, h = 480 * s;
            Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Hud.Panel(r);
            List<Participant> sorted = new List<Participant>(_participants);
            sorted.Sort((a, b) => b.PartyPoints.CompareTo(a.PartyPoints));
            Hud.Label(new Rect(r.x, r.y + 14 * s, w, 50 * s), sorted.Count > 0 ? sorted[0].Name + " WINS THE PARTY!" : "Party over",
                32, new Color(1f, 0.8f, 0.2f), TextAnchor.MiddleCenter);
            float y = r.y + 90 * s;
            int place = 0, last = int.MinValue;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].PartyPoints != last) { place = i + 1; last = sorted[i].PartyPoints; }
                Hud.Rect(new Rect(r.x + 40 * s, y + 5 * s, 16 * s, 16 * s), sorted[i].Color);
                Hud.Label(new Rect(r.x + 66 * s, y, w - 100 * s, 30 * s), Ordinal(place) + "   " + sorted[i].Name + "   " + sorted[i].PartyPoints + " pts", 21, Color.white);
                y += 38 * s;
            }
            if (Hud.Button(new Rect(r.x + 30 * s, r.y + h - 70 * s, w - 60 * s, 50 * s), "Main Menu", 20))
            {
                _screen = ScreenId.Menu;
            }
        }

        private static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return n + "th";
            switch (n % 10) { case 1: return n + "st"; case 2: return n + "nd"; case 3: return n + "rd"; default: return n + "th"; }
        }
    }
}
