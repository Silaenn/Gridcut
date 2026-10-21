using System.Collections.Generic;
using GridCut.Core;
using UnityEngine;

namespace GridCut.Game
{
    /// <summary>Loads the exported level set from Resources and exposes lookups.</summary>
    public static class LevelRepository
    {
        static Dictionary<int, LevelData> _byId;

        public static IList<LevelData> All { get; private set; }

        public static void Load()
        {
            var asset = Resources.Load<TextAsset>("levels");
            if (asset == null)
            {
                Debug.LogError("[LevelRepository] Resources levels.json not found — build will have no levels.");
                All = new LevelData[0];
                _byId = new Dictionary<int, LevelData>();
                return;
            }
            var list = new List<LevelData>(LevelJson.Read(asset.text));
            _byId = new Dictionary<int, LevelData>(list.Count);
            foreach (var l in list) _byId[l.Id] = l;
            All = list;
        }

        public static LevelData Get(int levelId) =>
            _byId != null && _byId.TryGetValue(levelId, out var l) ? l : null;

        public static int Count => All != null ? All.Count : 0;

        /// <summary>Localized difficulty 1..10: the level's TARGET score on the spec
        /// difficulty curve (linear rise, breathers −30%, dip at each new board size).
        /// Raw solver score stays in LevelData.Score for the export report — solver
        /// raws floor-mass on small boards and would show a flat "1" for the whole
        /// early game.</summary>
        public static string ScoreLabel(LevelData l) => LevelPlan.TargetScore(l.Id).ToString("0.#");
    }
}
