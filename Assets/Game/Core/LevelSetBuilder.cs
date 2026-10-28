using System.Collections.Generic;

namespace GridCut.Core
{
    /// <summary>Per-level export statistics, for the calibration log.</summary>
    public sealed class ExportStats
    {
        public int Level;
        public ulong SeedsTried;
        public int Accepted;
        public int ScoreClampMin, ScoreClampMax;
        public float TargetScore, ChosenScore;
        public readonly Dictionary<RejectReason, int> Rejects = new Dictionary<RejectReason, int>();
        public bool TimeOut;
    }

    /// <summary>
    /// Builds the full 150-level set: computes grid size and target score per level,
    /// walks sequential seeds until a candidate passes uniqueness + deduction + filters
    /// + score band, assigns palette colors, then emits LevelData objects.
    /// CalibratedMode: for each grid size a sample of candidate scores is computed first,
    /// and the target score is clamped to the achievable range (never forced beyond it).
    /// </summary>
    public static class LevelSetBuilder
    {
        public const float ScoreTolerance = 0.4f;
        public const int CalibrationSamplesPerSize = 500;
        public const ulong MaxSeedsPerLevel = 60000;

        public sealed class Calibration
        {
            public GridSize Size;
            public float MinRaw;
            public float MaxRaw;
            public float LowAnchor;   // 5th percentile (reporting)
            public float HighAnchor;  // 95th percentile (reporting)
            public int Samples;       // valid, unique, deductive-solvable
            /// <summary>Sorted raw scores of all accepted calibration samples.</summary>
            public float[] Raws = System.Array.Empty<float>();
        }

        /// <summary>Sample candidate puzzles for one grid size to learn the achievable score range.</summary>
        /// <summary>Keeps a single obvious block from carrying the whole puzzle.
        /// Roughly one quarter of the board, with a floor of 8 cells.</summary>
        public static PuzzleGenerator.GenRules RulesFor(GridSize size)
        {
            int cells = size.Cols * size.Rows;
            return new PuzzleGenerator.GenRules
            {
                MaxRectArea = System.Math.Max(8, (cells + 3) / 4),
            };
        }

        public static Calibration Calibrate(GridSize size, int sampleCount)
        {
            var cal = new Calibration { Size = size, MinRaw = float.MaxValue, MaxRaw = 0f };
            var raws = new List<float>(sampleCount);
            ulong seed = 1;
            int got = 0;
            while (got < sampleCount && seed < MaxSeedsPerLevel * 4)
            {
                var attempt = PuzzleGenerator.Generate(size.Cols, size.Rows, seed, RulesFor(size));
                if (attempt.Ok)
                {
                    raws.Add(attempt.Score);
                    if (attempt.Score < cal.MinRaw) cal.MinRaw = attempt.Score;
                    if (attempt.Score > cal.MaxRaw) cal.MaxRaw = attempt.Score;
                    got++;
                }
                seed++;
            }
            cal.Samples = got;
            raws.Sort();
            cal.Raws = raws.ToArray();
            if (got > 0)
            {
                cal.LowAnchor = raws[(got - 1) * 5 / 100];
                cal.HighAnchor = raws[(got - 1) * 95 / 100];
                if (cal.HighAnchor <= cal.LowAnchor) cal.HighAnchor = cal.LowAnchor + 1f;
            }
            else
            {
                cal.MinRaw = cal.MaxRaw = cal.LowAnchor = cal.HighAnchor = 0f;
            }
            return cal;
        }

        /// <summary>
        /// Map a raw solver score to the spec's 1..10 scale, linear between the size's
        /// p5 and p95 (anchors from the calibration sample). Raw scores are discrete with
        /// a large mass at the floor, which maps to exactly 1.0 — gapping bands near the
        /// floor are handled by the acceptance edge plus the closest-fit fallback.
        /// </summary>
        public static float Normalize(float raw, Calibration cal)
        {
            if (cal == null || cal.Samples == 0) return raw;
            float t = (raw - cal.LowAnchor) / (cal.HighAnchor - cal.LowAnchor);
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return 1f + 9f * t;
        }

        public static float ClampTarget(float target, Calibration cal)
        {
            if (cal == null || cal.Samples == 0) return target;
            // normalized scale: band between the anchors' mapped values 1..10
            float lo = 1.05f, hi = 9.95f;
            if (target < lo) return lo;
            if (target > hi) return hi;
            return target;
        }

        /// <summary>
        /// Build every requested level. One shared canonical-hash set prevents cross-band
        /// duplicates. Candidates are generated ONCE per size band into a pool, then
        /// matched to each level target nearest-first — levels never re-scan the same
        /// seed sequence.
        /// </summary>
        public static List<LevelData> Build(
            int levelFrom, int levelTo,
            Dictionary<string, Calibration> calibrationByBand,
            HashSet<ulong> seenHashes,
            Dictionary<int, ExportStats> statsOut)
        {
            var levels = new List<LevelData>(levelTo - levelFrom + 1);

            // group into bands of equal grid size
            var bands = new List<List<int>>();
            var current = new List<int>();
            GridSize currentSize = default;
            for (int level = levelFrom; level <= levelTo; level++)
            {
                var size = LevelPlan.SizeForLevel(level);
                if (current.Count > 0 && (size.Cols != currentSize.Cols || size.Rows != currentSize.Rows))
                {
                    bands.Add(current);
                    current = new List<int>();
                }
                current.Add(level);
                currentSize = size;
            }
            if (current.Count > 0) bands.Add(current);

            foreach (var band in bands)
                BuildBand(band, bands.IndexOf(band), calibrationByBand, seenHashes, statsOut, levels);

            return levels;
        }

        private sealed class Candidate
        {
            public GenAttempt Attempt;
            public float NormScore;
        }

        private static void BuildBand(
            List<int> bandLevels,
            int bandIndex,
            Dictionary<string, Calibration> calibrationByBand,
            HashSet<ulong> seenHashes,
            Dictionary<int, ExportStats> statsOut,
            List<LevelData> levels)
        {
            var size = LevelPlan.SizeForLevel(bandLevels[0]);
            string band = size.Cols + "x" + size.Rows;
            Calibration cal = calibrationByBand != null && calibrationByBand.TryGetValue(band, out var c)
                ? c : null;

            // generate the candidate pool once for the whole band (seed block per band)
            int poolTarget = System.Math.Max(120, bandLevels.Count * 24);
            var pool = new List<Candidate>(poolTarget);
            var bandRejects = new Dictionary<RejectReason, int>();
            int seedsTried = 0;
            const int MaxSeedsPerBand = 200000;
            ulong seedBase = 1_000_000UL + (ulong)bandIndex * 1_000_000UL;
            for (int i = 0; pool.Count < poolTarget && i < MaxSeedsPerBand; i++)
            {
                seedsTried++;
                var attempt = PuzzleGenerator.Generate(size.Cols, size.Rows, (ulong)(seedBase + (ulong)i), RulesFor(size));
                if (!attempt.Ok)
                {
                    Count(bandRejects, attempt.RejectedAs);
                    continue;
                }
                if (!ClueCountOk(size, attempt.Puzzle.ClueCount, out var clueReject))
                {
                    Count(bandRejects, clueReject);
                    continue;
                }
                ulong hash = PuzzleGenerator.CanonicalHash(attempt.Puzzle);
                if (!seenHashes.Add(hash))
                {
                    Count(bandRejects, RejectReason.Duplicate);
                    continue;
                }
                pool.Add(new Candidate { Attempt = attempt, NormScore = Normalize(attempt.Score, cal) });
            }
            pool.Sort((a, b) => a.NormScore.CompareTo(b.NormScore));
            // Papan 7x7+ punya kandidat sulit dan mudah yang terpisah jauh; jangan
            // memakai kandidat lantai untuk level pertama band baru (lonjakan 1 -> 4).
            float minNorm = size.Cells >= 49 ? 2f : 0f;

            // assignment: easiest target gets the lowest available candidate
            var ordered = new List<int>(bandLevels);
            ordered.Sort((a, b) => LevelPlan.TargetScore(a).CompareTo(LevelPlan.TargetScore(b)));
            var used = new bool[pool.Count];

            foreach (int level in ordered)
            {
                float target = LevelPlan.TargetScore(level);
                float clamped = ClampTarget(target, cal);
                bool tutorial = LevelPlan.IsTutorial(level);

                int best = -1;
                float bestDelta = float.MaxValue;
                for (int i = 0; i < pool.Count; i++)
                {
                    if (used[i]) continue;
                    if (pool[i].NormScore < minNorm) continue;
                    if (tutorial && pool[i].NormScore > 3.5f) continue;
                    float d = System.Math.Abs(pool[i].NormScore - clamped);
                    if (d < bestDelta) { bestDelta = d; best = i; }
                }

                var stat = new ExportStats { Level = level, TargetScore = target, SeedsTried = (ulong)seedsTried };
                foreach (var kv in bandRejects) stat.Rejects[kv.Key] = kv.Value;

                if (best < 0)
                {
                    stat.TimeOut = true;
                    levels.Add(MakeFallback(level, size, tutorial));
                    statsOut[level] = stat;
                    continue;
                }

                float normScore = pool[best].NormScore;
                if (bestDelta > ScoreTolerance) stat.TimeOut = true;
                used[best] = true;
                stat.ChosenScore = normScore;

                levels.Add(MakeLevel(level, size, pool[best].Attempt.Seed, normScore, tutorial, pool[best].Attempt));
                statsOut[level] = stat;
            }
        }

        static bool ClueCountOk(GridSize size, int clues, out RejectReason reason)
        {
            int min, max;
            int cells = size.Cols * size.Rows;
            if (cells >= 112) { min = 13; max = 24; }
            else if (cells >= 96) { min = 12; max = 24; }
            else if (cells >= 80) { min = 11; max = 22; }
            else if (cells >= 63) { min = 10; max = 20; }
            else if (cells >= 48) { min = 10; max = 18; }
            else if (cells >= 36) { min = 7; max = 16; }
            else if (cells >= 30) { min = 6; max = 14; }
            else { min = 5; max = 12; }
            if (clues < min) { reason = RejectReason.TooFewClues; return false; }
            if (clues > max) { reason = RejectReason.TooManyClues; return false; }
            reason = RejectReason.None;
            return true;
        }

        public enum Acceptance { Yes, No }

        /// <summary>
        /// Band acceptance on the normalized 1..10 scale. The distribution of raw scores
        /// is discrete with mass at the floor (exactly-1.0 normalized), so when a target
        /// sits near the bottom, the floor itself is accepted instead of leaving a gap.
        /// </summary>
        private static Acceptance Acceptable(float score, float target, bool tutorial)
        {
            if (tutorial) return score <= 3.5f ? Acceptance.Yes : Acceptance.No;
            float lowerEdge = System.Math.Min(1f, target - ScoreTolerance);
            bool inBand = score >= lowerEdge && score <= target + ScoreTolerance;
            return inBand ? Acceptance.Yes : Acceptance.No;
        }

        private static LevelData MakeLevel(int level, GridSize size, ulong seed, float score,
            bool tutorial, GenAttempt attempt)
        {
            var clueColors = LevelColors.AssignColorIndices(attempt.Puzzle, attempt.Solution);
            var clues = new Clue[attempt.Puzzle.ClueCount];
            for (int i = 0; i < clues.Length; i++)
            {
                var c = attempt.Puzzle.Clues[i];
                clues[i] = new Clue(c.Col, c.Row, c.Value, clueColors[i]);
            }
            return new LevelData
            {
                Id = level,
                Cols = size.Cols,
                Rows = size.Rows,
                Seed = seed,
                Score = score,
                IsTutorial = tutorial,
                Clues = clues,
                Solution = attempt.Solution.ToArray()
            };
        }

        /// <summary>Last-resort level so the set is complete even if a size band stalls.
        /// Flat "line" partition; flagged via score 0 and must be replaced manually.</summary>
        private static LevelData MakeFallback(int level, GridSize size, bool tutorial)
        {
            var clues = new List<Clue>();
            var solution = new List<RectI>();
            int id = 0;
            for (int row = 0; row < size.Rows; row += 2)
            {
                int h = System.Math.Min(2, size.Rows - row);
                var rect = new RectI(0, row, size.Cols, h);
                solution.Add(rect);
                clues.Add(new Clue(0, row, rect.Area, id++ % PuzzleGenerator.PaletteColors));
            }
            return new LevelData
            {
                Id = level, Cols = size.Cols, Rows = size.Rows,
                Seed = 0, Score = -1f, IsTutorial = tutorial,
                Clues = clues.ToArray(), Solution = solution.ToArray()
            };
        }

        private static void Count(Dictionary<RejectReason, int> d, RejectReason r)
        {
            if (d.TryGetValue(r, out int n)) d[r] = n + 1;
            else d[r] = 1;
        }
    }
}
