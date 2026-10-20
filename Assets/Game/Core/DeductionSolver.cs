using System.Collections.Generic;

namespace GridCut.Core
{
    /// <summary>Solver weights per the GDD: the four deduction techniques.</summary>
    public enum SolveStepWeight : int
    {
        UniqueCandidate = 1,        // clue with exactly one possible rectangle
        ForcedCell = 2,             // cell reachable only through one clue
        Elimination = 3,            // candidate whose removal leaves the rest unsolvable
        Trial = 8                   // one-level assumption, dropped on contradiction
    }

    /// <summary>Outcome of the deduction solver.</summary>
    public enum SolveResult { Solved, Stuck }

    /// <summary>
    /// Deduction-only solver. Solves a puzzle strictly with sound rules (no branching
    /// search except single-level trial-and-error), so a Stuck verdict matches the GDD
    /// rule "puzzles the player would have to guess are rejected".
    /// Score = sum(weights)/clues + 2*max weight.
    /// </summary>
    public sealed class DeductionSolver
    {
        public sealed class Result
        {
            public SolveResult Status;
            public List<RectI> Selection;   // valid when Solved
            public long WeightSum;
            public int MaxWeight;
            public int Steps;
            public int ClueCount;
            public float Score => ClueCount <= 0 ? 0f : (WeightSum / (float)ClueCount) + 2f * MaxWeight;
        }

        private sealed class State
        {
            public Puzzle Puzzle;
            public List<RectI>[] Cand = System.Array.Empty<List<RectI>>();
            public bool[] Fixed = System.Array.Empty<bool>();
            public RectI[] Sel = System.Array.Empty<RectI>();
            public bool[] Covered = System.Array.Empty<bool>();

            public State(Puzzle p)
            {
                Puzzle = p;
                Cand = RectCandidates.Build(p);
                int n = p.ClueCount;
                Fixed = new bool[n];
                Sel = new RectI[n];
                Covered = new bool[p.Cols * p.Rows];
            }

            public State(State from)
            {
                Puzzle = from.Puzzle;
                int n = from.Fixed.Length;
                Cand = new List<RectI>[n];
                for (int c = 0; c < n; c++) Cand[c] = new List<RectI>(from.Cand[c]);
                Fixed = (bool[])from.Fixed.Clone();
                Sel = (RectI[])from.Sel.Clone();
                Covered = (bool[])from.Covered.Clone();
            }

            public bool IsFixedFully()
            {
                for (int c = 0; c < Fixed.Length; c++) if (!Fixed[c]) return false;
                return true;
            }

            public bool AllCovered()
            {
                for (int i = 0; i < Covered.Length; i++) if (!Covered[i]) return false;
                return true;
            }

            public bool IsDead()
            {
                for (int c = 0; c < Cand.Length; c++)
                    if (!Fixed[c] && Cand[c].Count == 0) return true;
                return !AllCellsReachable();
            }

            private bool AllCellsReachable()
            {
                int w = Puzzle.Cols;
                for (int row = 0; row < Puzzle.Rows; row++)
                    for (int col = 0; col < w; col++)
                    {
                        int idx = row * w + col;
                        if (Covered[idx]) continue;
                        if (!CellReachable(col, row, idx)) return false;
                    }
                return true;
            }

            public bool CellReachable(int col, int row, int cellIdx)
            {
                for (int c = 0; c < Cand.Length; c++)
                {
                    if (Fixed[c]) continue;
                    var list = Cand[c];
                    for (int i = 0; i < list.Count; i++)
                        if (list[i].Contains(col, row)) return true;
                }
                return false;
            }

            /// <summary>Prune candidates of unfixed clues that overlap a fixed rect.</summary>
            public void PruneAgainstFixed()
            {
                int n = Cand.Length;
                for (int c = 0; c < n; c++)
                {
                    if (Fixed[c]) continue;
                    var list = Cand[c];
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        bool overlap = false;
                        for (int f = 0; f < n && !overlap; f++)
                            if (Fixed[f] && list[i].Overlaps(Sel[f])) overlap = true;
                        if (overlap)
                        {
                            list[i] = list[list.Count - 1];
                            list.RemoveAt(list.Count - 1);
                        }
                    }
                }
            }

            public void Fix(int clueIndex, RectI rect)
            {
                Fixed[clueIndex] = true;
                Sel[clueIndex] = rect;
                for (int row = rect.Y; row < rect.Bottom; row++)
                    for (int col = rect.X; col < rect.Right; col++)
                        Covered[row * Puzzle.Cols + col] = true;
            }

            public void Unfix(int clueIndex)
            {
                var rect = Sel[clueIndex];
                for (int row = rect.Y; row < rect.Bottom; row++)
                    for (int col = rect.X; col < rect.Right; col++)
                        Covered[row * Puzzle.Cols + col] = false;
                Fixed[clueIndex] = false;
                Sel[clueIndex] = default;
            }

            public void RemoveCandidate(int clueIndex, RectI rect)
            {
                Cand[clueIndex].RemoveAll(r => r.X == rect.X && r.Y == rect.Y && r.W == rect.W && r.H == rect.H);
            }
        }

        private State _root;
        private long _weightSum;
        private int _maxWeight;
        private int _steps;
        private int _clueCount;

        public Result Solve(Puzzle puzzle)
        {
            _root = new State(puzzle);
            _clueCount = puzzle.ClueCount;
            _weightSum = 0;
            _maxWeight = 0;
            _steps = 0;

            SolveResult status = RunLoop(_root);
            if (status == SolveResult.Solved && !_root.AllCovered()) status = SolveResult.Stuck;

            return new Result
            {
                Status = status,
                Selection = status == SolveResult.Solved
                    ? new List<RectI>(_root.Sel)
                    : null,
                WeightSum = _weightSum,
                MaxWeight = _maxWeight,
                Steps = _steps,
                ClueCount = _clueCount
            };
        }

        /// <summary>Applies rules 1-4 on `state` until solved or stuck. State is mutated.</summary>
        private SolveResult RunLoop(State state)
        {
            while (true)
            {
                state.PruneAgainstFixed();
                if (state.IsDead()) return SolveResult.Stuck;

                if (state.IsFixedFully())
                    return state.AllCovered() ? SolveResult.Solved : SolveResult.Stuck;

                if (TryRule1(state)) continue;
                if (TryRule2(state)) continue;
                if (TryRule3(state)) continue;
                if (TryRule4(state)) continue;

                return SolveResult.Stuck;
            }
        }

        private void Add(int weight)
        {
            _weightSum += weight;
            if (weight > _maxWeight) _maxWeight = weight;
            _steps++;
        }

        // -------- rule 1: unique candidate fixes the clue --------

        private bool TryRule1(State state)
        {
            for (int c = 0; c < _clueCount; c++)
            {
                var list = state.Cand[c];
                if (state.Fixed[c] || list.Count != 1) continue;
                state.Fix(c, list[0]);
                state.Cand[c] = new List<RectI>(0);
                Add(1);
                return true;
            }
            return false;
        }

        // -------- rule 2: a cell only one clue can reach -> that clue must cover it --------

        private bool TryRule2(State state)
        {
            int w = state.Puzzle.Cols;
            for (int row = 0; row < state.Puzzle.Rows; row++)
            {
                for (int col = 0; col < w; col++)
                {
                    int idx = row * w + col;
                    if (state.Covered[idx]) continue;

                    // which (single) clue has candidates covering this cell?
                    int sole = -1;
                    for (int c = 0; c < _clueCount; c++)
                    {
                        if (state.Fixed[c]) continue;
                        var list = state.Cand[c];
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (list[i].Contains(col, row))
                            {
                                if (sole >= 0) { sole = -2; break; }
                                sole = c;
                            }
                        }
                        if (sole == -2) break;
                    }
                    if (sole >= 0)
                    {
                        var list = state.Cand[sole];
                        var keep = new List<RectI>(list.Count);
                        for (int i = 0; i < list.Count; i++)
                            if (list[i].Contains(col, row)) keep.Add(list[i]);
                        if (keep.Count < list.Count)
                        {
                            state.Cand[sole] = keep;
                            Add(2);
                            return true; // state shrank; loop restarts
                        }
                    }
                }
            }
            return false;
        }

        // -------- rule 3: eliminate candidates that make the rest unsolvable --------
        // Per GDD: drop possibilities that would cover another clue or make other cells
        // unreachable. Implemented as: assume candidate X, prune overlaps, and if the
        // remainder is provably unsolvable (empty candidate list / unreachable cell),
        // X cannot be the pick.

        private bool TryRule3(State state)
        {
            for (int c = 0; c < _clueCount; c++)
            {
                var list = state.Cand[c];
                if (state.Fixed[c] || list.Count == 0) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    var rect = list[i];
                    var probe = new State(state);
                    probe.Fix(c, rect);
                    probe.PruneAgainstFixed();
                    if (probe.IsDead())
                    {
                        state.RemoveCandidate(c, rect);
                        Add(3);
                        return true;
                    }
                }
            }
            return false;
        }

        // -------- rule 4: single-level trial-and-error --------
        // SOUNDNESS: an assumption that completes the solution proves nothing (another
        // assumption may also complete); only a contradiction eliminates a candidate.

        private bool TryRule4(State state)
        {
            // most constrained unfixed clue first
            int best = -1, bestCount = int.MaxValue;
            for (int c = 0; c < _clueCount; c++)
            {
                if (state.Fixed[c]) continue;
                int n = state.Cand[c].Count;
                if (n > 1 && n < bestCount) { bestCount = n; best = c; }
            }
            if (best < 0) return false;

            var snapshot = new List<RectI>(state.Cand[best]);
            bool acted = false;
            int othersAlive = snapshot.Count;
            for (int i = 0; i < snapshot.Count; i++)
            {
                var list = state.Cand[best];
                if (list.Count <= 1) break; // handled by rule 1

                var rect = snapshot[i];
                var probe = new State(state);
                probe.Fix(best, rect);
                var final = RunRules123(probe);

                if (final == SolveResult.Stuck && probe.IsDead())
                {
                    // contradiction under the assumption => `rect` is impossible
                    state.RemoveCandidate(best, rect);
                    Add(8);
                    acted = true;
                    othersAlive--;
                }
                // final == Solved or an inconclusive stuck point: no information; skip
            }
            return acted;
        }

        /// <summary>Rules 1-3 loop only (used inside a probe; no nested trials).</summary>
        private SolveResult RunRules123(State state)
        {
            while (true)
            {
                state.PruneAgainstFixed();
                if (state.IsDead()) return SolveResult.Stuck;
                if (state.IsFixedFully())
                    return state.AllCovered() ? SolveResult.Solved : SolveResult.Stuck;

                if (TryRule1(state)) continue;
                if (TryRule2(state)) continue;
                if (TryRule3(state)) continue;
                return SolveResult.Stuck;
            }
        }

        /// <summary>
        /// True when rejecting `rect` is justified: the probe dead-ends while some OTHER
        /// candidate for the same clue survives propagation. (Soundness: at most one of
        /// the surviving alternatives can be the solution; letting both live adds no info.
        /// Simplest correct rule: only drop when the probe itself proves a contradiction.)
        /// </summary>
        private bool HypothesisContradicted(State state, int clueIdx, RectI rect, State probe)
        {
            // The probe dead-ended. A dead end after *assuming* `rect` is only a sound
            // elimination if the dead end is a contradiction independent of remaining
            // choice — i.e. propagation hit a wall that no alternative could repair.
            // Since the probe ran to a fixed point of sound rules, a Stuck state with an
            // empty candidate list or an unreachable cell under this assumption means the
            // assumption itself was impossible.
            bool someOtherSurvives = state.Cand[clueIdx].Count > 1;
            bool probeDead = probe.IsDead();
            if (!probeDead)
            {
                // inconclusive stuck state: the puzzle resumes with fewer candidates —
                // keep them; the outer loop will re-run rules on the original state.
            }
            return probeDead && someOtherSurvives;
        }
    }
}
