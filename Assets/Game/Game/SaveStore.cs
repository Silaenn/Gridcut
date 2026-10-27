using System;
using System.Collections.Generic;
using UnityEngine;
using GridCut.Core;

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
        public static bool ReduceMotion { get; private set; } = false;
        public static int ThemesSeen { get; private set; } = 0;   // bitmask tema yang sudah dilihat
        public static float MusicVolume { get; private set; } = 0.7f;
        public static float SfxVolume { get; private set; } = 0.85f;
        public static bool TutorialDone { get; private set; } = false;

        static readonly Dictionary<int, int> _bestStars = new Dictionary<int, int>();

        public static void Load()
        {
            UnlockedLevel = Mathf.Clamp(PlayerPrefs.GetInt("gc.unlocked", 1), 1, LevelCount);
            SoundOn = PlayerPrefs.GetInt("gc.sound", 1) == 1;
            ThemeIndex = Mathf.Clamp(PlayerPrefs.GetInt("gc.theme", 0), 0, ThemeLibrary.All.Length - 1);
            ReduceMotion = PlayerPrefs.GetInt("gc.motion", 0) == 1;
            ThemesSeen = PlayerPrefs.GetInt("gc.themesSeen", 0);
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("gc.music", 0.7f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("gc.sfx", 0.85f));
            TutorialDone = PlayerPrefs.GetInt("gc.tutorialDone", 0) == 1;
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

        // ------------- state level yang sedang dimainkan (spec: otomatis tiap
        // perubahan, level terakhir saja, agar pemain menutup tab bisa lanjut) -----

        /// <summary>Format "{level};{hints};{x,y,w,h}..." — satu kunci kecil yang
        /// selalu tertimpa, sehingga simpanan hanya menyimpan state level terakhir.</summary>
        public static void SavePlayState(int levelId, int hints, IReadOnlyList<RectI> rects)
        {
            var sb = new System.Text.StringBuilder(128);
            sb.Append(levelId).Append(';').Append(Math.Max(0, hints));
            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                sb.Append(';').Append(r.X).Append(',').Append(r.Y)
                    .Append(',').Append(r.W).Append(',').Append(r.H);
            }
            PlayerPrefs.SetString("gc.play", sb.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>Load benar bila berisi level & hint yang masuk akal (rect yang
        /// ada kurang valid diperiksa kembali lapisan pemanggil saat dipasang).</summary>
        public static bool TryLoadPlayState(out int levelId, out int hints, out List<RectI> rects)
        {
            levelId = 0; hints = 0; rects = null;
            string s = PlayerPrefs.GetString("gc.play", "");
            if (string.IsNullOrEmpty(s)) return false;
            var parts = s.Split(';');
            if (parts.Length < 2 || !int.TryParse(parts[0], out levelId)
                || !int.TryParse(parts[1], out hints)) return false;
            if (levelId < 1 || levelId > LevelCount || hints < 0 || hints > PuzzleSession.HintLimit)
                return false;
            rects = new List<RectI>();
            for (int i = 2; i < parts.Length; i++)
            {
                var p = parts[i].Split(',');
                if (p.Length != 4
                    || !int.TryParse(p[0], out int x) || !int.TryParse(p[1], out int y)
                    || !int.TryParse(p[2], out int w) || !int.TryParse(p[3], out int h)) continue;
                if (w > 0 && h > 0) rects.Add(new RectI(x, y, w, h));
            }
            return true;
        }

        /// <summary>Level berasal dari play: tidak ada lagi state yang diselesaikan.</summary>
        public static void ClearPlayState()
        {
            if (PlayerPrefs.HasKey("gc.play"))
            {
                PlayerPrefs.DeleteKey("gc.play");
                PlayerPrefs.Save();
            }
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

        public static void SetReduceMotion(bool on)
        {
            ReduceMotion = on;
            PlayerPrefs.SetInt("gc.motion", on ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SetMusicVolume(float v)
        {
            MusicVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat("gc.music", MusicVolume);
            PlayerPrefs.Save();
        }

        public static void SetSfxVolume(float v)
        {
            SfxVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat("gc.sfx", SfxVolume);
            PlayerPrefs.Save();
        }

        public static void SetTutorialDone(bool done)
        {
            TutorialDone = done;
            PlayerPrefs.SetInt("gc.tutorialDone", done ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Spec bagian 10 (opsional): titik di tombol Themes bila ada tema
        /// baru yang terbuka tetapi belum pernah dilihat di popup.</summary>
        public static bool HasUnseenTheme()
        {
            int unlocked = UnlockedLevel;
            for (int i = 1; i < ThemeLibrary.All.Length; i++)
            {
                var th = ThemeLibrary.All[i];
                bool open = unlocked > th.UnlockLevel || th.UnlockLevel <= 1;
                if (open && (ThemesSeen & (1 << i)) == 0) return true;
            }
            return false;
        }

        public static void MarkThemesSeen()
        {
            int mask = ThemesSeen;
            for (int i = 1; i < ThemeLibrary.All.Length; i++)
            {
                var th = ThemeLibrary.All[i];
                bool open = UnlockedLevel > th.UnlockLevel || th.UnlockLevel <= 1;
                if (open) mask |= 1 << i;
            }
            if (mask == ThemesSeen) return;
            ThemesSeen = mask;
            PlayerPrefs.SetInt("gc.themesSeen", ThemesSeen);
            PlayerPrefs.Save();
        }

        public static void ResetProgress()
        {
            UnlockedLevel = 1;
            _bestStars.Clear();
            ThemesSeen = 0;
            TutorialDone = false;
            PlayerPrefs.DeleteKey("gc.unlocked");
            PlayerPrefs.DeleteKey("gc.stars");
            PlayerPrefs.DeleteKey("gc.themesSeen");
            PlayerPrefs.DeleteKey("gc.tutorialDone");
            PlayerPrefs.Save();
            ClearPlayState();   // state level yang sedang berjalan ikut dihapus
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
