using System.Collections.Generic;

namespace GridCut.Core
{
    /// <summary>
    /// Framework-free test cases so the same code runs under the Unity Test Runner
    /// and under the dotnet console tool. Each item is one named assertion.
    /// </summary>
    public static class TestCases
    {
        public sealed class Item
        {
            public string Name;
            public bool Pass;
            public string Detail;

            public Item(string name, bool pass, string detail = null)
            { Name = name; Pass = pass; Detail = detail; }
        }

        public static List<Item> RunAll() => RunAll(97); // default sample size

        public static List<Item> RunAll(int sampleSize)
        {
            var results = new List<Item>();

            // --- PRNG ---
            {
                var a = new Prng(12345);
                var b = new Prng(12345);
                bool same = true, inRange = true;
                for (int i = 0; i < 1000; i++)
                {
                    int x = a.NextInt(7);
                    if (x != b.NextInt(7)) same = false;
                    if (x < 0 || x >= 7) inRange = false;
                    a.NextBool(); b.NextBool();
                }
                results.Add(new Item("PRNG sama untuk seed sama", same));
                results.Add(new Item("PRNG dalam rentang", inRange));
                results.Add(new Item("PRNG seed beda hasil beda",
                    new Prng(1).NextInt(9) != new Prng(2).NextInt(9)));
            }

            // --- RectI ---
            {
                var r = new RectI(2, 3, 4, 5);
                results.Add(new Item("RectI Area", r.Area == 20));
                results.Add(new Item("RectI Contains", r.Contains(2, 3) && r.Contains(5, 7) && !r.Contains(6, 7) && !r.Contains(1, 3)));
                var o = new RectI(4, 5, 2, 2);
                results.Add(new Item("RectI Overlaps", r.Overlaps(o)));
                results.Add(new Item("RectI tidak overlap saat corner touch",
                    !new RectI(0, 0, 2, 2).Overlaps(new RectI(2, 2, 2, 2))));
                results.Add(new Item("RectI SharesEdge", new RectI(0, 0, 2, 2).SharesEdge(new RectI(2, 0, 2, 1))));
            }

            // --- RectCandidates: clue 2 di (0,0) punya tepat 2 kandidat ---
            {
                var clues = new[] { new Clue(0, 0, 2, 0) };
                var p = new Puzzle(4, 4, clues);
                var cand = RectCandidates.ForClue(p, 0);
                results.Add(new Item("Kandidat clue 2 di pojok = 2", cand.Count == 2,
                    $"count={cand.Count}"));
            }

            // --- Deduction menyelesaikan puzzle trivial (satu sel 4x4 penuh) ---
            {
                var clues = new[] { new Clue(0, 0, 4, 0) };
                var p = new Puzzle(2, 2, clues);
                var r = new DeductionSolver().Solve(p);
                results.Add(new Item("Deduction selesaikan puzzle 1 clue",
                    r.Status == SolveResult.Solved && r.Selection != null,
                    $"status={r.Status}"));
            }

            // --- Deduction: puzzle dengan dua solusi harus Stuck (kriteria "nol soal >1 solusi") ---
            {
                var clues = new[] { new Clue(0, 0, 2, 0), new Clue(1, 1, 2, 0) };
                var p = new Puzzle(2, 2, clues);
                var r = new DeductionSolver().Solve(p);
                results.Add(new Item("Puzzle bermulti-solusi: deduction Stuck",
                    r.Status == SolveResult.Stuck, $"status={r.Status}"));
                int count = BacktrackSolver.CountSolutions(p, 2);
                results.Add(new Item("Puzzle bermulti-solusi: backtrack = 2", count == 2, $"count={count}"));
            }

            // --- Properti: deduction Solved -> solusi unik (contoh acak kecil) ---
            {
                int consistent = 0;
                string firstReason = null;
                int triedSeeds = 0;
                int solvedCount = 0;
                for (int seed = 1; triedSeeds < sampleSize; seed++)
                {
                    var attempt = PuzzleGenerator.Generate(5, 4, (ulong)seed);
                    triedSeeds++;
                    if (!attempt.Ok)
                    {
                        if (firstReason == null) firstReason = attempt.RejectedAs.ToString();
                        continue;
                    }
                    solvedCount++;

                    int cnt = BacktrackSolver.CountSolutions(attempt.Puzzle, 2);
                    var ded = new DeductionSolver().Solve(attempt.Puzzle);
                    bool consistent_this = ded.Status == SolveResult.Solved ? cnt == 1 : true;
                    if (consistent_this) consistent++;
                    else firstReason = $"seed {seed}: deduction={ded.Status} backtrack={cnt}";
                }
                results.Add(new Item("Deduction Solved selalu solusinya unik",
                    consistent == solvedCount, firstReason));
                results.Add(new Item("Generator menghasilkan minimal 1 soal valid dari sampel",
                    solvedCount > 0, $"valid={solvedCount}/{triedSeeds}; alasan tolak pertama={firstReason}"));
            }

            // --- Generator: struktur solusi valid & deterministik ---
            {
                // determinism: the same seed must give identical output, pass or fail
                ulong seed = 11;
                var a = PuzzleGenerator.Generate(6, 5, seed);
                var b = PuzzleGenerator.Generate(6, 5, seed);
                bool det = SameLevel(a, b);
                results.Add(new Item("Generator deterministik pada seed sama", det,
                    a.Ok ? $"score={a.Score:0.##}" : $"reject={a.RejectedAs}"));

                string firstBad = null;
                int accepted = 0;
                for (ulong s = 1; s <= 120 && accepted < 8; s++)
                {
                    var t = PuzzleGenerator.Generate(8, 6, s);
                    if (!t.Ok) continue;
                    accepted++;
                    if (!ValidSolution(t)) firstBad = $"seed {s} invalid structure";
                    else
                    {
                        // filters hold on the chosen solution
                        int total = t.Solution.Count, ones = 0, spans = 0, big = 0;
                        foreach (var r in t.Solution)
                        {
                            if (r.Area == 1) ones++;
                            if (r.W == 8 || r.H == 6) spans++;
                            if (r.Area >= 4) big++;
                        }
                        if (ones > total * 15 / 100) firstBad = $"seed {s} 1x1 {ones}/{total}";
                        if (spans * 10 >= total * 7) firstBad = $"seed {s} liney {spans}/{total}";
                        if (big * 10 < total * 3) firstBad = $"seed {s} too-few-big {big}/{total}";
                    }
                }
                results.Add(new Item("Saringan pola dijaga (8x6, 8 contoh)", accepted >= 4 && firstBad == null,
                    $"accepted={accepted}; {firstBad}"));
            }

            // --- CanonicalHash invarian cermin/rotasi ---
            {
                var p = MakeSmallPuzzle(2, 2,
                    new[] { new Clue(0, 0, 2, 0), new Clue(1, 1, 2, 0) });
                ulong h0 = PuzzleGenerator.CanonicalHash(p);
                ulong hMirror = PuzzleGenerator.CanonicalHash(MirrorPuzzle(p));
                results.Add(new Item("CanonicalHash invarian cermin", h0 == hMirror));
            }

            // --- JSON round trip ---
            {
                var level = new LevelData
                {
                    Id = 137, Cols = 12, Rows = 8, Seed = 90210, Score = 5.25f, IsTutorial = false,
                    Clues = new[] { new Clue(0, 0, 2, 1), new Clue(1, 1, 3, 0) },
                    Solution = new[] { new RectI(0, 0, 2, 1), new RectI(0, 1, 4, 2) }
                };
                string json = LevelJson.Write(new LevelData[] { level });
                var parsed = LevelJson.Read(json);
                bool ok = parsed.Count == 1
                          && parsed[0].Id == 137 && parsed[0].Cols == 12 && parsed[0].Rows == 8
                          && parsed[0].Seed == 90210 && parsed[0].Score > 5.24f && parsed[0].Score < 5.26f
                          && parsed[0].Clues.Length == 2 && parsed[0].Solution.Length == 2
                          && parsed[0].Clues[1].ColorIndex == 0
                          && parsed[0].Solution[1].W == 4 && parsed[0].Solution[1].H == 2;
                results.Add(new Item("Level JSON round trip", ok, json.Length <= 400 ? json : "(json panjang)"));
            }

            // --- LevelPlan ---
            {
                var l1 = LevelPlan.SizeForLevel(1);
                var l50 = LevelPlan.SizeForLevel(50);
                var l150 = LevelPlan.SizeForLevel(150);
                results.Add(new Item("LevelPlan: awal 4x4, tengah 8x6, akhir 14x8",
                    l1.Cols == 4 && l1.Rows == 4 && l50.Cols == 8 && l50.Rows == 6 &&
                    l150.Cols == 14 && l150.Rows == 8));
                bool monotonic = true;
                for (int n = 2; n <= 150 && monotonic; n++)
                    if (LevelPlan.SizeForLevel(n).Cells < LevelPlan.SizeForLevel(n - 1).Cells)
                        monotonic = false;
                results.Add(new Item("LevelPlan: luas grid tidak menurun", monotonic));
                results.Add(new Item("LevelPlan: target dalam 1..10",
                    LevelPlan.TargetScore(1) >= 0.7f && LevelPlan.TargetScore(150) <= 10f));
            }

            return results;
        }

        /// <summary>Full structural validation of a generated attempt.</summary>
        public static bool ValidSolution(GenAttempt attempt)
        {
            if (!attempt.Ok || attempt.Solution == null) return false;
            var p = attempt.Puzzle;
            if (p.ClueCount != attempt.Solution.Count) return false;

            var seen = new bool[p.Cols * p.Rows];
            for (int i = 0; i < p.ClueCount; i++)
            {
                var r = attempt.Solution[i];
                var c = p.Clues[i];
                if (r.Area != c.Value) return false;
                if (!r.Contains(c.Col, c.Row)) return false;
                for (int row = r.Y; row < r.Bottom; row++)
                    for (int col = r.X; col < r.Right; col++)
                    {
                        int idx = row * p.Cols + col;
                        if (seen[idx]) return false;      // overlap = double coverage
                        seen[idx] = true;
                    }
            }
            foreach (var s in seen) if (!s) return false; // full coverage
            return true;
        }

        private static bool SameLevel(GenAttempt a, GenAttempt b)
        {
            if (a.Ok != b.Ok) return false;
            if (!a.Ok) return a.RejectedAs == b.RejectedAs;
            if (a.Puzzle.ClueCount != b.Puzzle.ClueCount) return false;
            for (int i = 0; i < a.Puzzle.ClueCount; i++)
            {
                var ca = a.Puzzle.Clues[i];
                var cb = b.Puzzle.Clues[i];
                if (ca.Col != cb.Col || ca.Row != cb.Row || ca.Value != cb.Value) return false;
            }
            return a.Score == b.Score;
        }

        private static Puzzle MakeSmallPuzzle(int cols, int rows, Clue[] clues) =>
            new Puzzle(cols, rows, clues);

        private static Puzzle MirrorPuzzle(Puzzle p)
        {
            var clues = new Clue[p.ClueCount];
            for (int i = 0; i < p.ClueCount; i++)
                clues[i] = new Clue(p.Cols - 1 - p.Clues[i].Col, p.Clues[i].Row, p.Clues[i].Value, p.Clues[i].ColorIndex);
            return new Puzzle(p.Cols, p.Rows, clues);
        }
    }
}
