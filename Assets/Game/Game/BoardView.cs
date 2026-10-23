using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using GridCut.Core;
// Input System punya enum TouchPhase sendiri yang senama dgn UnityEngine.TouchPhase
// (bakal ambil CS0104 bila pakai langsung) — jadi alias di sini.
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace GridCut.Game
{
    /// <summary>
    /// Renders a puzzle board (slab papan Rounded, tile sel Permukaan, clue labels,
    /// placed-rect fills) and handles pointer → cell input with a grid-snapped
    /// preview. The whole board rebuilds on level start and on screen resize;
    /// puzzles are small enough (max 14x8 cells) that rebuilds are effectively free.
    /// </summary>
    public class BoardView : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        public PuzzleSession Session { get; private set; }
        public float Cell { get; private set; }
        public int Cols { get; private set; }
        public int Rows { get; private set; }

        // spec event umpan balik (juice & audio)
        public event System.Action DragStarted;       // drag dimulai pada sebuah sel
        public event System.Action DragCellChanged;   // preview berpindah sel (termasuk clamp tepi)

        // spec sizes use 1280-wide reference units; ×1.35 matches the app-wide rebake
        public const float Ref = 1.35f;
        const float MaxCell = 150f * Ref;   // spec: cells never exceed 150 reference
        const float Margin = 48f * Ref;     // spec: 48 reference inset around the grid area
        // spec papan/sel — nilai px mengikuti radius (unit canvas, tanpa faktor Ref)
        const float Pad = 28f;              // padding sekeliling kisi di dalam papan
        const float Gap = 8f;               // jarak antar sel
        const float PieceInset = 6f;        // gabungan tile sel → akar keping (celah antar keping 8+6+6)
        const float FrameMargin = 6f;       // margin anak "Fill" di dalam akar keping

        /// <summary>Single source for cell sizing (spec margins + cap); shared with the compact-panel check.</summary>
        public static float EstimateCell(float slotW, float slotH, int cols, int rows) =>
            Mathf.Min(MaxCell, Mathf.Min((slotW - Margin * 2f) / cols, (slotH - Margin * 2f) / rows));

        RectTransform _rt;
        RectTransform _previewRt;   // preview drag: Fill + Ring + angka luas
        Image _pFill;
        Image _pRing;
        TMP_Text _pNum;
        List<Image> _rectFill = new List<Image>(64);          // akar keping per clue (valid)
        List<Image> _neutralFill = new List<Image>(64);       // fill rect netral (spec 18)
        List<TMP_Text> _clueText = new List<TMP_Text>(64);    // per clue label (pulse hint)
        Coroutine _hintCo;

        bool _dragging;
        int _dragPointerId = int.MinValue;   // sentuhan aktif; sentuhan lain diabaikan (spec)
        int _dc0, _dr0, _dc1, _dr1;
        bool _movedAny;   // spec bag.5: tap = naik di sel jangkar DAN tak pernah pindah sel
        RectTransform _slot;
        RectTransform _dyn;   // sub-canvas: keping, preview, input (lapisan dinamis per spec)

        void Awake() => _rt = (RectTransform)transform;

        /// <summary>Replaces the current level.</summary>
        public void SetLevel(PuzzleSession session)
        {
            Session = session;
            Cols = session.Puzzle.Cols;
            Rows = session.Puzzle.Rows;
            Rebuild();
        }

        public void RebuildIfReady()
        {
            if (Session != null && _slot != null) Rebuild();
        }

        public void Rebuild()
        {
            if (_slot == null) _slot = transform.parent as RectTransform;
            if (Session == null) return;
            StopAllCoroutines();      // mematikan pulse hint dari level sebelumnya
            _hintCo = null;
            ClearChildren(_rt);
            _rectFill.Clear();
            _neutralFill.Clear();
            _clueText.Clear();

            Cell = Mathf.Max(24f, Mathf.Floor(EstimateCell(_slot.rect.width, _slot.rect.height, Cols, Rows)));
            float bw = Cols * Cell, bh = Rows * Cell;
            _rt.sizeDelta = new Vector2(bw, bh);

            // papan = Rounded warna Papan, radius 48 (56/1.17) + bayangan (0,-16)
            var bg = UiFactory.Panel(_rt, "bg", ThemeApplier.ColorOf(ThemeApplier.Role.Board),
                false, AppRoot.Instance != null ? AppRoot.Instance.Rounded : null, 1.17f);
            // padding 28: papan melebar simetris di sekeliling rect kisi
            bg.rectTransform.offsetMin = new Vector2(-Pad, -Pad);
            bg.rectTransform.offsetMax = new Vector2(Pad, Pad);
            UiFactory.ApplyShadow(bg, new Vector2(0f, -16f), ThemeApplier.ColorOf(ThemeApplier.Role.Board));

            // sel grid = tile Rounded warna Permukaan, radius 20 (56/2.8), jarak antar sel 8 px
            var rounded = AppRoot.Instance != null ? AppRoot.Instance.Rounded : null;
            var cellColor = ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty);
            // Fredoka-Bold untuk angka clue & angka luas preview (spesifikasi font)
            var bold = AppRoot.Instance != null ? AppRoot.Instance.FontBold : null;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    var img = UiFactory.Image(_rt, $"cell{r}x{c}", cellColor);
                    if (rounded != null)
                    {
                        img.sprite = rounded;
                        img.type = UnityEngine.UI.Image.Type.Sliced;
                        img.pixelsPerUnitMultiplier = 2.8f;
                    }
                    var crt = img.rectTransform;
                    crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
                    crt.sizeDelta = new Vector2(Cell - Gap, Cell - Gap);
                    crt.anchoredPosition = CellCenter(c, r);
                }

            // Sub-canvas terpisah khusus lapisan yang sering berubah (keping, preview,
            // input). Papan, tile sel, dan angka statis tetap di canvas induk —
            // perubahan keping tidak memicu rebuild seluruh Canvas (spec bag. Canvas).
            // Dibuat tepat di atas tile sel sehingga keping & angka tetap di atasnya.
            var dynGo = new GameObject("Dynamic", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            dynGo.transform.SetParent(_rt, false);
            _dyn = (RectTransform)dynGo.transform;
            _dyn.anchorMin = Vector2.zero; _dyn.anchorMax = Vector2.one;
            _dyn.offsetMin = Vector2.zero; _dyn.offsetMax = Vector2.zero;

            // keping per clue: akar tepi gelap (Rounded radius 32/1.75) + anak
            // "Fill" warna keping (radius 26/2.15, margin 6) — di sub-canvas
            // Dynamic, di atas tile sel
            for (int ci = 0; ci < Session.ClueCount; ci++)
            {
                var go = new GameObject($"fill{ci}", typeof(RectTransform), typeof(Image));
                var img = go.GetComponent<Image>();
                MakePieceRoot(img);
                go.transform.SetParent(_dyn, false);
                MakePieceFill(img);
                img.raycastTarget = false;
                img.gameObject.SetActive(false);
                _rectFill.Add(img);
            }

            // clue labels
            var tc = ThemeApplier.ColorOf(ThemeApplier.Role.Text);
            for (int ci = 0; ci < Session.ClueCount; ci++)
            {
                var clue = Session.Puzzle.Clues[ci];
                var t = UiFactory.Text(_rt, $"clue{ci}", clue.Value.ToString(),
                    Mathf.Clamp(Mathf.RoundToInt(Cell * 0.5f), Mathf.RoundToInt(36f * Ref), Mathf.RoundToInt(72f * Ref)),
                    tc, TMPro.TextAlignmentOptions.Center);
                // angka clue = font Fredoka-Bold (spesifikasi pemakaian font)
                if (bold != null) t.font = bold;
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Cell - 4f, Cell - 4f);
                rt.anchoredPosition = CellCenter(clue.Col, clue.Row);
                _clueText.Add(t);
            }

            // preview drag: dua Image di rect sama — "Fill" Rounded aksen α0,3
            // (radius 32/1.75) dan "Ring" RoundedRing aksen; angka luas TMP Bold
            // 88 warna Teks di tengah
            var pgo = new GameObject("preview", typeof(RectTransform));
            pgo.transform.SetParent(_dyn, false);
            _previewRt = (RectTransform)pgo.transform;
            _pFill = UiFactory.Image(pgo.transform, "Fill", Color.clear);
            MakePieceRoot(_pFill);
            _pRing = UiFactory.Image(pgo.transform, "Ring", Color.clear);
            // cincin 10px tanpa fill center; multiplier 1.75 → tebal ≈ 6 px di layar
            var ring = AppRoot.Instance != null ? AppRoot.Instance.RoundedRing : null;
            if (ring != null)
            {
                _pRing.sprite = ring;
                _pRing.type = Image.Type.Sliced;
                _pRing.fillCenter = false;
                _pRing.pixelsPerUnitMultiplier = 1.75f;
            }
            _pNum = UiFactory.Text(pgo.transform, "Num", "0", 88f, null, TextAlignmentOptions.Center);
            if (bold != null) _pNum.font = bold;
            var nrt = _pNum.rectTransform;
            nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
            nrt.offsetMin = Vector2.zero; nrt.offsetMax = Vector2.zero;
            pgo.SetActive(false);

            // input catcher full board
            var igo = new GameObject("input", typeof(RectTransform), typeof(Image));
            var ii = igo.GetComponent<Image>();
            ii.color = Color.clear;
            ii.raycastTarget = true;
            igo.transform.SetParent(_dyn, false);
            var irt = (RectTransform)igo.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            var proxy = igo.AddComponent<InputProxy>();
            proxy.Owner = this;

            ShowPlaced();
        }

        Vector2 CellCenter(int col, int row) =>
            new Vector2(-(Cols * Cell) / 2f + (col + 0.5f) * Cell,
                         (Rows * Cell) / 2f - (row + 0.5f) * Cell);

        static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        // ------------------------- placed rendering -----------------------------

        public void ShowPlaced()
        {
            for (int ci = 0; ci < Session.ClueCount; ci++)
                if (ci < _rectFill.Count && _rectFill[ci] != null) _rectFill[ci].gameObject.SetActive(false);

            int neutralUsed = 0;
            for (int i = 0; i < Session.Placed.Count; i++)
            {
                var rect = Session.Placed[i];
                int owner = Session.RectOwner(i);
                Image img;
                if (owner >= 0 && owner < _rectFill.Count && _rectFill[owner] != null)
                {
                    // keping valid: akar = tepi keping (gelap), Fill = warna keping
                    var rc = ThemeApplier.ColorOf(ThemeApplier.Role.Rect, Session.Puzzle.Clues[owner].ColorIndex);
                    img = _rectFill[owner];
                    TintPiece(img, ThemeApplier.ShadowOf(rc), rc);
                }
                else
                {
                    img = NeutralFill(neutralUsed++);   // spec 18: rect tak valid tampil netral
                    var gl = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
                    TintPiece(img, new Color(gl.r, gl.g, gl.b, 0.45f), new Color(gl.r, gl.g, gl.b, 0.55f));
                }
                PlaceFillGeometry(img, rect);
                img.gameObject.SetActive(true);
            }
            for (int n = neutralUsed; n < _neutralFill.Count; n++)
                if (_neutralFill[n] != null) _neutralFill[n].gameObject.SetActive(false);
        }

        /// <summary>Anchor/ukuran/posisi keping di dalam kisi (dipakai keping
        /// valid maupun netral).</summary>
        void PlaceFillGeometry(Image img, in RectI rect)
        {
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            // akar keping = gabungan tile sel − 6 px tiap sisi → keping bertetangga
            // berjarak 20 px (8 jarak sel + 6 + 6); pusat gabungan tak bergeser
            img.rectTransform.sizeDelta = new Vector2(PieceSpan(rect.W), PieceSpan(rect.H));
            img.rectTransform.anchoredPosition = new Vector2(
                -(Cols * Cell) / 2f + (rect.X + rect.W * 0.5f) * Cell,
                 (Rows * Cell) / 2f - (rect.Y + rect.H * 0.5f) * Cell);
        }

        // ------------------------------ resep keping ------------------------------

        /// <summary>Ukuran sisi akar keping untuk span sel: gabungan tile sel
        /// (span×Cell − 8) dikurangi PieceInset tiap sisi.</summary>
        float PieceSpan(int span) => span * Cell - Gap - 2f * PieceInset;

        /// <summary>Akar keping: Rounded Sliced radius 32 (56/1.75) — warna
        /// "tepi keping" diisi pemanggil via TintPiece.</summary>
        static void MakePieceRoot(Image img)
        {
            var rounded = AppRoot.Instance != null ? AppRoot.Instance.Rounded : null;
            if (rounded != null)
            {
                img.sprite = rounded;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1.75f;
            }
        }

        /// <summary>Anak "Fill" keping: Rounded Sliced radius 26 (56/2.15) margin 6.</summary>
        static void MakePieceFill(Image root)
        {
            var go = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var inner = go.GetComponent<Image>();
            var rounded = AppRoot.Instance != null ? AppRoot.Instance.Rounded : null;
            if (rounded != null)
            {
                inner.sprite = rounded;
                inner.type = Image.Type.Sliced;
                inner.pixelsPerUnitMultiplier = 2.15f;
            }
            inner.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.SetParent(root.rectTransform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(FrameMargin, FrameMargin);
            rt.offsetMax = new Vector2(-FrameMargin, -FrameMargin);
        }

        /// <summary>Image anak "Fill" dari akar keping (atau null).</summary>
        static Image PieceInner(Component root)
        {
            if (root == null || root.transform.childCount == 0) return null;
            return root.transform.GetChild(0).GetComponent<Image>();
        }

        /// <summary>Warnai pasangan keping: akar (tepi gelap) + Fill (warna keping).</summary>
        static void TintPiece(Image root, Color frame, Color fill)
        {
            root.color = frame;
            var inner = PieceInner(root);
            if (inner != null) inner.color = fill;
        }

        /// <summary>Tampilkan/sembunyikan sarang preview drag.</summary>
        void SetPreviewVisible(bool on)
        {
            if (_previewRt != null) _previewRt.gameObject.SetActive(on);
        }

        /// <summary>Pool fill netral: rect tak valid tidak punya owner clue, jadi
        /// tidak memetakan ke fill per-clue — pakai Image generik yang dibuat
        /// sesuai kebutuhan. Hidup di sub-canvas Dynamic; tile sel & angka tetap
        /// di canvas induk dan tergambar di atasnya.</summary>
        Image NeutralFill(int n)
        {
            if (n < _neutralFill.Count && _neutralFill[n] != null) return _neutralFill[n];
            var go = new GameObject("fillNeutral", typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            // keping netral = struktur keping yang sama (akar + Fill), warna abu
            MakePieceRoot(img);
            MakePieceFill(img);
            img.raycastTarget = false;
            go.transform.SetParent(_dyn, false);
            if (n < _neutralFill.Count) _neutralFill[n] = img; else _neutralFill.Add(img);
            return img;
        }

        /// <summary>Hides the drag preview (called on win overlay open).</summary>
        public void ClearPreview()
        {
            SetPreviewVisible(false);
            _dragging = false;
        }

        public void ApplyTheme()
        {
            Rebuild();
        }

        // ------------------------------- input -----------------------------------

        public void OnPointerDown(PointerEventData e) => Begin(e);

        void Begin(PointerEventData e)
        {
            if (Session == null || Session.IsSolved) return;
            if (_dragging) return;   // spec: sentuhan kedua saat drag diabaikan
            if (!TryCell(e.position, out int c, out int r)) return;
            _dragging = true;
            _dragPointerId = e.pointerId;
            _dc0 = _dc1 = c;
            _dr0 = _dr1 = r;
            _movedAny = false;
            UpdatePreview();
            DragStarted?.Invoke();   // spec umpan balik
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging || e.pointerId != _dragPointerId) return;
            if (!TryCell(e.position, out int c, out int r)
                && !TryCellClamped(e.position, out c, out r)) return;
            if (c != _dc1 || r != _dr1)
            {
                _movedAny |= (c != _dc0 || r != _dr0);
                _dc1 = c; _dr1 = r;
                UpdatePreview();
                DragCellChanged?.Invoke();   // spec umpan balik
            }
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging || e.pointerId != _dragPointerId) return;
            _dragging = false;
            SetPreviewVisible(false);   // tutup preview apa pun hasil jatuhnya
            // spec kasus tepi: pointer di luar kisi → sel terdekat; lepas di luar
            // tetap jatuh pada sel tepi, bukan dibatalkan
            if (!TryCell(e.position, out int c, out int r)
                && !TryCellClamped(e.position, out c, out r)) return;
            // spec: tap tanpa tarikan pada sel terpasang langsung menghapus rect
            // (menggantikan tombol undo); tap pada clue 1 tetap bisa menaruh 1x1.
            // Syarat lengkap bag.5: naik di sel jangkar DAN tak pernah pindah sel —
            // yang sempat keluar sel lalu kembali = tarik (commit 1x1 via Place)
            if (c == _dc0 && r == _dr0 && !_movedAny)
            {
                if (Session.RemoveRectAt(c, r)) return;
                var one = PuzzleSession.Snap(_dc0, _dr0, c, r, Cols, Rows);
                // ketuk di sel kosong: hanya clue bernilai 1 yang menaruh 1x1,
                // sel kosong lain tidak melakukan apa-apa (spec 18 Tap)
                if (Session.RectValid(one)) Session.Place(one);
                return;
            }
            var rect = PuzzleSession.Snap(_dc0, _dr0, c, r, Cols, Rows);
            // spec 18.3: setiap jatuh MENJADI rect — valid (berwarna) atau netral
            Session.Place(rect);
        }

        /// <summary>Sentuhan tanpa tarikan diteruskan ke OnEndDrag: hapus rect pada
        /// sel itu, atau coba letakkan persegi 1x1 (clue bernilai 1).</summary>
        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragging || e.pointerId != _dragPointerId) return;
            OnEndDrag(e);
        }

        /// <summary>EventSystem bawaan Unity TIDAK punya event pointer-cancel —
        /// deteksi manual per frame: drag "hantu" (pointer sudah lepas dibatalkan
        /// OS di luar event) langsung dibatalkan. Polling via Input System API
        /// (proyek memakai activeInputHandler baru; UnityEngine.Input bakal lempar).
        /// Touch ditulis beda namespace karena UnityEngine juga punya struct Touch.</summary>
        void Update()
        {
            if (!_dragging) return;
            bool mouseDown = Mouse.current != null && Mouse.current.leftButton.isPressed;
            Touchscreen ts = Touchscreen.current;
            bool touchDown = false;
            if (ts != null)
            {
                var phase = ts.primaryTouch.phase.ReadValue();
                touchDown = phase == TouchPhase.Began || phase == TouchPhase.Moved
                            || phase == TouchPhase.Stationary;
            }
            if (!mouseDown && !touchDown) CancelDrag();
        }

        void CancelDrag()
        {
            _dragging = false;
            SetPreviewVisible(false);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            // spec kasus tepi: tab kehilangan fokus saat drag → batalkan preview
            if (!hasFocus && _dragging) CancelDrag();
        }

        void OnDisable() => CancelDrag();   // jaring pengaman: nonaktif/destroy di tengah drag

        bool TryCell(Vector2 screenPos, out int col, out int row)
        {
            col = row = -1;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screenPos, null, out var local))
                return false;
            float halfW = Cols * Cell * 0.5f, halfH = Rows * Cell * 0.5f;
            float pad = Cell * 0.4f;   // spec: hit area longgar untuk jari
            if (local.x < -halfW - pad || local.x > halfW + pad
                || local.y < -halfH - pad || local.y > halfH + pad) return false;
            float u = local.x + halfW, v = halfH - local.y;
            col = Mathf.Clamp(Mathf.FloorToInt(u / Cell), 0, Cols - 1);
            row = Mathf.Clamp(Mathf.FloorToInt(v / Cell), 0, Rows - 1);
            return true;
        }

        /// <summary>Spec kasus tepi: pointer di luar kisi → sel terdekat di dalam
        /// kisi. Tanpa radius pad; jarak apa pun menempel ke tepi papan.</summary>
        bool TryCellClamped(Vector2 screenPos, out int col, out int row)
        {
            col = row = 0;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screenPos, null, out var local))
                return false;
            float halfW = Cols * Cell * 0.5f, halfH = Rows * Cell * 0.5f;
            float u = Mathf.Clamp(local.x + halfW, 0f, Cols * Cell - 0.01f);
            float v = Mathf.Clamp(halfH - local.y, 0f, Rows * Cell - 0.01f);
            col = Mathf.Min(Cols - 1, Mathf.FloorToInt(u / Cell));
            row = Mathf.Min(Rows - 1, Mathf.FloorToInt(v / Cell));
            return true;
        }

        void UpdatePreview()
        {
            if (Session == null || _previewRt == null) return;
            var rect = PuzzleSession.Snap(_dc0, _dr0, _dc1, _dr1, Cols, Rows);
            // spec: preview selalu aksen — Fill α0,3 + Ring α1 di rect sama;
            // angka luas (W×H) di tengah. Validitas tak mengubah warna: hasil
            // lepas tetap menjadi keping (valid berwarna / tak valid netral)
            var accent = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
            var fillC = accent; fillC.a = 0.3f;
            _pFill.color = fillC;
            _pRing.color = accent;
            _pNum.color = ThemeApplier.ColorOf(ThemeApplier.Role.Text);
            _pNum.text = (rect.W * rect.H).ToString();
            _previewRt.transform.SetAsLastSibling();   // fill netral belakangan jangan menutupi preview
            SetPreviewVisible(true);
            FitPreview(rect);
        }

        void FitPreview(in RectI rect)
        {
            _previewRt.anchorMin = _previewRt.anchorMax = new Vector2(0.5f, 0.5f);
            // preview mengikuti akar keping: gabungan tile sel − 6 px tiap sisi
            _previewRt.sizeDelta = new Vector2(PieceSpan(rect.W), PieceSpan(rect.H));
            _previewRt.anchoredPosition = new Vector2(
                -(Cols * Cell) / 2f + (rect.X + rect.W * 0.5f) * Cell,
                 (Rows * Cell) / 2f - (rect.Y + rect.H * 0.5f) * Cell);
        }

        /// <summary>Spec hint bertingkat tahap 1: kedipkan angka clue (warna aksen)
        /// 2× — termasuk rect miliknya bila ada (posisi salah) — kemudian `apply`
        /// menempatkan rect solusinya (tahap 2). Hitungan hint sudah dibukukan pada
        /// pemanggilan tahap 1 (TryGetHintTarget).</summary>
        public void HintPulse(int clueIndex, System.Action apply)
        {
            if (_hintCo != null) StopCoroutine(_hintCo);
            _hintCo = StartCoroutine(HintPulseCo(clueIndex, apply));
        }

        System.Collections.IEnumerator HintPulseCo(int clueIndex, System.Action apply)
        {
            var label = clueIndex < _clueText.Count ? _clueText[clueIndex] : null;
            var accent = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
            var labelText = label != null ? label.color : accent;
            // spec: bila target punya rect (posisi salah), rect itu ikut disorot —
            // akar + Fill keping kedip aksen bersama, pulih otomatis di akhir loop
            Image root = clueIndex < _rectFill.Count
                         && _rectFill[clueIndex] != null && _rectFill[clueIndex].gameObject.activeSelf
                ? _rectFill[clueIndex] : null;
            var inner = PieceInner(root);
            var frameNormal = root != null ? root.color : Color.clear;
            var innerNormal = inner != null ? inner.color : Color.clear;
            for (int i = 0; i < 4; i++)   // on-off-on-off = 2 kedip ≈ 0.88 s
            {
                bool hot = i % 2 == 0;
                if (label != null) label.color = hot ? accent : labelText;
                if (root != null)
                {
                    root.color = hot ? accent : frameNormal;
                    if (inner != null) inner.color = hot ? accent : innerNormal;
                }
                yield return new WaitForSecondsRealtime(0.22f);
            }
            _hintCo = null;
            apply?.Invoke();
        }

        /// <summary>Sits on the invisible full-board input image and forwards events.</summary>
        class InputProxy : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
        {
            public BoardView Owner;
            public void OnPointerDown(PointerEventData e) => Owner.OnPointerDown(e);
            public void OnDrag(PointerEventData e) => Owner.OnDrag(e);
            public void OnEndDrag(PointerEventData e) => Owner.OnEndDrag(e);
            public void OnPointerUp(PointerEventData e) => Owner.OnPointerUp(e);
        }
    }
}
