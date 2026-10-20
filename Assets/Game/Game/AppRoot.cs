using UnityEngine;
using UnityEngine.UI;

namespace GridCut.Game
{
    /// <summary>
    /// Scene-owned app shell (sits on the scene root). Wires navigation between the
    /// three screen roots which all live in the scene next to it. Initialization
    /// (save data, level set, audio, theme) happens here once, in Awake.
    /// </summary>
    public class AppRoot : MonoBehaviour
    {
        public static AppRoot Instance { get; private set; }

        [Header("Screens (scene references)")]
        public HomeScreen Home;
        public LevelSelectScreen LevelSelect;
        public GameplayScreen Gameplay;
        public Image Background;

        IAdService _ads;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            // Start, bukan Awake: pastikan Awake semua screen sudah jalan sehingga
            // subscriber tema sudah terdaftar saat ApplySaved memicu re-tint.
            SaveStore.Load();
            LevelRepository.Load();
            AudioService.CreateIfMissing();
            _ads = ResolveAds();

            ThemeApplier.ApplySaved();
            if (Background != null)
                ThemeApplier.Subscribe(ThemeApplier.Role.Background, c => Background.color = c, true);
            Application.targetFrameRate = 60;

            Gameplay.LevelFinished += OnLevelFinished;
            OpenHome();
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

        void ShowOnly(GameObject go)
        {
            Home.gameObject.SetActive(go == Home.gameObject);
            LevelSelect.gameObject.SetActive(go == LevelSelect.gameObject);
            Gameplay.gameObject.SetActive(go == Gameplay.gameObject);
        }

        public void OpenHome()
        {
            ShowOnly(Home.gameObject);
            Home.Show();
        }
        public void OpenLevelSelect()
        {
            ShowOnly(LevelSelect.gameObject);
            LevelSelect.Show();
        }

        public void StartLevel(int levelId)
        {
            ShowOnly(Gameplay.gameObject);
            Gameplay.Show(levelId);
        }

        /// <summary>Continue button: jumps to the highest unlocked level.</summary>
        public void StartContinue() => StartLevel(SaveStore.UnlockedLevel);

        void OnLevelFinished(int levelId)
        {
            // placeholder: portals inject their interstitial via IAdService
            if (levelId < SaveStore.LevelCount && levelId % 5 == 0 && _ads.InterstitialReady)
                _ads.ShowInterstitial(ok => { });
        }
    }
}
