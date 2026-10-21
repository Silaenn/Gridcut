using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using GridCut.Core;

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

        // spec sizes use 1280-wide reference units; ×1.35 matches the app-wide rebake
        public const float Ref = 1.35f;
        const float MaxCell = 150f * Ref;   // spec: cells never exceed 150 reference
        const float Margin = 48f * Ref;     // spec: 48 reference inset around the grid area

        /// <summary>Single source for cell sizing (spec margins + cap); shared with the compact-panel check.</summary>
        public static float EstimateCell(float slotW, float slotH, int cols, int rows) =>
            Mathf.Min(MaxCell, Mathf.Min((slotW - Margin * 2f) / cols, (slotH - Margin * 2f) / rows));

        RectTransform _rt;
        Image _preview;
        List<Image> _rectFill = new List<Image>(64);          // per clue fill image

        bool _dragging;
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
            ClearChildren(_rt);
            _rectFill.Clear();

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
            {
                if (ci >= _rectFill.Count || _rectFill[ci] == null) continue;
                _rectFill[ci].enabled = false;
            }
            foreach (var rect in Session.Placed)
            {
                int owner = -1;
                for (int row = rect.Y; row < rect.Bottom && owner == -1; row++)
                    for (int col = rect.X; col < rect.Right && owner == -1; col++)
                        owner = Session.PlacedClueAt(col, row);
                if (owner < 0 || owner >= _rectFill.Count) continue;

                var img = _rectFill[owner];
                var rc = ThemeApplier.ColorOf(ThemeApplier.Role.Rect, Session.Puzzle.Clues[owner].ColorIndex);
                img.color = new Color(rc.r, rc.g, rc.b, 0.92f);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                img.rectTransform.sizeDelta = new Vector2(rect.W * Cell - 2f, rect.H * Cell - 2f);
                img.rectTransform.anchoredPosition = new Vector2(
                    -(Cols * Cell) / 2f + (rect.X + rect.W * 0.5f) * Cell,
                     (Rows * Cell) / 2f - (rect.Y + rect.H * 0.5f) * Cell);
                img.enabled = true;
            }
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
            if (!TryCell(e.position, out int c, out int r)) return;
            _dragging = true;
            _dc0 = _dc1 = c;
            _dr0 = _dr1 = r;
            UpdatePreview();
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            if (!TryCell(e.position, out int c, out int r)) return;
            if (c != _dc1 || r != _dr1)
            {
                _dc1 = c; _dr1 = r;
                UpdatePreview();
            }
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_preview != null) _preview.enabled = false;   // tutup preview apa pun hasil jatuhnya
            if (!TryCell(e.position, out int c, out int r)) return;
            // spec: tap tanpa tarikan pada sel terpasang langsung menghapus rect
            // (menggantikan tombol undo); tap pada clue 1 tetap bisa menaruh 1x1
            if (c == _dc0 && r == _dr0)
            {
                if (Session.RemoveRectAt(c, r)) return;
                var one = PuzzleSession.Snap(_dc0, _dr0, c, r, Cols, Rows);
                if (Session.CouldPlace(one)) Session.Place(one);
                return;
            }
            var rect = PuzzleSession.Snap(_dc0, _dr0, c, r, Cols, Rows);
            Session.Place(rect);   // overwrite-aware: rect yang beririsan ikut dihapus
        }

        /// <summary>Sentuhan tanpa tarikan diteruskan ke OnEndDrag: hapus rect pada
        /// sel itu, atau coba letakkan persegi 1x1 (clue bernilai 1).</summary>
        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragging) return;
            OnEndDrag(e);
        }

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

        void UpdatePreview()
        {
            if (Session == null || _preview == null) return;
            var rect = PuzzleSession.Snap(_dc0, _dr0, _dc1, _dr1, Cols, Rows);
            bool ok = Session.CouldPlace(rect);
            // spec: rect valid berwarna; tidak valid NETRAL tanpa hukuman
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
