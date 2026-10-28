using System.Collections.Generic;

namespace GridCut.Core
{
    /// <summary>Why a candidate puzzle was rejected during generation.</summary>
    public enum RejectReason : byte
    {
        None = 0,
        NotUnique,          // backtracking found 2+ solutions (or zero)
        SolverStuck,        // deduction solver cannot finish it: player would have to guess
        TooManyOnes,        // more than 15% of the rects are 1x1
        TooFewBig,          // (8x6 and up) fewer than 30% of rects have area >= 4
        Liney,              // >=70% of rects span the full width or height
        OutOfScoreRange,    // difficulty score outside the accepted band (export-time only)
        Duplicate,          // canonical hash already seen (rotation/mirror of another level)
        TooFewClues,        // too few rectangles: level risks one obvious block
        TooManyClues        // too many tiny rectangles: level becomes cleanup work
    }

    /// <summary>Result of one generation attempt for one seed.</summary>
    public sealed class GenAttempt
    {
        public bool Ok;
        public RejectReason RejectedAs;
        public Puzzle Puzzle;
        public List<RectI> Solution;     // solution rectangles, same order as Clues
        public float Score;
        public long DeductionWeightSum;
        public int DeductionMaxWeight;
        public ulong Seed;
    }

    /// <summary>
    /// Puzzle generation pipeline: random rectangle partition -> clue placement ->
    /// uniqueness and deduction checks -> pattern filters.
    /// Deterministic: identical seed + parameters give identical output.
    /// </summary>
    public static class PuzzleGenerator
    {
        public const int PaletteColors = 6;

        /// <summary>Optional generation limits. Zero means unlimited.</summary>
        public struct GenRules
        {
            /// <summary>Reject candidate rectangles larger than this area. Keeps
            /// early boards from being dominated by one obvious 15/20 block.</summary>
            public int MaxRectArea;
        }

        public static GenAttempt Generate(int cols, int rows, ulong seed) =>
            Generate(cols, rows, seed, default);

        public static GenAttempt Generate(int cols, int rows, ulong seed, GenRules rules)
        {
            var result = new GenAttempt { RejectedAs = RejectReason.None, Seed = seed };
            var rng = new Prng(seed);

            // 1. random partition
            var rects = Partition(cols, rows, rng, rules);

            // 2. clue: one per rect, value = area, placed on a random cell of the rect
            var clues = new Clue[rects.Count];
            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                int col = r.X + rng.NextInt(r.W);
                int row = r.Y + rng.NextInt(r.H);
                clues[i] = new Clue(col, row, r.Area, 0);
            }

            // 3. checks
            var puzzle = new Puzzle(cols, rows, clues);
            result.Puzzle = puzzle;

            int solutionCount = BacktrackSolver.CountSolutions(puzzle, 2);
            if (solutionCount != 1) return Reject(result, RejectReason.NotUnique);

            var solve = new DeductionSolver().Solve(puzzle);
            result.Score = solve.Score;
            result.DeductionWeightSum = solve.WeightSum;
            result.DeductionMaxWeight = solve.MaxWeight;

            if (solve.Status != SolveResult.Solved) return Reject(result, RejectReason.SolverStuck);
            result.Solution = solve.Selection;

            // 4. pattern filters
            RejectReason reason = ApplyFilters(rects, cols, rows);
            if (reason != RejectReason.None) return Reject(result, reason);

            result.Ok = true;
            return result;
        }

        private static GenAttempt Reject(GenAttempt result, RejectReason reason)
        {
            result.Ok = false;
            result.RejectedAs = reason;
            return result;
        }

        private static RejectReason ApplyFilters(List<RectI> rects, int cols, int rows)
        {
            int total = rects.Count;
            if (total == 0) return RejectReason.NotUnique;

            int ones = 0, spans = 0, big = 0;
            for (int i = 0; i < total; i++)
            {
                var r = rects[i];
                if (r.Area == 1) ones++;
                if (r.W == cols || r.H == rows) spans++;
                if (r.Area >= 4) big++;
            }

            if (ones > total * 15 / 100) return RejectReason.TooManyOnes;
            if (spans * 10 >= total * 7) return RejectReason.Liney;      // >= 70%
            if (cols * rows >= 48 && big * 10 < total * 3)
                return RejectReason.TooFewBig;                            // < 30% with area>=4 (8x6 and up)
            return RejectReason.None;
        }

        /// <summary>
        /// Canonical fingerprint over the clue layout, invariant under the 8
        /// rotations/reflections (rotations outside squares are skipped: they change
        /// the grid shape, which never matches another generated size anyway).
        /// </summary>
        public static ulong CanonicalHash(Puzzle puzzle)
        {
            ulong best = ulong.MaxValue;
            for (int t = 0; t < 8; t++)
            {
                if (!TransformAllowed(t, puzzle.Cols, puzzle.Rows)) continue;
                best = System.Math.Min(best, HashUnderTransform(puzzle, t));
            }
            return best;
        }

        private static bool TransformAllowed(int t, int cols, int rows)
        {
            // 0: identity, 1: rotate90, 2: rotate180, 3: rotate270,
            // 4: mirrorH, 5: mirrorV, 6: transpose, 7: anti-transpose
            bool square = cols == rows;
            if (!square && (t == 1 || t == 3 || t == 6 || t == 7)) return false;
            return true;
        }

        private static void Transform(int t, int col, int row, int cols, int rows, out int c, out int r)
        {
            switch (t)
            {
                case 1: c = cols - 1 - row; r = col; break;              // rotate 90 (square)
                case 2: c = cols - 1 - col; r = rows - 1 - row; break;   // rotate 180
                case 3: c = row; r = rows - 1 - col; break;              // rotate 270 (square)
                case 4: c = cols - 1 - col; r = row; break;              // mirror horizontally
                case 5: c = col; r = rows - 1 - row; break;              // mirror vertically
                case 6: c = row; r = col; break;                          // transpose (square)
                case 7: c = rows - 1 - row; r = cols - 1 - col; break;    // anti-transpose (square)
                default: c = col; r = row; break;
            }
        }

        private static ulong HashUnderTransform(Puzzle puzzle, int t)
        {
            int cols = t == 1 || t == 3 || t == 6 || t == 7 ? puzzle.Rows : puzzle.Cols;
            int rows = t == 1 || t == 3 || t == 6 || t == 7 ? puzzle.Cols : puzzle.Rows;

            int maxClue = 0;
            for (int i = 0; i < puzzle.Clues.Length; i++) if (puzzle.Clues[i].Value > maxClue) maxClue = puzzle.Clues[i].Value;

            ulong hash = 14695981039346656037UL;
            var ordered = new List<int>(puzzle.Clues.Length);
            for (int i = 0; i < puzzle.Clues.Length; i++) ordered.Add(i);
            ordered.Sort((a, b) =>
            {
                Transform(t, puzzle.Clues[a].Col, puzzle.Clues[a].Row, puzzle.Cols, puzzle.Rows, out int ca, out int ra);
                Transform(t, puzzle.Clues[b].Col, puzzle.Clues[b].Row, puzzle.Cols, puzzle.Rows, out int cb, out int rb);
                int cmp = ra.CompareTo(rb);
                return cmp != 0 ? cmp : ca.CompareTo(cb);
            });

            foreach (int i in ordered)
            {
                Transform(t, puzzle.Clues[i].Col, puzzle.Clues[i].Row, puzzle.Cols, puzzle.Rows, out int c, out int r);
                hash = Mix(hash, (ulong)(r * cols + c));
                hash = Mix(hash, (ulong)(puzzle.Clues[i].Value + 1));
            }
            return hash;
        }

        private static ulong Mix(ulong h, ulong v)
        {
            h ^= v + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
            return h;
        }

        // ------------------------------------------------------------------
        // Random partition of the full grid into rectangles.
        // Anchor = topmost-then-leftmost uncovered cell; grow a rect from it with
        // quadratic small-biased sampling; guaranteed to cover the whole grid.
        // ------------------------------------------------------------------

        private static List<RectI> Partition(int cols, int rows, Prng rng, GenRules rules)
        {
            var free = new bool[cols * rows];
            for (int i = 0; i < free.Length; i++) free[i] = true;

            var rects = new List<RectI>(cols * rows / 3);
            while (true)
            {
                int anchor = -1;
                for (int i = 0; i < free.Length; i++)
                    if (free[i]) { anchor = i; break; }
                if (anchor < 0) break;

                int ax = anchor % cols;
                int ay = anchor / cols;

                int freeW = 0;
                while (ax + freeW < cols && free[ay * cols + ax + freeW]) freeW++;
                int freeH = 0;
                while (ay + freeH < rows && free[(ay + freeH) * cols + ax]) freeH++;

                var rect = SampleRectangle(cols, rows, free, ax, ay, freeW, freeH, rng, rules);
                Mark(free, cols, rows, rect, false);
                rects.Add(rect);
            }
            return rects;
        }

        private static RectI SampleRectangle(int cols, int rows, bool[] free, int ax, int ay, int freeW, int freeH, Prng rng, GenRules rules)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int w = SampleDim(freeW, rng);
                int h = SampleDim(freeH, rng);
                if (rules.MaxRectArea > 0 && w * h > rules.MaxRectArea) continue;
                var rect = new RectI(ax, ay, w, h);
                if (RectFullyFree(cols, free, rect)) return rect;
            }
            return new RectI(ax, ay, 1, 1);
        }

        /// <summary>Uniform over [1, max], with one retry when it lands on 1 so that
        /// isolated 1x1 cells stay rare (P(1x1) drops to ~1/max² instead of 1/max).</summary>
        private static int SampleDim(int max, Prng rng)
        {
            int d = rng.NextInt(max) + 1;
            if (d == 1 && max >= 3)
            {
                int retry = rng.NextInt(max) + 1;
                if (retry >= 2) d = retry;
            }
            return d;
        }

        private static bool RectFullyFree(int cols, bool[] free, RectI rect)
        {
            for (int row = rect.Y; row < rect.Bottom; row++)
                for (int col = rect.X; col < rect.Right; col++)
                    if (!free[row * cols + col]) return false;
            return true;
        }

        private static void Mark(bool[] free, int cols, int rows, RectI rect, bool value)
        {
            for (int row = rect.Y; row < rect.Bottom && row < rows; row++)
                for (int col = rect.X; col < rect.Right && col < cols; col++)
                    free[row * cols + col] = value;
        }
    }
}
