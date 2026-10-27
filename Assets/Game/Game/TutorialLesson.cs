using UnityEngine;
using GridCut.Core;

namespace GridCut.Game
{
    /// <summary>Data satu pelajaran tutorial (spec bagian 13). Satu aset per pelajaran.</summary>
    [CreateAssetMenu(menuName = "GridCut/Tutorial Lesson", fileName = "Lesson")]
    public sealed class TutorialLesson : ScriptableObject
    {
        public enum ActionKind { DragTarget = 0, TapTarget = 1, Solve = 2 }

        public int Index;
        public int Cols;
        public int Rows;

        [Header("Clue & solusi (indeks sejajar)")]
        public int[] ClueCols = new int[0];
        public int[] ClueRows = new int[0];
        public int[] ClueValues = new int[0];
        public int[] ClueColors = new int[0];
        public int[] SolX = new int[0];
        public int[] SolY = new int[0];
        public int[] SolW = new int[0];
        public int[] SolH = new int[0];

        [Header("Keping awal (netral)")]
        public int[] InitX = new int[0];
        public int[] InitY = new int[0];
        public int[] InitW = new int[0];
        public int[] InitH = new int[0];

        [Header("Aksi & teks")]
        public ActionKind Action = ActionKind.DragTarget;
        [TextArea] public string Instruction;
        [TextArea] public string Success;
        [TextArea] public string AfterRemoval;

        [Header("Demo")]
        public bool HasDemo = true;
        public bool DemoTap;
        public int DemoStartCol;
        public int DemoStartRow;
        public int DemoEndCol;
        public int DemoEndRow;
        public float DemoDelay = 4f;

        [Header("Hint")]
        public bool HintEnabled;
        public float HintDelay = 8f;

        public Clue[] BuildClues()
        {
            int n = Mathf.Min(ClueCols.Length,
                Mathf.Min(ClueRows.Length, Mathf.Min(ClueValues.Length, ClueColors.Length)));
            var clues = new Clue[n];
            for (int i = 0; i < n; i++)
                clues[i] = new Clue(ClueCols[i], ClueRows[i], ClueValues[i], ClueColors[i]);
            return clues;
        }

        public RectI[] BuildSolution()
        {
            int n = Mathf.Min(SolX.Length,
                Mathf.Min(SolY.Length, Mathf.Min(SolW.Length, SolH.Length)));
            var rects = new RectI[n];
            for (int i = 0; i < n; i++)
                rects[i] = new RectI(SolX[i], SolY[i], SolW[i], SolH[i]);
            return rects;
        }

        public RectI[] BuildInitial()
        {
            int n = Mathf.Min(InitX.Length,
                Mathf.Min(InitY.Length, Mathf.Min(InitW.Length, InitH.Length)));
            var rects = new RectI[n];
            for (int i = 0; i < n; i++)
                rects[i] = new RectI(InitX[i], InitY[i], InitW[i], InitH[i]);
            return rects;
        }

        public LevelData BuildLevel() => new LevelData
        {
            Id = 900 + Index,
            Cols = Cols,
            Rows = Rows,
            IsTutorial = true,
            Clues = BuildClues(),
            Solution = BuildSolution(),
        };
    }
}