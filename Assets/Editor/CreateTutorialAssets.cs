using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GridCut.Core;
using GridCut.Game;

/// <summary>Membuat aset Resources/Tutorial/Lesson1..5 untuk spec bagian 13.</summary>
public static class CreateTutorialAssets
{
    const string Dir = "Assets/Game/Content/Resources/Tutorial";

    [MenuItem("GridCut/Create Tutorial Lessons")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder(Dir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Content/Resources"))
                AssetDatabase.CreateFolder("Assets/Game/Content", "Resources");
            AssetDatabase.CreateFolder("Assets/Game/Content/Resources", "Tutorial");
        }

        Make(1, 3, 1, new[] { 1 }, new[] { 0 }, new[] { 3 }, new[] { 0 },
            new[] { 0 }, new[] { 0 }, new[] { 3 }, new[] { 1 },
            null, TutorialLesson.ActionKind.DragTarget,
            "Drag across all 3 squares. The number shows how many.",
            "Nice! 3 squares for the number 3.", "",
            true, false, 0, 0, 2, 0, 0.8f, false);

        Make(2, 3, 2, new[] { 1 }, new[] { 1 }, new[] { 6 }, new[] { 1 },
            new[] { 0 }, new[] { 0 }, new[] { 3 }, new[] { 2 },
            null, TutorialLesson.ActionKind.DragTarget,
            "Rectangles can be wide and tall. Drag from corner to corner.",
            "Great! 6 squares for the number 6.", "",
            true, false, 0, 0, 2, 1, 2f, false);

        Make(3, 3, 2, new[] { 0, 2 }, new[] { 0, 1 }, new[] { 2, 4 }, new[] { 0, 2 },
            new[] { 0, 1 }, new[] { 0, 0 }, new[] { 1, 2 }, new[] { 2, 2 },
            null, TutorialLesson.ActionKind.Solve,
            "Cover every square. Each rectangle holds exactly one number.",
            "Perfect! Every square is covered.", "",
            true, false, 0, 0, 0, 1, 2f, false);

        Make(4, 3, 3, new[] { 0, 2, 0 }, new[] { 0, 1, 2 }, new[] { 2, 3, 4 }, new[] { 0, 2, 4 },
            new[] { 0, 2, 0 }, new[] { 0, 0, 1 }, new[] { 2, 1, 2 }, new[] { 1, 3, 2 },
            new[] { 0, 1, 2, 1 }, TutorialLesson.ActionKind.TapTarget,
            "Tap a rectangle to remove it. Then finish the puzzle.",
            "You fixed it!", "Now finish the puzzle.",
            true, true, 0, 1, 0, 1, 2f, false);

        Lesson5();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Tutorial] Lessons created");
    }

    static void Make(int index, int cols, int rows,
        int[] cc, int[] cr, int[] cv, int[] ccol,
        int[] sx, int[] sy, int[] sw, int[] sh, int[] init,
        TutorialLesson.ActionKind action, string instruction, string success, string after,
        bool demo, bool demoTap, int dsc, int dsr, int dec, int der, float demoDelay, bool hint)
    {
        var l = ScriptableObject.CreateInstance<TutorialLesson>();
        l.Index = index; l.Cols = cols; l.Rows = rows;
        l.ClueCols = cc; l.ClueRows = cr; l.ClueValues = cv; l.ClueColors = ccol;
        l.SolX = sx; l.SolY = sy; l.SolW = sw; l.SolH = sh;
        if (init != null && init.Length >= 4)
        {
            l.InitX = new[] { init[0] }; l.InitY = new[] { init[1] };
            l.InitW = new[] { init[2] }; l.InitH = new[] { init[3] };
        }
        l.Action = action;
        l.Instruction = instruction; l.Success = success; l.AfterRemoval = after;
        l.HasDemo = demo; l.DemoTap = demoTap;
        l.DemoStartCol = dsc; l.DemoStartRow = dsr; l.DemoEndCol = dec; l.DemoEndRow = der;
        l.DemoDelay = demoDelay; l.HintEnabled = hint;
        Save(l, $"Lesson{index}");
    }

    static void Lesson5()
    {
        GenAttempt best = null;
        for (ulong seed = 1; seed <= 20000; seed++)
        {
            var a = PuzzleGenerator.Generate(4, 4, seed);
            if (!a.Ok) continue;
            int n = a.Puzzle.ClueCount;
            if (n < 4 || n > 5) continue;
            if (best == null || a.Score < best.Score)
            {
                best = a;
                if (a.Score <= 1f) break;
            }
        }
        if (best == null)
        {
            Debug.LogError("[Tutorial] tidak menemukan puzzle 4x4 4-5 clue");
            return;
        }
        var p = best.Puzzle;
        var colors = LevelColors.AssignColorIndices(p, best.Solution);
        var l = ScriptableObject.CreateInstance<TutorialLesson>();
        l.Index = 5; l.Cols = 4; l.Rows = 4;
        l.ClueCols = new int[p.ClueCount]; l.ClueRows = new int[p.ClueCount];
        l.ClueValues = new int[p.ClueCount]; l.ClueColors = new int[p.ClueCount];
        l.SolX = new int[p.ClueCount]; l.SolY = new int[p.ClueCount];
        l.SolW = new int[p.ClueCount]; l.SolH = new int[p.ClueCount];
        for (int i = 0; i < p.ClueCount; i++)
        {
            l.ClueCols[i] = p.Clues[i].Col; l.ClueRows[i] = p.Clues[i].Row;
            l.ClueValues[i] = p.Clues[i].Value; l.ClueColors[i] = colors[i];
            var r = best.Solution[i];
            l.SolX[i] = r.X; l.SolY[i] = r.Y; l.SolW[i] = r.W; l.SolH[i] = r.H;
        }
        l.Action = TutorialLesson.ActionKind.Solve;
        l.Instruction = "Your turn! Finish the puzzle.";
        l.Success = "You're ready!";
        l.AfterRemoval = "";
        l.HasDemo = false;
        l.HintEnabled = true;
        l.HintDelay = 8f;
        Save(l, "Lesson5");
        Debug.Log($"[Tutorial] Lesson5 seed={best.Seed} clues={p.ClueCount} score={best.Score:0.00}");
    }

    static void Save(TutorialLesson l, string name)
    {
        string path = $"{Dir}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<TutorialLesson>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(l, path);
    }
}