using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GridCut.Core;

namespace GridCut.Tooling
{
    /// <summary>
    /// Console tool for the content pipeline:
    ///   tests      - run every Core test case
    ///   soak       - generate 1000 valid unique puzzles across all grid sizes
    ///   levels     - calibrate + build + export the 150-level data file
    ///   calibrate  - report achievable score ranges per size only
    /// Output goes to Tooling/out/ and, for export, Assets/Game/Content/.
    /// </summary>
    internal static class Program
    {
        private const string OutDir = "out";
        private const string ExportPath = "../Assets/Game/Content/Resources/levels.json";
        private const int SoakTotal = 1000;

        private static int Main(string[] args)
        {
            var command = args.Length > 0 ? args[0].ToLowerInvariant() : "tests";
            Directory.CreateDirectory(OutDir);

            switch (command)
            {
                case "tests":
                    RunTests(sampleSize: 97);
                    return 0;

                case "testslarge":
                    RunTests(sampleSize: 4000);
                    return 0;

                case "soak":
                    return RunSoak();

                case "levels":
                    return RunLevels();

                case "calibrate":
                    RunCalibrationOnly();
                    return 0;

                case "verify":
                    return VerifyExported();

                default:
                    Console.WriteLine($"Perintah tak dikenal: {command}. Pakai: tests | testslarge | soak | levels | calibrate | verify");
                    return 1;
            }
        }

        private static void RunTests(int sampleSize)
        {
            Console.WriteLine($"Menjalankan test cases (sampel {sampleSize})...");
            var items = TestCases.RunAll(sampleSize);
            int failed = 0;
            foreach (var item in items)
            {
                Console.WriteLine($"  [{(item.Pass ? "PASS" : "GAGAL")}] {item.Name}{(item.Pass ? "" : $"  -> {item.Detail}")}");
                if (!item.Pass) failed++;
            }
            Console.WriteLine(failed == 0
                ? $"Semua {items.Count} test PASS."
                : $"{failed}/{items.Count} test GAGAL.");
        }

        /// <summary>
        /// 1000 puzzles spread over all grid sizes. Every accepted puzzle is regenerated
        /// through the same pipeline the game content uses, then re-verified independently.
        /// </summary>
        private static int RunSoak()
        {
            var plan = new (int cols, int rows)[]
            {
                (4, 4),
                (5, 4),
                (5, 5),
                (6, 5),
                (8, 6),
                (10, 6),
                (12, 6),
                (14, 6),
            };

            // even spread: soak focuses on proof of uniqueness at each size, berimbang
            int perSize = SoakTotal / plan.Length;          // 125
            int remainder = SoakTotal - perSize * plan.Length;

            var hashes = new HashSet<ulong>();
            var rejectSummary = new SortedDictionary<string, int>();
            var invalidSummary = new List<string>();
            var sw = Stopwatch.StartNew();

            int generated = 0;
            foreach (var (cols, rows) in plan)
            {
                ulong seed = 1;
                int target = perSize + remainder;
                remainder = 0;
                int got = 0;
                while (got < target)
                {
                    var attempt = PuzzleGenerator.Generate(cols, rows, seed);
                    if (!attempt.Ok)
                        Summarize(rejectSummary, $"{cols}x{rows}:{attempt.RejectedAs}");

                    if (attempt.Ok)
                    {
                        if (!hashes.Add(PuzzleGenerator.CanonicalHash(attempt.Puzzle)))
                            Summarize(rejectSummary, $"{cols}x{rows}:{RejectReason.Duplicate}");
                        else
                        {
                            // independent re-verification, not trusting the generator
                            int cnt = BacktrackSolver.CountSolutions(attempt.Puzzle, 2);
                            var ded = new DeductionSolver().Solve(attempt.Puzzle);
                            if (!TestCases.ValidSolution(attempt))
                                invalidSummary.Add($"{cols}x{rows} seed{seed}: struktur tidak valid");
                            else if (cnt != 1)
                                invalidSummary.Add($"{cols}x{rows} seed{seed}: backtrack menemukan {cnt} solusi!");
                            else if (ded.Status != SolveResult.Solved)
                                invalidSummary.Add($"{cols}x{rows} seed{seed}: solver deduksi stuck");
                            else
                            {
                                got++;
                                generated++;
                            }
                        }
                    }
                    seed++;
                }
                Console.WriteLine($"  {cols}x{rows}: {got}/{target} soal valid unik (seed sampai {seed - 1})");
            }

            sw.Stop();
            var lines = new List<string>
            {
                $"total_valid={generated}",
                $"waktu={sw.Elapsed.TotalSeconds:0.0}s",
                ""
            };
            lines.AddRange(rejectSummary.Select(k => $"reject {k.Key}: {k.Value}"));
            lines.Add("");
            lines.AddRange(invalidSummary);
            File.WriteAllLines($"{OutDir}/soak-report.txt", lines);

            Console.WriteLine($"Selesai: {generated}/{SoakTotal} valid unik berjumlah 1000 (waktu {sw.Elapsed.TotalSeconds:0.0}s).");
            if (invalidSummary.Count > 0) Console.WriteLine($"MASALAH: {invalidSummary.Count} anomali terdeteksi!");
            else Console.WriteLine("KRITERIA: nol soal dengan lebih dari satu solusi — OK.");
            return generated == SoakTotal && invalidSummary.Count == 0 ? 0 : 2;
        }

        /// <summary>
        /// Re-reads the exported levels.json and re-verifies every level independently:
        /// exactly one solution, deduction solvable, solution consistent with clues,
        /// colors all in range.
        /// </summary>
        private static int VerifyExported()
        {
            string path = ExportPath;
            if (!File.Exists(path)) { Console.WriteLine($"Berkas tidak ditemukan: {path}"); return 1; }

            var levels = LevelJson.Read(File.ReadAllText(path));
            Console.WriteLine($"Memverifikasi {levels.Count} level dari {path}...");

            int bad = 0;
            foreach (var l in levels)
            {
                string problems = null;
                if (l.Clues == null || l.Solution == null || l.Clues.Length != l.Solution.Length)
                    problems = "clue/solusi tidak konsisten";
                else
                {
                    var clues = new Clue[l.Clues.Length];
                    for (int i = 0; i < l.Clues.Length; i++)
                    {
                        var c = l.Clues[i];
                        if (c.ColorIndex < 0 || c.ColorIndex >= PuzzleGenerator.PaletteColors)
                            problems = $"color index {c.ColorIndex} di luar palet";
                        clues[i] = new Clue(c.Col, c.Row, c.Value, c.ColorIndex);
                    }

                    if (problems == null)
                    {
                        var p = new Puzzle(l.Cols, l.Rows, clues);
                        int count = BacktrackSolver.CountSolutions(p, 2);
                        if (count != 1) problems = $"backtrack menemukan {count} solusi";
                        else
                        {
                            var ded = new DeductionSolver().Solve(p);
                            if (ded.Status != SolveResult.Solved) problems = "solver deduksi stuck";
                            else
                            {
                                // solution stored must be THE solution and valid
                                var fake = new GenAttempt { Ok = true, Puzzle = p, Solution = l.Solution.ToList() };
                                if (!TestCases.ValidSolution(fake)) problems = "solusi tersimpan tidak valid";
                                else
                                {
                                    for (int i = 0; i < clues.Length; i++)
                                        if (l.Solution[i].Area != clues[i].Value || !l.Solution[i].Contains(clues[i].Col, clues[i].Row))
                                        { problems = $"clue/solusi tidak cocok di {i}"; break; }
                                }
                            }
                        }
                    }
                }

                if (problems != null)
                {
                    bad++;
                    Console.WriteLine($"  [GAGAL] level {l.Id} ({l.Cols}x{l.Rows}, seed {l.Seed}): {problems}");
                }
            }

            Console.WriteLine(bad == 0
                ? "Semua level lolos verifikasi ulang: tepat satu solusi, deduksi selesai, solusi konsisten."
                : $"{bad} level bermasalah!");
            return bad == 0 ? 0 : 2;
        }

        private static void Summarize(SortedDictionary<string, int> d, string key)
        {
            if (d.TryGetValue(key, out int n)) d[key] = n + 1;
            else d[key] = 1;
        }

        private static void RunCalibrationOnly()
        {
            var sw = Stopwatch.StartNew();
            var lines = new List<string>();
            foreach (var size in AllSizes())
            {
                var c = LevelSetBuilder.Calibrate(size, LevelSetBuilder.CalibrationSamplesPerSize);
                lines.Add($"{size.Cols}x{size.Rows}\t{c.Samples}\t{c.MinRaw:0.###}\t{c.MaxRaw:0.###}\t{c.LowAnchor:0.###}\t{c.HighAnchor:0.###}");
                Console.WriteLine($"  {size.Cols}x{size.Rows}: {c.Samples} sampel, raw {c.MinRaw:0.##} .. {c.MaxRaw:0.##} (p5 {c.LowAnchor:0.##} / p95 {c.HighAnchor:0.##} → skala 1..10)");
            }
            sw.Stop();
            File.WriteAllLines($"{OutDir}/calibration.txt", lines);
            Console.WriteLine($"Kalibrasi selesai ({sw.Elapsed.TotalSeconds:0.0}s) -> out/calibration.txt");
        }

        /// <summary>Semua ukuran yang benar-benar dipakai LevelPlan (urut kemunculan) —
        /// tidak ada lagi daftar manual yang bisa mancurus.</summary>
        private static GridSize[] AllSizes()
        {
            var list = new List<GridSize>();
            for (int l = 1; l <= LevelPlan.TotalLevels; l++)
            {
                var s = LevelPlan.SizeForLevel(l);
                if (!list.Contains(s)) list.Add(s);
            }
            return list.ToArray();
        }

        private static int RunLevels()
        {
            var swOverall = Stopwatch.StartNew();

            // 1. kalibrasi: rentang skor yang mampu dihasilkan per ukuran
            var calibrations = new Dictionary<string, LevelSetBuilder.Calibration>();
            Console.WriteLine("Kalibrasi per ukuran grid...");
            foreach (var size in AllSizes())
            {
                var c = LevelSetBuilder.Calibrate(size, LevelSetBuilder.CalibrationSamplesPerSize);
                calibrations[size.Cols + "x" + size.Rows] = c;
                Console.WriteLine($"  {size.Cols}x{size.Rows}: {c.Samples} sampel, raw {c.MinRaw:0.##}..{c.MaxRaw:0.##} → skala 1..10 (p5 {c.LowAnchor:0.##} / p95 {c.HighAnchor:0.##})");
            }

            // 2. build 150 level
            var stats = new Dictionary<int, ExportStats>();
            var hashes = new HashSet<ulong>();
            Console.WriteLine("Membangun 150 level...");
            var levels = LevelSetBuilder.Build(1, LevelPlan.TotalLevels, calibrations, hashes, stats);

            int stalled = 0;
            foreach (var l in levels)
                if (l.Score < 0f) stalled++;
            if (stalled > 0)
                Console.WriteLine($"  PERINGATAN: {stalled} level fallback — perlu diperiksa (laporan).");

            // 3. tulis berkas & laporan
            string file = LevelJson.Write(levels);
            Directory.CreateDirectory("../Assets/Game/Content");
            File.WriteAllText(ExportPath, file);
            File.WriteAllText($"{OutDir}/levels-report.txt", ReportText(levels, stats, calibrations));

            swOverall.Stop();
            Console.WriteLine($"Ekspor {levels.Count} level -> {ExportPath} ({file.Length / 1024} KB)");
            Console.WriteLine($"Total waktu {swOverall.Elapsed.TotalSeconds:0.0}s. Laporan: out/levels-report.txt");
            return stalled == 0 ? 0 : 1;
        }

        private static string ReportText(
            List<LevelData> levels,
            Dictionary<int, ExportStats> stats,
            Dictionary<string, LevelSetBuilder.Calibration> calibrations)
        {
            var lines = new List<string>
            {
                "GridCut level export report",
                "level\tgrid\tseed\tscore\ttarget\tseedsTried\trejects"
            };

            foreach (var l in levels)
            {
                if (!stats.TryGetValue(l.Id, out var st))
                {
                    lines.Add($"{l.Id}\t{l.Cols}x{l.Rows}\t-\t0\t-\t-\t(no stats)");
                    continue;
                }
                var rejects = st.Rejects.Count == 0 ? "-" : string.Join(" ",
                    st.Rejects.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
                lines.Add($"{l.Id}\t{l.Cols}x{l.Rows}\t{l.Seed}\t{l.Score:0.##}\t{st.TargetScore:0.##}\t{st.SeedsTried}\t{rejects}");
            }

            lines.Add("");
            lines.Add("KALIBRASI (sampel per ukuran, skala mentah → ternormalisasi 1..10):");
            foreach (var c in calibrations.Values)
                lines.Add($"{c.Size.Cols}x{c.Size.Rows}: {c.Samples} sampel, raw {c.MinRaw:0.##}..{c.MaxRaw:0.##}, jangkar p5={c.LowAnchor:0.##} p95={c.HighAnchor:0.##}");
            return string.Join("\n", lines);
        }
    }
}
