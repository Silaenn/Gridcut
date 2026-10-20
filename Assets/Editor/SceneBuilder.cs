using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace GridCut.EditorTools
{
    /// <summary>
    /// Builds the Main scene UI hierarchy (uGUI) with all components and serialized
    /// references wired. Run once from batch or the GridCut menu; afterwards the UI
    /// is ordinary scene objects and can be restyled in the editor. The scene is
    /// rebuilt from scratch each time (idempotent by design).
    /// </summary>
    public static class SceneBuilder
    {
        const string MainScene = "Assets/Scenes/Main.unity";
        static TMP_FontAsset _font;

        [MenuItem("GridCut/Build Main Scene")]
        public static void BuildFromMenu()
        {
            Build();
            Debug.Log("[SceneBuilder] Main scene built.");
        }

        public static void Build()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/LiberationSans SDF.asset");
            if (_font == null)
                Debug.LogWarning("[SceneBuilder] TMP font asset not found; texts use TMP default.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ------- root / canvas / event system -------
            var root = new GameObject("GridCutRoot", typeof(Game.AppRoot));
            var app = root.GetComponent<Game.AppRoot>();

            var canvasGo = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);
            scaler.matchWidthOrHeight = 0.5f;

            var safeGo = new GameObject("SafeArea", typeof(RectTransform), typeof(Game.SafeArea));
            safeGo.transform.SetParent(canvasGo.transform, false);
            var safe = (RectTransform)safeGo.transform;
            Stretch(safe);

            var bg = Panel(safe, "Background", ThemeColor.background);

            var esGo = new GameObject("EventSystem", typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            esGo.transform.SetParent(canvasGo.transform, false);

            // ------- Home -------
            var homeGo = Img("Home", typeof(RectTransform));
            homeGo.transform.SetParent(safe, false);
            Stretch((RectTransform)homeGo.transform);
            var home = homeGo.AddComponent<Game.HomeScreen>();
            var homeRoot = (RectTransform)homeGo.transform;

            var title = Tmp(homeRoot, "Title", "GRIDCUT", 96, ThemeColor.accent, new Vector2(900, 120));
            Anchor(title.rectTransform, 0.5f, 0.80f);
            var sub = Tmp(homeRoot, "Sub", "potong grid menjadi persegi \u00b7 setiap angka = luasnya",
                24, Color.white, new Vector2(900, 40));
            Anchor(sub.rectTransform, 0.5f, 0.71f);

            var continueBtn = Btn(homeRoot, "ContinueBtn", "Lanjutkan", new Vector2(380, 72), 0.60f, 0.5f);
            var selectBtn = Btn(homeRoot, "SelectBtn", "Pilih Level", new Vector2(380, 72), 0.485f, 0.5f);
            var soundBtn = Btn(homeRoot, "SoundBtn", "Suara: nyala", new Vector2(380, 72), 0.375f, 0.5f);
            var resetBtn = Btn(homeRoot, "ResetBtn", "Reset Progres", new Vector2(380, 72), 0.265f, 0.5f);
            var themesHeader = Btn(homeRoot, "ThemesHeader", "\u25CF  Tema", new Vector2(380, 52), 0.135f, 0.5f);
            themesHeader.interactable = false;

            var swatches = new Button[Game.ThemeLibrary.All.Length];
            for (int i = 0; i < swatches.Length; i++)
            {
                var th = Game.ThemeLibrary.All[i];
                var b = Btn(homeRoot, $"Theme{i}", th.Name, new Vector2(152, 58), 0.015f, 0.5f,
                    Game.ThemeApplier.Parse(th.Rects[0]));
                var rt = b.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2((i - (swatches.Length - 1) / 2f) * 172f, 0);
                swatches[i] = b;
            }

            var confirmGo = Img("ConfirmReset", typeof(RectTransform));
            confirmGo.transform.SetParent(homeRoot, false);
            Stretch((RectTransform)confirmGo.transform);
            Dim(confirmGo.transform);
            var q = Tmp((RectTransform)confirmGo.transform, "Question", "Hapus semua progres?",
                34, Color.white, new Vector2(700, 50));
            Anchor(q.rectTransform, 0.5f, 0.56f);
            var yes = Btn((RectTransform)confirmGo.transform, "Yes", "Ya, hapus", new Vector2(300, 66), 0.42f, 0.5f);
            var no = Btn((RectTransform)confirmGo.transform, "No", "Batal", new Vector2(300, 66), 0.42f, 0.5f);
            yes.GetComponent<RectTransform>().anchoredPosition = new Vector2(-165, 0);
            no.GetComponent<RectTransform>().anchoredPosition = new Vector2(165, 0);
            confirmGo.SetActive(false);

            // ------- LevelSelect -------
            var lsGo = Img("LevelSelect", typeof(RectTransform));
            lsGo.transform.SetParent(safe, false);
            Stretch((RectTransform)lsGo.transform);
            var ls = lsGo.AddComponent<Game.LevelSelectScreen>();
            var lsRoot = (RectTransform)lsGo.transform;

            var backBtn = Btn(lsRoot, "BackBtn", "\u25C0 Kembali", new Vector2(260, 60), 0.885f, 0.12f);
            var prevBtn = Btn(lsRoot, "PrevBtn", "\u25C0", new Vector2(80, 60), 0.885f, 0.36f);
            var nextBtn = Btn(lsRoot, "NextBtn", "\u25B6", new Vector2(80, 60), 0.885f, 0.64f);
            var pageLabel = Tmp(lsRoot, "PageLabel", "Level 1\u201345", 28, Color.white, new Vector2(360, 44));
            Anchor(pageLabel.rectTransform, 0.5f, 0.885f);

            var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(lsRoot, false);
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            var grt = (RectTransform)gridGo.transform;
            grt.anchorMin = new Vector2(0.06f, 0.03f);
            grt.anchorMax = new Vector2(0.94f, 0.80f);
            grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;
            grid.cellSize = new Vector2(96, 92);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.MiddleCenter;

            // ------- Gameplay -------
            var gpGo = Img("Gameplay", typeof(RectTransform));
            gpGo.transform.SetParent(safe, false);
            Stretch((RectTransform)gpGo.transform);
            var gp = gpGo.AddComponent<Game.GameplayScreen>();
            var gpRoot = (RectTransform)gpGo.transform;

            var slotGo = new GameObject("BoardSlot", typeof(RectTransform));
            slotGo.transform.SetParent(gpRoot, false);
            var slot = (RectTransform)slotGo.transform;
            Stretches(slot, Vector2.zero, new Vector2(-360, 0));

            var boardGo = new GameObject("Board", typeof(RectTransform));
            boardGo.transform.SetParent(slot, false);
            var board = boardGo.AddComponent<Game.BoardView>();

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(gpRoot, false);
            var panel = (RectTransform)panelGo.transform;
            panel.anchorMin = new Vector2(1, 0);
            panel.anchorMax = new Vector2(1, 1);
            panel.offsetMin = new Vector2(-360, 0);
            panel.offsetMax = Vector2.zero;

            var levelLabel = Tmp(panel, "LevelLabel", "Level 1", 34, Color.white, new Vector2(336, 54));
            Anchor(levelLabel.rectTransform, 0.5f, 0.92f);
            var progressLabel = Tmp(panel, "ProgressLabel", "0/0", 28, Color.white, new Vector2(336, 40));
            Anchor(progressLabel.rectTransform, 0.5f, 0.80f);
            var timeLabel = Tmp(panel, "TimeLabel", "0:00", 24, Color.white, new Vector2(336, 36));
            Anchor(timeLabel.rectTransform, 0.5f, 0.70f);

            var undoBtn = Btn(panel, "UndoBtn", "\u21BA  Undo", new Vector2(324, 62), 0.52f, 0.5f);
            var gpResetBtn = Btn(panel, "ResetBtn", "\u21BB  Reset", new Vector2(324, 62), 0.38f, 0.5f);
            var hintBtn = Btn(panel, "HintBtn", "\u2728  Hint", new Vector2(324, 62), 0.24f, 0.5f);
            var gpBackBtn = Btn(panel, "BackBtn", "\u25C0  Menu", new Vector2(324, 62), 0.10f, 0.5f);

            var winGo = Img("WinOverlay", typeof(RectTransform));
            winGo.transform.SetParent(gpRoot, false);
            Stretch((RectTransform)winGo.transform);
            Dim(winGo.transform);
            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(winGo.transform, false);
            var crt = (RectTransform)cardGo.transform;
            crt.sizeDelta = new Vector2(440, 340);
            var cardImg = cardGo.GetComponent<Image>();
            cardImg.raycastTarget = false;
            cardImg.color = ThemeColor.cellEmpty;

            var winTitle = Tmp(crt, "WinTitle", "Selesai!", 46, Color.black, new Vector2(420, 90));
            Anchor(winTitle.rectTransform, 0.5f, 0.745f);
            var winDetail = Tmp(crt, "WinDetail", "", 28, Color.black, new Vector2(420, 90));
            Anchor(winDetail.rectTransform, 0.5f, 0.515f);
            var winNext = Btn(crt, "WinNextBtn", "Level Berikutnya \u2192", new Vector2(360, 64), 0.5f, 0.5f);
            winNext.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -60);
            var winMenu = Btn(crt, "WinMenuBtn", "Pilih Level", new Vector2(360, 56), 0.5f, 0.5f);
            winMenu.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -140);
            winGo.SetActive(false);

            // ------- wire serialized references -------
            app.Background = bg;
            app.Home = home;
            app.LevelSelect = ls;
            app.Gameplay = gp;

            home.Subtitle = sub;
            home.ContinueBtn = continueBtn;
            home.SelectBtn = selectBtn;
            home.SoundBtn = soundBtn;
            home.ResetBtn = resetBtn;
            home.ThemeSwatches = swatches;
            home.ConfirmDialog = confirmGo;
            home.ConfirmYes = yes;
            home.ConfirmNo = no;

            ls.PageLabel = pageLabel;
            ls.PrevBtn = prevBtn;
            ls.NextBtn = nextBtn;
            ls.BackBtn = backBtn;
            ls.GridContent = grt;

            gp.Board = board;
            gp.BoardSlot = slot;
            gp.Panel = panel;
            gp.LevelLabel = levelLabel;
            gp.ProgressLabel = progressLabel;
            gp.TimeLabel = timeLabel;
            gp.UndoBtn = undoBtn;
            gp.ResetBtn = gpResetBtn;
            gp.HintBtn = hintBtn;
            gp.BackBtn = gpBackBtn;
            gp.WinOverlay = winGo;
            gp.WinTitle = winTitle;
            gp.WinDetail = winDetail;
            gp.WinNextBtn = winNext;
            gp.WinMenuBtn = winMenu;

            homeGo.SetActive(true);
            lsGo.SetActive(false);
            gpGo.SetActive(false);

            MarkComponent(app); MarkComponent(home); MarkComponent(ls); MarkComponent(gp);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainScene);
        }

        static void MarkComponent(Object c) => EditorUtility.SetDirty(c);

        // theme starting colors (mirrors ThemeLibrary.Paper / kept in sync manually)
        static class ThemeColor
        {
            public const string backgroundHex = "#F5F1E8";
            public const string accentHex = "#E4572E";
            public const string cellEmptyHex = "#FBF9F4";
            public static Color background => Hex(backgroundHex);
            public static Color accent => Hex(accentHex);
            public static Color cellEmpty => Hex(cellEmptyHex);
            static Color Hex(string s) =>
                ColorUtility.TryParseHtmlString(s, out var c) ? c : Color.magenta;
        }

        // ------------------------------ helpers ---------------------------------

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void Stretches(RectTransform rt, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        static GameObject Img(string name, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            return go;
        }

        static Image Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt);
            return img;
        }

        static void Dim(Transform parent)
        {
            var dim = Panel(parent, "Dim", new Color(0, 0, 0, 0.55f));
            dim.raycastTarget = true;
        }

        static TMP_Text Tmp(RectTransform parent, string name, string content,
            float size, Color color, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = size;
            t.color = color;
            if (_font != null) t.font = _font;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizeDelta;
            return t;
        }

        static Button Btn(RectTransform parent, string name, string label,
            Vector2 size, float anchorY, float anchorX, Color? colorOverride = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var img = go.GetComponent<Image>();
            img.color = colorOverride ?? ThemeColor.accent;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(anchorX, anchorY);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            var t = Tmp(rt, "Label", label, 28, Color.black, Vector2.zero);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }

        static void Anchor(RectTransform rt, float ax, float ay)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
