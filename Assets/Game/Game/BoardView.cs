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
    /// Renders a puzzle board (background, grid lines, clue labels, placed-rect fills)
    /// and handles pointer → cell input with a grid-snapped preview. The whole
    /// board rebuilds on level start and on screen resize; puzzles are small enough
    /// (max 14x8 cells) that rebuilds are effectively free.
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

        /// <summary>Single source for cell sizing (spec margins + cap); shared with the compact-panel check.</summary>
        public static float EstimateCell(float slotW, float slotH, int cols, int rows) =>
            Mathf.Min(MaxCell, Mathf.Min((slotW - Margin * 2f) / cols, (slotH - Margin * 2f) / rows));

        RectTransform _rt;
        Image _preview;
        List<Image> _rectFill = new List<Image>(64);          // per clue fill image (valid)
        List<Image> _neutralFill = new List<Image>(64);       // fill rect netral (spec 18)
        List<TMP_Text> _clueText = new List<TMP_Text>(64);    // per clue label (pulse hint)
        Coroutine _hintCo;

        bool _dragging;
        int _dragPointerId = int.MinValue;   // sentuhan aktif; sentuhan lain diabaikan (spec)
        int _dc0, _dr0, _dc1, _dr1;
        RectTransform _slot;

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

            // background and cell area
            var bg = UiFactory.Panel(_rt, "bg", ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty));

            // rect fills (one child per clue; enabled state mirrors Placed)
            for (int ci = 0; ci < Session.ClueCount; ci++)
            {
                var go = new GameObject($"fill{ci}", typeof(RectTransform), typeof(Image));
                var img = go.GetComponent<Image>();
                go.transform.SetParent(_rt, false);
                img.raycastTarget = false;
                img.enabled = false;
                _rectFill.Add(img);
            }

            // grid lines
            var lineColor = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
            for (int c = 0; c <= Cols; c++)
                Line($"v{c}", lineColor, new Vector2(2f * Ref, bh), new Vector2(-bw / 2f + c * Cell, 0f));
            for (int r = 0; r <= Rows; r++)
                Line($"h{r}", lineColor, new Vector2(bw, 2f * Ref), new Vector2(0f, -bh / 2f + r * Cell));

            // clue labels
            var tc = ThemeApplier.ColorOf(ThemeApplier.Role.Text);
            for (int ci = 0; ci < Session.ClueCount; ci++)
            {
                var clue = Session.Puzzle.Clues[ci];
                var t = UiFactory.Text(_rt, $"clue{ci}", clue.Value.ToString(),
                    Mathf.Clamp(Mathf.RoundToInt(Cell * 0.5f), Mathf.RoundToInt(36f * Ref), Mathf.RoundToInt(72f * Ref)),
                    tc, TMPro.TextAlignmentOptions.Center);
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Cell - 4f, Cell - 4f);
                rt.anchoredPosition = CellCenter(clue.Col, clue.Row);
                _clueText.Add(t);
            }

            // preview rect (on top of fills, below nothing yet)
            var pgo = new GameObject("preview", typeof(RectTransform), typeof(Image));
            _preview = pgo.GetComponent<Image>();
            pgo.transform.SetParent(_rt, false);
            _preview.raycastTarget = false;
            _preview.enabled = false;

            // input catcher full board
            var igo = new GameObject("input", typeof(RectTransform), typeof(Image));
            var ii = igo.GetComponent<Image>();
            ii.color = Color.clear;
            ii.raycastTarget = true;
            igo.transform.SetParent(_rt, false);
            var irt = (RectTransform)igo.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            var proxy = igo.AddComponent<InputProxy>();
            proxy.Owner = this;

            ShowPlaced();
        }

        void Line(string name, Color c, Vector2 size, Vector2 pos)
        {
            var img = UiFactory.Image(_rt, name, c);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
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
                if (ci < _rectFill.Count && _rectFill[ci] != null) _rectFill[ci].enabled = false;

            int neutralUsed = 0;
            for (int i = 0; i < Session.Placed.Count; i++)
            {
                var rect = Session.Placed[i];
                int owner = Session.RectOwner(i);
                Image img;
                if (owner >= 0 && owner < _rectFill.Count && _rectFill[owner] != null)
                {
                    var rc = ThemeApplier.ColorOf(ThemeApplier.Role.Rect, Session.Puzzle.Clues[owner].ColorIndex);
                    img = _rectFill[owner];
                    img.color = new Color(rc.r, rc.g, rc.b, 0.92f);
                }
                else
                {
                    img = NeutralFill(neutralUsed++);   // spec 18: rect tak valid tampil netral
                    var c = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
                    c.a = 0.55f;
                    img.color = c;
                }
                PlaceFillGeometry(img, rect);
                img.enabled = true;
            }
            for (int n = neutralUsed; n < _neutralFill.Count; n++)
                if (_neutralFill[n] != null) _neutralFill[n].enabled = false;
        }

        /// <summary>Anchor/ukuran/posisi fill persegi di dalam kisi (dipakai fill
        /// per-clue maupun fill netral).</summary>
        void PlaceFillGeometry(Image img, in RectI rect)
        {
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.sizeDelta = new Vector2(rect.W * Cell - 2f, rect.H * Cell - 2f);
            img.rectTransform.anchoredPosition = new Vector2(
                -(Cols * Cell) / 2f + (rect.X + rect.W * 0.5f) * Cell,
                 (Rows * Cell) / 2f - (rect.Y + rect.H * 0.5f) * Cell);
        }

        /// <summary>Pool fill netral: rect tak valid tidak punya owner clue, jadi
        /// tidak memetakan ke fill per-clue — pakai Image generik yang dibuat
        /// sesuai kebutuhan. Sibling index 1 = tepat di atas bg (angka &amp; garis
        /// kisi tetap terbaca di atasnya).</summary>
        Image NeutralFill(int n)
        {
            if (n < _neutralFill.Count && _neutralFill[n] != null) return _neutralFill[n];
            var go = new GameObject("fillNeutral", typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            go.transform.SetParent(_rt, false);
            go.transform.SetSiblingIndex(1);   // bg selalu index 0 (dibuat pertama di Rebuild)
            if (n < _neutralFill.Count) _neutralFill[n] = img; else _neutralFill.Add(img);
            return img;
        }

        /// <summary>Hides the drag preview (called on win overlay open).</summary>
        public void ClearPreview()
        {
            if (_preview != null) _preview.enabled = false;
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
                _dc1 = c; _dr1 = r;
                UpdatePreview();
                DragCellChanged?.Invoke();   // spec umpan balik
            }
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging || e.pointerId != _dragPointerId) return;
            _dragging = false;
            if (_preview != null) _preview.enabled = false;   // tutup preview apa pun hasil jatuhnya
            // spec kasus tepi: pointer di luar kisi → sel terdekat; lepas di luar
            // tetap jatuh pada sel tepi, bukan dibatalkan
            if (!TryCell(e.position, out int c, out int r)
                && !TryCellClamped(e.position, out c, out r)) return;
            // spec: tap tanpa tarikan pada sel terpasang langsung menghapus rect
            // (menggantikan tombol undo); tap pada clue 1 tetap bisa menaruh 1x1
            if (c == _dc0 && r == _dr0)
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
            if (_preview != null) _preview.enabled = false;
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
            if (Session == null || _preview == null) return;
            var rect = PuzzleSession.Snap(_dc0, _dr0, _dc1, _dr1, Cols, Rows);
            bool ok = Session.RectValid(rect);
            // spec: rect valid berwarna; tidak valid TAMPIL NETRAL — drop tetap dibuat
            if (ok) _preview.color = new Color(0.30f, 0.85f, 0.35f, 0.5f);
            else
            {
                var c = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
                c.a = 0.4f;
                _preview.color = c;
            }
            _preview.enabled = true;
            FitPreview(rect);
        }

        void FitPreview(in RectI rect)
        {
            _preview.rectTransform.anchorMin = _preview.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _preview.rectTransform.sizeDelta = new Vector2(rect.W * Cell - 3f, rect.H * Cell - 3f);
            _preview.rectTransform.anchoredPosition = new Vector2(
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
            // spec: bila target punya rect (posisi salah), rect itu ikut disorot
            Image fillImg = clueIndex < _rectFill.Count
                            && _rectFill[clueIndex] != null && _rectFill[clueIndex].enabled
                ? _rectFill[clueIndex] : null;
            var fillNormal = fillImg != null ? fillImg.color : Color.clear;
            for (int i = 0; i < 4; i++)   // on-off-on-off = 2 kedip ≈ 0.88 s
            {
                bool hot = i % 2 == 0;
                if (label != null) label.color = hot ? accent : labelText;
                if (fillImg != null)
                {
                    var c = hot ? ThemeApplier.ColorOf(ThemeApplier.Role.Accent) : fillNormal;
                    c.a = 0.92f;
                    fillImg.color = c;
                }
                yield return new WaitForSecondsRealtime(0.22f);
            }
            if (fillImg != null) fillImg.color = fillNormal;
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
