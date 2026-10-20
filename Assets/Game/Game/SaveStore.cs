using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridCut.Game
{
    /// <summary>
    /// Persistent progress, localStorage-backed via PlayerPrefs. 
    /// UnlockedLevel = highest level the player may enter (1-based).
    /// BestStars[levelId] = 0..3, 0 when never finished.
    /// </summary>
    public static class SaveStore
    {
        public const int LevelCount = 150;

        public static int UnlockedLevel { get; private set; } = 1;
        public static bool SoundOn { get; private set; } = true;
        public static int ThemeIndex { get; private set; } = 0;

        static readonly Dictionary<int, int> _bestStars = new Dictionary<int, int>();

        public static void Load()
        {
            UnlockedLevel = Mathf.Clamp(PlayerPrefs.GetInt("gc.unlocked", 1), 1, LevelCount);
            SoundOn = PlayerPrefs.GetInt("gc.sound", 1) == 1;
            ThemeIndex = Mathf.Clamp(PlayerPrefs.GetInt("gc.theme", 0), 0, ThemeLibrary.All.Length - 1);
            _bestStars.Clear();
            string packed = PlayerPrefs.GetString("gc.stars", "");
            if (packed.Length == 0) return;
            var parts = packed.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                var kv = parts[i];
                int sep = kv.IndexOf(':');
                if (sep <= 0) continue;
                if (int.TryParse(kv.Substring(0, sep), out int id) && int.TryParse(kv.Substring(sep + 1), out int st))
                    _bestStars[id] = Mathf.Clamp(st, 0, 3);
            }
        }

        public static int BestStars(int levelId) =>
            _bestStars.TryGetValue(levelId, out int s) ? s : 0;

        /// <summary>Registers a finished level, keeps the best star count, unlocks the next level.</summary>
        public static int RecordWin(int levelId, int stars)
        {
            int prev = BestStars(levelId);
            if (stars > prev)
            {
                _bestStars[levelId] = stars;
                PersistStars();
            }
            if (levelId >= UnlockedLevel && levelId < LevelCount)
            {
                UnlockedLevel = levelId + 1;
                PlayerPrefs.SetInt("gc.unlocked", UnlockedLevel);
            }
            PlayerPrefs.Save();
            return Mathf.Max(prev, stars);
        }

        public static void SetSound(bool on)
        {
            SoundOn = on;
            PlayerPrefs.SetInt("gc.sound", on ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SetTheme(int index)
        {
            ThemeIndex = Mathf.Clamp(index, 0, ThemeLibrary.All.Length - 1);
            PlayerPrefs.SetInt("gc.theme", ThemeIndex);
            PlayerPrefs.Save();
        }

        public static void ResetProgress()
        {
            UnlockedLevel = 1;
            _bestStars.Clear();
            PlayerPrefs.DeleteKey("gc.unlocked");
            PlayerPrefs.DeleteKey("gc.stars");
            PlayerPrefs.Save();
        }

        static void PersistStars()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _bestStars)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append(':').Append(kv.Value);
            }
            PlayerPrefs.SetString("gc.stars", sb.ToString());
        }
    }
}
