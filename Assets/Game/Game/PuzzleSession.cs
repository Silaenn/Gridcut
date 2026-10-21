using System;
using System.Collections.Generic;
using GridCut.Core;

namespace GridCut.Game
{
    /// <summary>
    /// Mutable runtime state of one puzzle at play time. The BoardView renders and
    /// feeds pointer results; session owns the rules (spec 18 "Detail mekanik" —
    /// menimpa bagian 3–5 bila konflik):
    /// - state pemain = daftar Rect non-tumpang tindih (Placed + PlacedOwners);
    /// - rect TIDAK valid (0/2 clue atau luas salah) tetap boleh dibuat — tampil
    ///   netral, tanpa hukuman; rect valid: tepat satu clue + luas = nilai;
    /// - menang = semua sel tertutup dan semua rect valid, dicek setiap rect
    ///   ditambah/dihapus; kesamaan dengan solusi tersimpan TIDAK dijadikan syarat;
    /// - spec MVP: tap pada rect terpasang menghapusnya (menggantikan tombol undo),
    ///   tap clue bernilai 1 di sel kosong menaruh 1x1, sel kosong lain diam;
    /// - hint bertingkat: menyorot angka clue, lalu menempatkan rect solusinya.
    /// </summary>
    public class PuzzleSession
    {
        public readonly LevelData Level;
        public readonly Puzzle Puzzle;
        public readonly DateTime StartedAtUtc = DateTime.UtcNow;

        public readonly List<RectI> Placed = new List<RectI>();
        readonly List<int> PlacedOwners = new List<int>();   // paralel dengan Placed; -1 = rect netral
        readonly bool[] _placedClue;                         // clue index -> sudah tertutup rect valid?

        public event Action<int, RectI> RectPlaced;   // owner clue (atau -1 utk rect netral), rect
        public event Action<int, RectI> RectRemoved;  // owner clue (atau -1), rect
        public event Action Invalid;                  // rejected input
        public event Action<int> HintUsed;            // clueIndex
        public event Action<bool> SolvedChanged;      // true at solve time

        public int ClueCount => Puzzle.ClueCount;
        public int PlacedCount { get; private set; }
        public const int HintLimit = 3;      // spec: Hint button shows remaining (e.g. 2/3)
        public int HintCount { get; private set; }
        public int HintsLeft => HintLimit - HintCount;
        /// <summary>Spec cek menang: SEMUA sel tertutup dan SEMUA rect valid — dicek
        /// tiap rect ditambah (penghapusan tak bisa membuat menang). Rect valid
        /// berpasangan bijektif dengan clue (keduanya memuat sel angka ⇒ tak mungkin
        /// dua rect valid memakai clue sama tanpa tumpang tindih), dan jumlah luas
        /// semua clue = luas papan ⇒ menutup penuh otomatis. Kesamaan dengan solusi
        /// tersimpan TIDAK dicek.</summary>
        public bool IsSolved
        {
            get
            {
                if (Placed.Count != ClueCount) return false;   // rect netral menahan sampai dihapus
                for (int i = 0; i < PlacedOwners.Count; i++)
                    if (PlacedOwners[i] < 0) return false;
                return true;
            }
        }

        PuzzleSession(LevelData level, Puzzle puzzle)
        {
            Level = level;
            Puzzle = puzzle;
            _placedClue = new bool[puzzle.ClueCount];
        }

        public static PuzzleSession Make(LevelData level)
        {
            var clues = new Clue[level.Clues.Length];
            for (int i = 0; i < level.Clues.Length; i++) clues[i] = level.Clues[i];
            return new PuzzleSession(level, new Puzzle(level.Cols, level.Rows, clues));
        }

        /// <summary>Owner (clue index) rect ke-`i`; -1 bila rect netral.</summary>
        public int RectOwner(int index) => PlacedOwners[index];

        /// <summary>Snaps a preview drag between two board cells, inclusive of both endpoints.</summary>
        public static RectI Snap(int c0, int r0, int c1, int r1, int cols, int rows)
        {
            if (c1 < 0) c1 = 0; else if (c1 >= cols) c1 = cols - 1;
            if (r1 < 0) r1 = 0; else if (r1 >= rows) r1 = rows - 1;
            int x0 = Math.Min(c0, c1), x1 = Math.Max(c0, c1);
            int y0 = Math.Min(r0, r1), y1 = Math.Max(r0, r1);
            return new RectI(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        /// <summary>Validitas satu rect (spec 18): dalam batas kisi, memuat tepat
        /// satu clue, dan luasnya sama dengan nilai clue itu. Rect tak valid tetap
        /// BOLEH dibuat — hanya tampil netral (preview memakai ini untuk warna).</summary>
        public bool RectValid(in RectI rect)
        {
            if (rect.W <= 0 || rect.H <= 0) return false;
            if (rect.X < 0 || rect.Y < 0 || rect.Right > Puzzle.Cols || rect.Bottom > Puzzle.Rows) return false;
            int found = -1;
            for (int row = rect.Y; row < rect.Bottom; row++)
                for (int col = rect.X; col < rect.Right; col++)
                {
                    int c = Puzzle.ClueIndexAt(col, row);
                    if (c == -1) continue;
                    if (found >= 0 && found != c) return false;   // dua angka dalam satu rect
                    found = c;
                }
            return found >= 0 && rect.Area == Puzzle.Clues[found].Value;
        }

        public void Place(in RectI rect)
        {
            if (IsSolved) return;
            if (rect.W <= 0 || rect.H <= 0 || rect.X < 0 || rect.Y < 0
                || rect.Right > Puzzle.Cols || rect.Bottom > Puzzle.Rows)
            {
                Invalid?.Invoke();   // defensive; TryCell sudah clamp
                return;
            }
            int ci = UniqueAnchor(rect);
            // spec 18.3: rect yang tumpang tindih dengan rect baru dihapus otomatis
            for (int i = Placed.Count - 1; i >= 0; i--)
                if (Placed[i].Overlaps(rect))
                    RemovePlacedAt(i);
            var r = rect;
            int owner = ci >= 0 && r.Area == Puzzle.Clues[ci].Value ? ci : -1;
            Placed.Add(r);
            PlacedOwners.Add(owner);
            if (owner >= 0)
            {
                _placedClue[owner] = true;
                PlacedCount++;
            }
            RectPlaced?.Invoke(owner, r);
            if (IsSolved) SolvedChanged?.Invoke(true);
        }

        /// <summary>-1 = tidak memuat clue; -2 = memuat lebih dari satu clue.</summary>
        int UniqueAnchor(in RectI rect)
        {
            int found = -1;
            for (int row = rect.Y; row < rect.Bottom; row++)
                for (int col = rect.X; col < rect.Right; col++)
                {
                    int c = Puzzle.ClueIndexAt(col, row);
                    if (c == -1) continue;
                    if (found >= 0 && found != c) return -2;
                    found = c;
                }
            return found;
        }

        public void Reset()
        {
            if (Placed.Count == 0) return;
            for (int i = Placed.Count - 1; i >= 0; i--)
                RemovePlacedAt(i);
        }

        /// <summary>Spec: tap pada persegi terpasang menghapusnya (sel apa pun dalam rect).</summary>
        public bool RemoveRectAt(int col, int row)
        {
            for (int i = Placed.Count - 1; i >= 0; i--)
                if (Placed[i].Contains(col, row))
                {
                    RemovePlacedAt(i);
                    return true;
                }
            return false;
        }

        void RemovePlacedAt(int index)
        {
            var r = Placed[index];
            int owner = PlacedOwners[index];
            Placed.RemoveAt(index);
            PlacedOwners.RemoveAt(index);
            if (owner >= 0)
            {
                _placedClue[owner] = false;
                PlacedCount--;
            }
            RectRemoved?.Invoke(owner, r);
            _ = IsSolved;   // spec: cek menang juga dijalankan tiap penghapusan (tak bisa jadi menang)
        }

        /// <summary>
        /// Spec hint: target = clue pertama yang belum tertutup rect SOLUSINYA —
        /// belum ditempatkan ATAU ditempatkan salah. false bila tak ada lagi.
        /// </summary>
        public bool TryGetHintTarget(out int clueIndex, out RectI rect)
        {
            clueIndex = -1;
            rect = default;
            if (IsSolved || HintCount >= HintLimit) return false;
            for (int c = 0; c < ClueCount; c++)
            {
                if (_placedClue[c])
                {
                    var placed = RectOfClue(c);
                    if (placed.HasValue && SameAs(placed.Value, Level.Solution[c])) continue;
                }
                clueIndex = c;
                rect = Level.Solution[c];
                return true;
            }
            return false;
        }

        /// <summary>Menempatkan solusi clue `clueIndex` (rect miliknya yang salah ikut
        /// terhapus via penimpaan). Dipanggil BoardView SETELAH animasi sorot sesuai
        /// spec hint bertingkat.</summary>
        public void ApplyHint(int clueIndex)
        {
            if (IsSolved || HintCount >= HintLimit) return;
            if (clueIndex < 0 || clueIndex >= ClueCount) return;
            HintCount++;
            Place(Level.Solution[clueIndex]);
            if (_placedClue[clueIndex]) HintUsed?.Invoke(clueIndex);
        }

        RectI? RectOfClue(int ci)
        {
            for (int i = 0; i < PlacedOwners.Count; i++)
                if (PlacedOwners[i] == ci) return Placed[i];
            return null;
        }

        static bool SameAs(in RectI a, in RectI b) => a.X == b.X && a.Y == b.Y && a.W == b.W && a.H == b.H;

        /// <summary>Percentage of clues placed, for the progress readout.</summary>
        public float Progress => ClueCount == 0 ? 1f : PlacedCount / (float)ClueCount;
    }
}
