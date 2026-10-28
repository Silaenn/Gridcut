using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace GridCut.Game
{
    /// <summary>
    /// Scene-owned app shell (sits on UICanvas). Wires navigation between the three
    /// screen roots which all live in the scene. Per UI rules: screens are children
    /// of one safe-area root; screen switches fade + slide in over 200 ms unscaled;
    /// Escape/Back returns to the previous screen via a history stack; every button
    /// gets pressed-scale 0.95 and disabled alpha 0.4.
    /// </summary>
    public class AppRoot : MonoBehaviour
    {
        public static AppRoot Instance { get; private set; }

        [Header("Screens (scene references)")]
        public HomeScreen Home;
        public LevelSelectScreen LevelSelect;
        public GameplayScreen Gameplay;
        public TutorialScreen Tutorial;
        public Image Background;

        [Header("Shared sprites (PNG assets, wired in scene)")]
        public Sprite StarFull, StarEmpty, Padlock;

        [Header("Shared fonts (SDF assets, wired in scene)")]
        public TMP_FontAsset FontBold, FontMedium;   // SemiBold = default TMP Settings

        [Header("Shapes (9-slice, wired in scene)")]
        public Sprite Rounded;       // Assets/Game/UI/Rounded.png (putih, radius 56, border 56)
        public Sprite RoundedRing;   // Assets/Game/UI/RoundedRing.png (cincin 10px, radius luar 56, border 56)

        enum ScreenId { Home = 0, LevelSelect = 1, Gameplay = 2, Tutorial = 3 }
        readonly List<ScreenId> _history = new();
        ScreenId _current = ScreenId.Home;
        Coroutine _fade;

        IAdService _ads;
        Action<Color> _bgTint;
        BgDecor _decor;
        int _popupCount;

        void Awake() => Instance = this;

        void Start()
        {
            // Start, bukan Awake: pastikan Awake semua screen sudah jalan (listener,
            // auto-wire) sebelum ApplySaved memicu re-tint lewat subscriber OnEnable.
            AutoWireScreens();
            // slot null = meta PNG belum terimpor / scene belum direload dengan wire baru
            if (StarFull == null || StarEmpty == null || Padlock == null)
                Debug.LogWarning("[AppRoot] Sprite belum lengkap di Inspector: StarFull/StarEmpty/Padlock");
            SaveStore.Load();
            LevelRepository.Load();
            AudioService.CreateIfMissing();
            _ads = ResolveAds();

            ThemeApplier.ApplySaved();
            if (Background != null)
            {
                _bgTint = c => Background.color = c;
                ThemeApplier.Subscribe(ThemeApplier.Role.Background, _bgTint, true);
                _decor = Background.GetComponent<BgDecor>();
            }
            Application.targetFrameRate = 60;

            if (Gameplay != null) Gameplay.LevelCompleted += OnLevelCompleted;
            BakeButtonFeel();
            Switch(ScreenId.Home, push: false);
        }

        void OnDestroy()
        {
            // play mode without domain reload keeps statics across sessions; leave
            // nothing behind that would touch destroyed objects on the next play
            ThemeApplier.Unsubscribe(_bgTint);
            if (Gameplay != null) Gameplay.LevelCompleted -= OnLevelCompleted;
        }

        /// <summary>
        /// Fallback wiring: serialized refs written by tooling can fail to bind;
        /// recover from the hierarchy and log exactly which refs were missing.
        /// </summary>
        void AutoWireScreens()
        {
            if (Home == null)
            {
                Home = GetComponentInChildren<HomeScreen>(true);
                if (Home != null) Debug.LogWarning("[AppRoot] Home ter-wire otomatis dari hierarki");
            }
            if (LevelSelect == null)
            {
                LevelSelect = GetComponentInChildren<LevelSelectScreen>(true);
                if (LevelSelect != null) Debug.LogWarning("[AppRoot] LevelSelect ter-wire otomatis dari hierarki");
            }
            if (Gameplay == null)
            {
                Gameplay = GetComponentInChildren<GameplayScreen>(true);
                if (Gameplay != null) Debug.LogWarning("[AppRoot] Gameplay ter-wire otomatis dari hierarki");
            }
            if (Tutorial == null)
            {
                Tutorial = GetComponentInChildren<TutorialScreen>(true);
                if (Tutorial != null) Debug.LogWarning("[AppRoot] Tutorial ter-wire otomatis dari hierarki");
            }
            if (Background == null)
            {
                var bg = GetComponentInChildren<Image>(true);
                if (bg != null && bg.name == "Background")
                {
                    Background = bg;
                    Debug.LogWarning("[AppRoot] Background ter-wire otomatis dari hierarki");
                }
            }
            Debug.Log($"[AppRoot] refs \u2192 Home={Home != null} LevelSelect={LevelSelect != null} Gameplay={Gameplay != null} Tutorial={Tutorial != null} Background={Background != null}");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (PopupView.CloseTop()) return;   // Back/Escape menutup popup teratas
                NavBack();
            }
        }

        static IAdService ResolveAds()
        {
#if GRIDCUT_CRAZYGAMES || GRIDCUT_COOLMATH || GRIDCUT_ARMORGAMES
            return new PortalAdService();
#else
            return NullAdService.Instance;
#endif
        }

        // ------------------------------ navigation ------------------------------

        void Switch(ScreenId id, bool push)
        {
            if (push) _history.Add(_current);
            _current = id;
            var go = id == ScreenId.Home ? Home != null ? Home.gameObject : null
                   : id == ScreenId.LevelSelect ? LevelSelect != null ? LevelSelect.gameObject : null
                   : id == ScreenId.Gameplay ? Gameplay != null ? Gameplay.gameObject : null
                   : Tutorial != null ? Tutorial.gameObject : null;
            if (go == null)
            {
                Debug.LogError($"[AppRoot] Layar {id} tidak tersedia — referensi hilang, cek Inspector AppRoot di UICanvas");
                return;
            }
            ShowOnly(go);
            if (_decor != null) _decor.SetBoxesVisible(id == ScreenId.Home);
            AudioService.PlayMusic(
                id == ScreenId.Home || id == ScreenId.LevelSelect ? "home" : "game");

            // per-screen refresh; Gameplay refreshes via StartLevel (needs levelId)
            switch (id)
            {
                case ScreenId.Home: Home.Show(); break;
                case ScreenId.LevelSelect: LevelSelect.Show(); break;
            }
        }

        public void OpenHome() => Switch(ScreenId.Home, push: false);
        public void OpenLevelSelect() => Switch(ScreenId.LevelSelect, push: true);

        public void StartLevel(int levelId)
        {
            // dari tutorial (Skip / Play penutup) jangan menumpuk tutorial di riwayat:
            // Back dari Level 1 harus pulang ke menu, bukan kembali ke tutorial selesai
            bool fromTutorial = _current == ScreenId.Tutorial;
            Switch(ScreenId.Gameplay, push: !fromTutorial);
            Gameplay.Show(levelId);
        }

        // ------------------------------ popup blocking ------------------------------

        /// <summary>Spec bagian 9: selama popup terbuka, tombol layar di bawahnya
        /// tidak bisa dipakai dan drag yang berjalan dibatalkan. Backdrop juga
        /// memblokir raycast; ini lapisan kedua untuk tombol/keys.</summary>
        public void PopupOpened()
        {
            _popupCount++;
            ApplyPopupBlocking();
        }

        public void PopupClosed()
        {
            _popupCount = Mathf.Max(0, _popupCount - 1);
            ApplyPopupBlocking();
        }

        void ApplyPopupBlocking()
        {
            bool block = _popupCount > 0;
            SetScreenInteractive(Home, !block);
            SetScreenInteractive(LevelSelect, !block);
            SetScreenInteractive(Gameplay, !block);
            SetScreenInteractive(Tutorial, !block);
            if (block && Gameplay != null && Gameplay.Board != null)
                Gameplay.Board.ClearPreview();   // drag di layar bawah dibatalkan
        }

        static void SetScreenInteractive(MonoBehaviour screen, bool on)
        {
            if (screen == null) return;
            var cg = screen.GetComponent<CanvasGroup>();
            if (cg == null) return;
            cg.interactable = on;
            cg.blocksRaycasts = on;
        }

        /// <summary>Continue button: tutorial first (once per save), lalu level tertinggi.</summary>
        public void StartContinue()
        {
            if (!SaveStore.TutorialDone) { StartTutorial(); return; }
            StartLevel(SaveStore.UnlockedLevel);
        }

        public void StartTutorial()
        {
            Switch(ScreenId.Tutorial, push: true);
            if (Tutorial != null) Tutorial.Begin();
        }

        /// <summary>Back one screen (Escape/Back button); lands on Home with no history.</summary>
        public void NavBack()
        {
            AudioService.Play("tap");
            if (_history.Count == 0)
            {
                Switch(ScreenId.Home, push: false);
                return;
            }
            var prev = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            Switch(prev, push: false);
        }

        void ShowOnly(GameObject go)
        {
            Home.gameObject.SetActive(go == Home.gameObject);
            LevelSelect.gameObject.SetActive(go == LevelSelect.gameObject);
            Gameplay.gameObject.SetActive(go == Gameplay.gameObject);
            if (Tutorial != null) Tutorial.gameObject.SetActive(go == Tutorial.gameObject);
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeIn(go));
        }

        /// <summary>Fade + small slide, 200 ms at unscaled time.</summary>
        IEnumerator FadeIn(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            var rt = go.GetComponent<RectTransform>();
            var basePos = rt.anchoredPosition;
            cg.alpha = 0f;
            rt.anchoredPosition = basePos + new Vector2(24f, 0f);
            const float dur = 0.2f;
            for (float t = 0f; t < dur;)
            {
                t += Time.unscaledDeltaTime;
                var k = Mathf.Clamp01(t / dur);
                cg.alpha = k;
                rt.anchoredPosition = basePos + new Vector2(24f * (1f - k), 0f);
                yield return null;
            }
            cg.alpha = 1f;
            rt.anchoredPosition = basePos;
        }

        /// <summary>Keadaan tombol (spec): ButtonFX pada tiap Button yang belum diatur
        /// PressFx resep; nonaktif alpha 0,45 via CanvasGroup (di ButtonFX).</summary>
        void BakeButtonFeel()
        {
            foreach (var b in GetComponentsInChildren<Button>(true))
                if (b.GetComponent<ButtonFX>() == null) UiFactory.PressFx(b, -12f);
        }

        void OnLevelCompleted(int levelId, int stars)
        {
            // placeholder: portals inject their interstitial via IAdService
            if (levelId < SaveStore.LevelCount && levelId % 5 == 0 && _ads.InterstitialReady)
                _ads.ShowInterstitial(ok => { });
        }
    }
}
