using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GridCut.Game
{
    /// <summary>
    /// Level select (scene component). Paged 45-button grid populated at runtime from
    /// SaveStore progress; static chrome (page label, arrows, back) is wired in scene.
    /// </summary>
    public class LevelSelectScreen : MonoBehaviour
    {
        [Header("Wired in scene")]
        public TMP_Text PageLabel;
        public TMP_Text SelTitle;           // header "Pilih Level"
        public Button PrevBtn, NextBtn, BackBtn;
        public Image PrevIcon, NextIcon;    // panah halaman Kenney (diwarnai warna teks tema)
        public RectTransform GridContent;   // GridLayoutGroup

        int _page;
        const int PerPage = 15;             // spec: kisi 5 x 3
        Action<Color> _textTint, _frameTint;

        void Awake()
        {
            AutoWire();
            PrevBtn.onClick.AddListener(Prev);
            NextBtn.onClick.AddListener(Next);
            BackBtn.onClick.AddListener(Back);
            _textTint = c =>
            {
                if (PageLabel != null) PageLabel.color = c;
                if (SelTitle != null) SelTitle.color = c;
                if (PrevIcon != null) PrevIcon.color = c;
                if (NextIcon != null) NextIcon.color = c;
            };
            _frameTint = _ =>
            {
                UiFactory.FrameTint(PrevBtn, true);
                UiFactory.FrameTint(NextBtn, true);
                UiFactory.FrameTint(BackBtn, true);
            };
            // keadaan tombol (spec): tekan-turun + alpha nonaktif
            UiFactory.PressFx(PrevBtn, -12f);
            UiFactory.PressFx(NextBtn, -12f);
            UiFactory.PressFx(BackBtn, -12f);

            // ubin bake scene: 15 slot dengan struktur statis — klik disambungkan
            // sekali di sini (state/warna diputikkan PaintTile tiap halaman)
            if (GridContent != null && GridContent.childCount > 0)
                for (int s = 0; s < GridContent.childCount; s++)
                {
                    var b = (GridContent.GetChild(s) as RectTransform)
                            ?.GetComponentInChildren<Button>(true);
                    if (b == null) continue;
                    int slot = s;
                    b.onClick.AddListener(() => OnTileTap(slot));
                }
        }

        void OnEnable()
        {
            // applyImmediately re-tints PageLabel from the current theme on appearance
            ThemeApplier.Subscribe(ThemeApplier.Role.Text, _textTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _frameTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _frameTint, true);
        }

        void OnDisable()
        {
            ThemeApplier.Unsubscribe(_textTint);
            ThemeApplier.Unsubscribe(_frameTint);
        }

        /// <summary>Recovers scene refs by child name when serialized wiring fails.</summary>
        void AutoWire()
        {
            var t = transform;
            if (PageLabel == null)
            {
                var p = t.Find("PageLabel");
                if (p != null) PageLabel = p.GetComponentInChildren<TMP_Text>(true);
            }
            if (SelTitle == null) SelTitle = UiFactory.Wire<TMP_Text>(this, SelTitle, "SelTitle");
            if (PrevBtn == null) PrevBtn = UiFactory.Wire<Button>(this, null, "PrevBtn");
            if (NextBtn == null) NextBtn = UiFactory.Wire<Button>(this, null, "NextBtn");
            if (BackBtn == null) BackBtn = UiFactory.Wire<Button>(this, null, "BackBtn");
            if (PrevIcon == null) PrevIcon = UiFactory.Wire<Image>(this, null, "PrevBtn/Icon");
            if (NextIcon == null) NextIcon = UiFactory.Wire<Image>(this, null, "NextBtn/Icon");
            if (GridContent == null)
            {
                var g = t.Find("Grid");
                if (g != null) GridContent = g.GetComponent<RectTransform>();
            }
        }

        public void Prev() { _page = Mathf.Max(0, _page - 1); Populate(); AudioService.Play("tap"); }
        public void Next()
        {
            int pages = Mathf.CeilToInt(SaveStore.LevelCount / (float)PerPage);
            _page = Mathf.Min(pages - 1, _page + 1);
            Populate();
            AudioService.Play("tap");
        }
        public void Back() => AppRoot.Instance.NavBack();

        public void Show()
        {
            _page = (SaveStore.UnlockedLevel - 1) / PerPage;
            Populate();
        }

        void Populate()
        {
            int start = _page * PerPage + 1;
            int end = Mathf.Min(start + PerPage - 1, SaveStore.LevelCount);
            int pages = Mathf.CeilToInt(SaveStore.LevelCount / (float)PerPage);
            PageLabel.text = $"{_page + 1} / {pages}";

            if (GridContent.childCount == 0)
            {
                // fallback: scene belum bake — bangun ubin runtime seperti dulu
                for (int i = GridContent.childCount - 1; i >= 0; i--)
                    Destroy(GridContent.GetChild(i).gameObject);
                for (int id = start; id <= end; id++)
                    Tile(id.ToString(), id);
                return;
            }

            // slot bake scene: struktur statis, hanya state/warna/teks yang diganti
            for (int s = 0; s < GridContent.childCount; s++)
            {
                var seat = (RectTransform)GridContent.GetChild(s);
                int id = start + s;
                bool show = id >= 1 && id <= SaveStore.LevelCount;
                seat.gameObject.SetActive(show);
                if (show) PaintTile(seat, id);
            }
        }

        void OnTileTap(int slotIdx)
        {
            int id = _page * PerPage + slotIdx + 1;
            if (id < 1 || id > SaveStore.LevelCount) return;
            var seat = (RectTransform)GridContent.GetChild(slotIdx);
            if (id > SaveStore.UnlockedLevel)
            {
                AudioService.Play("invalid");
                StartCoroutine(Shake(seat));
                return;
            }
            AudioService.Play("tap");
            AppRoot.Instance.StartLevel(id);
        }

        /// <summary>Warnai ubin bake sesuai state level (spec bagian ubin):
        /// Selesai = akar Tepi + Fill Permukaan + shadow Tepi, angka Teks,
        /// bintang Utama/Tepi. Aktif = akar Aksen + shadow bayangan Aksen +
        /// Glow, angka auto-kontras, bintang kosong Latar. Terkunci = warna
        /// Ubin terkunci + gembok (tanpa shadow/angka/bintang).</summary>
        void PaintTile(RectTransform seat, int id)
        {
            var btnT = seat.Find("Btn");
            if (btnT == null) return;
            var rootImg = btnT.GetComponent<Image>();
            var shadow = btnT.GetComponent<Shadow>();
            var glow = seat.Find("Glow");
            var fill = btnT.Find("Fill");
            var fillImg = fill != null ? fill.GetComponent<Image>() : null;
            var num = btnT.Find("num") != null ? btnT.Find("num").GetComponent<TMP_Text>() : null;
            var numGo = btnT.Find("num") != null ? btnT.Find("num").gameObject : null;
            var rowGo = btnT.Find("stars") != null ? btnT.Find("stars").gameObject : null;
            var lockGo = btnT.Find("lock") != null ? btnT.Find("lock").gameObject : null;
            var lockImg = btnT.Find("lock") != null ? btnT.Find("lock").GetComponent<Image>() : null;

            bool locked = id > SaveStore.UnlockedLevel;
            bool active = id == SaveStore.UnlockedLevel;

            if (glow != null) glow.gameObject.SetActive(active);
            if (lockGo != null) lockGo.SetActive(locked);
            if (fill != null) fill.gameObject.SetActive(!locked && !active);
            if (numGo != null) numGo.SetActive(!locked);
            if (rowGo != null) rowGo.SetActive(!locked);
            if (shadow != null) shadow.enabled = !locked;
            if (lockImg != null) lockImg.color = ThemeApplier.ColorOf(ThemeApplier.Role.Lock);
            if (num != null) num.text = id.ToString();

            if (locked)
            {
                rootImg.color = ThemeApplier.ColorOf(ThemeApplier.Role.Locked);
                return;
            }

            var accent = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
            rootImg.color = active ? accent : ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
            if (shadow != null)
                shadow.effectColor = active ? ThemeApplier.ShadowOf(accent)
                    : ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
            if (fillImg != null) fillImg.color = ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty);
            if (num != null)
                num.color = active ? ThemeApplier.ContrastTextOn(accent)
                    : ThemeApplier.ColorOf(ThemeApplier.Role.Text);
            if (rowGo != null)
                UiFactory.SetStars(rowGo.transform, SaveStore.BestStars(id),
                    accent,
                    active ? ThemeApplier.ColorOf(ThemeApplier.Role.Background)
                           : ThemeApplier.ColorOf(ThemeApplier.Role.GridLine));
        }

        // ------------------------------ resep ubin level ------------------------------

        /// <summary>Ubin level 224×224 (spec). Selesai: berbingkai — akar Tepi
        /// (radius 44) + Fill Permukaan (radius 38, margin 6) + shadow (0,-12) Tepi,
        /// angka Bold 80 warna Teks, bintang (diperoleh = Utama, belum = Tepi).
        /// Aktif: akar Aksen (radius 44) + shadow (0,-12) bayangan Aksen, Glow Aksen
        /// α0,3 radius 56 ×1,0 dengan ukuran +12 px tiap sisi di belakang, angka
        /// auto-kontras, bintang belum diperoleh = Latar. Terkunci: warna Ubin
        /// terkunci tanpa bingkai/shadow; hanya gembok 64×64 warna Gembok.</summary>
        void Tile(string label, int levelId)
        {
            var rounded = AppRoot.Instance != null ? AppRoot.Instance.Rounded : null;
            bool locked = levelId > SaveStore.UnlockedLevel;
            bool active = levelId == SaveStore.UnlockedLevel;

            // Seat menampung Glow (state aktif saja) di belakang Btn; grid mengatur
            // posisi sel per halaman
            var seatGo = new GameObject(label, typeof(RectTransform));
            var seat = (RectTransform)seatGo.transform;
            seat.SetParent(GridContent, false);
            seat.sizeDelta = new Vector2(224f, 224f);

            var go = new GameObject("Btn", typeof(RectTransform), typeof(Button), typeof(Image));
            var root = (RectTransform)go.transform;
            root.SetParent(seat, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(224f, 224f);
            var btn = go.GetComponent<Button>();
            var img = go.GetComponent<Image>();
            if (rounded != null)
            {
                img.sprite = rounded;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1.27f;          // radius 44 (56/1,27)
            }

            if (locked)
            {
                img.color = ThemeApplier.ColorOf(ThemeApplier.Role.Locked);
                var lockImg = UiFactory.Image(root, "lock", ThemeApplier.ColorOf(ThemeApplier.Role.Lock));
                lockImg.sprite = AppRoot.Instance.Padlock;    // PNG gembok, di-wire di AppRoot
                lockImg.preserveAspect = true;
                var lrt = (RectTransform)lockImg.transform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(64f, 64f);        // spec: gembok 64×64
                lrt.anchoredPosition = Vector2.zero;
                btn.onClick.AddListener(() =>
                {
                    AudioService.Play("invalid");
                    StartCoroutine(Shake(seat));
                });
                return;
            }

            var accent = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
            var bold = AppRoot.Instance != null ? AppRoot.Instance.FontBold : null;
            if (active)
            {
                // Glow Rounded Aksen α0,3, radius 56 ×1,0, +12 px tiap sisi, di belakang Btn
                var glowC = accent; glowC.a = 0.3f;
                var glow = UiFactory.Image(seat, "Glow", glowC);
                if (rounded != null)
                {
                    glow.sprite = rounded;
                    glow.type = UnityEngine.UI.Image.Type.Sliced;
                    glow.pixelsPerUnitMultiplier = 1f;
                }
                var grt = glow.rectTransform;
                grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
                grt.sizeDelta = new Vector2(248f, 248f);
                grt.anchoredPosition = Vector2.zero;
                img.color = accent;
                UiFactory.ApplyShadow(img, new Vector2(0f, -12f), ThemeApplier.ShadowOf(accent));
                Face(root, levelId, ThemeApplier.ContrastTextOn(accent),
                    accent, ThemeApplier.ColorOf(ThemeApplier.Role.Background), bold);
            }
            else
            {
                img.color = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
                UiFactory.ApplyShadow(img, new Vector2(0f, -12f), ThemeApplier.ColorOf(ThemeApplier.Role.GridLine));
                var fill = UiFactory.Panel(root, "Fill", ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty),
                    false, rounded, 1.47f);
                fill.rectTransform.offsetMin = new Vector2(6f, 6f);
                fill.rectTransform.offsetMax = new Vector2(-6f, -6f);
                Face(fill.rectTransform, levelId, ThemeApplier.ColorOf(ThemeApplier.Role.Text),
                    accent, ThemeApplier.ColorOf(ThemeApplier.Role.GridLine), bold);
            }

            int levelRef = levelId;
            btn.onClick.AddListener(() =>
            {
                AudioService.Play("tap");
                AppRoot.Instance.StartLevel(levelRef);
            });
        }

        /// <summary>Angka Bold 80 + baris tiga bintang (32×32, jarak 8, 16 px di bawah angka).</summary>
        void Face(Transform host, int levelId, Color numberColor, Color starOn, Color starOff,
            TMP_FontAsset bold)
        {
            var t = UiFactory.Text(host, "num", levelId.ToString(), 80f, numberColor,
                TextAlignmentOptions.Center);
            if (bold != null) t.font = bold;
            var nrt = t.rectTransform;
            nrt.anchorMin = new Vector2(0f, 0.5f);
            nrt.anchorMax = new Vector2(1f, 0.5f);
            nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.sizeDelta = new Vector2(0f, 80f);
            nrt.anchoredPosition = new Vector2(0f, 20f);      // pusat angka sedikit di atas tengah

            var row = UiFactory.StarRow(host, "stars");
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.sizeDelta = new Vector2(112f, 32f);
            row.anchoredPosition = new Vector2(0f, -52f);     // 16 px di bawah batas bawah angka
            UiFactory.SetStars(row, SaveStore.BestStars(levelId), starOn, starOff);
        }

        /// <summary>Spec bagian 8: tap level terkunci menggetarkan tombol.</summary>
        IEnumerator Shake(RectTransform rt)
        {
            var base0 = rt.anchoredPosition;
            const float Wobble = 7f, Step = 0.035f;
            for (int i = 0; i < 6; i++)
            {
                rt.anchoredPosition = base0 + new Vector2(i % 2 == 0 ? Wobble : -Wobble, 0f);
                yield return new WaitForSeconds(Step);
            }
            rt.anchoredPosition = base0;
        }
    }
}
