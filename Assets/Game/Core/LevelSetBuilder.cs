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
    /// generates one candidate pool per size band, gates the deduction technique the
    /// band is allowed to require, orders candidates by the continuous difficulty
    /// metric, assigns palette colors, then emits LevelData objects.
    /// </summary>
    public static class LevelSetBuilder
    {
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
        /// Build every requested level. One shared canonical-hash set prevents cross-band
        /// duplicates. Candidates are generated ONCE per size band into a pool, ordered
        /// by the continuous difficulty metric, then matched to the level targets.
        /// Each band's easiest..hardest pool is mapped exactly onto that band's target
        /// span, so difficulty rises inside every board size instead of sitting on the
        /// rule-1 floor until the next board arrives.
        /// </summary>
        public static List<LevelData> Build(
            int levelFrom, int levelTo,
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
                BuildBand(band, bands.IndexOf(band), seenHashes, statsOut, levels);

            return levels;
        }

        private sealed class Candidate
        {
            public GenAttempt Attempt;
            public float Difficulty;
        }

        private static void BuildBand(
            List<int> bandLevels,
            int bandIndex,
            HashSet<ulong> seenHashes,
            Dictionary<int, ExportStats> statsOut,
            List<LevelData> levels)
        {
            var size = LevelPlan.SizeForLevel(bandLevels[0]);
            // Technique gate: the first board teaches unique-candidate placement;
            // forced-cell logic arrives with the second board, elimination with the
            // first 7x7. No band asks for a technique the player has not met yet.
            int maxWeight = bandIndex == 0 ? 1 : bandIndex <= 2 ? 2 : 3;

            // generate the candidate pool once for the whole band (seed block per band)
            int poolTarget = System.Math.Max(160, bandLevels.Count * 28);
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
                if (attempt.DeductionMaxWeight > maxWeight)
                {
                    Count(bandRejects, RejectReason.TechniqueTooHard);
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
                pool.Add(new Candidate { Attempt = attempt, Difficulty = attempt.Difficulty });
            }

            if (pool.Count == 0)
            {
                foreach (int level in bandLevels)
                {
                    statsOut[level] = new ExportStats { Level = level, TargetScore = LevelPlan.TargetScore(level), TimeOut = true };
                    levels.Add(MakeFallback(level, size, LevelPlan.IsTutorial(level)));
                }
                return;
            }

            pool.Sort((a, b) => a.Difficulty.CompareTo(b.Difficulty));

            // Assign by RANK, not by raw difficulty value. The raw distribution is
            // bimodal (rule-1 boards around 3-5, rule-2 boards around 6-7), so a linear
            // value mapping would jump to rule 2 at the third level of a band. Rank
            // spacing keeps the early targets on rule-1 boards and crosses into rule 2
            // only where the pool actually has those boards.
            float targetFirst = LevelPlan.TargetScore(bandLevels[0]);
            float targetLast = LevelPlan.TargetScore(bandLevels[bandLevels.Count - 1]);
            int loIdx = (pool.Count - 1) * 2 / 100;
            int hiIdx = (pool.Count - 1) * 98 / 100;
            if (hiIdx <= loIdx) { loIdx = 0; hiIdx = pool.Count - 1; }

            var ordered = new List<int>(bandLevels);
            ordered.Sort((a, b) => LevelPlan.TargetScore(a).CompareTo(LevelPlan.TargetScore(b)));
            var used = new bool[pool.Count];
            int prevIdx = -1;

            foreach (int level in ordered)
            {
                float target = LevelPlan.TargetScore(level);
                bool tutorial = LevelPlan.IsTutorial(level);

                float q = targetLast > targetFirst
                    ? (target - targetFirst) / (targetLast - targetFirst)
                    : 0f;
                int want = loIdx + (int)System.Math.Round(q * (hiIdx - loIdx));
                int idx = System.Math.Max(want, prevIdx + 1);
                while (idx <= hiIdx && used[idx]) idx++;
                if (idx > hiIdx)
                {
                    for (int i = hiIdx; i >= 0; i--)
                        if (!used[i]) { idx = i; break; }
                }

                var stat = new ExportStats { Level = level, TargetScore = target, SeedsTried = (ulong)seedsTried };
                foreach (var kv in bandRejects) stat.Rejects[kv.Key] = kv.Value;

                if (idx < 0 || idx >= pool.Count)
                {
                    stat.TimeOut = true;
                    levels.Add(MakeFallback(level, size, tutorial));
                    statsOut[level] = stat;
                    continue;
                }

                float normScore = targetFirst + (targetLast - targetFirst)
                    * ((idx - loIdx) / (float)System.Math.Max(1, hiIdx - loIdx));
                used[idx] = true;
                prevIdx = idx;
                stat.ChosenScore = normScore;

                levels.Add(MakeLevel(level, size, pool[idx].Attempt.Seed, normScore, tutorial, pool[idx].Attempt));
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
