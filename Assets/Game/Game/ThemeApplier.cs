using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridCut.Game
{
    /// <summary>
    /// Central theme state. UI elements register an updater per color role; when the
    /// theme changes every updater re-runs. Roles: Background, CellEmpty, GridLine,
    /// Text, Accent and one of the 6 rect colors.
    /// </summary>
    public static class ThemeApplier
    {
        public enum Role { Background, CellEmpty, GridLine, Text, Accent, Rect }

        public static ThemeLibrary.Theme Current { get; private set; } = ThemeLibrary.Paper;

        static readonly List<(Role role, Action<Color> cb)> _subscribers = new List<(Role, Action<Color>)>();

#if UNITY_EDITOR
        // Play-mode without domain reload keeps statics across sessions; stale
        // closures would then touch destroyed objects. Clear them on load.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSubscribers() => _subscribers.Clear();
#endif

        public static Color ColorOf(Role role, int rectIndex = 0)
        {
            return role switch
            {
                Role.Background => Parse(Current.Background),
                Role.CellEmpty => Parse(Current.CellEmpty),
                Role.GridLine => Parse(Current.GridLine),
                Role.Text => Parse(Current.Text),
                Role.Accent => Parse(Current.Accent),
                Role.Rect => Parse(Current.Rects[Mathf.Clamp(rectIndex, 0, Current.Rects.Length - 1)]),
                _ => Color.white,
            };
        }

        public static void Subscribe(Role role, Action<Color> updater, bool applyImmediately) 
        {
            // idempotent: screens re-subscribe on every enable; never stack duplicates,
            // and an updater moved to another role drops its stale entry
            for (int i = _subscribers.Count - 1; i >= 0; i--)
            {
                if (_subscribers[i].cb != updater) continue;
                if (_subscribers[i].role == role)
                {
                    if (applyImmediately) updater(ColorOf(role));
                    return;
                }
                _subscribers.RemoveAt(i);
            }
            _subscribers.Add((role, updater));
            if (applyImmediately) updater(ColorOf(role));
        }

        public static void Unsubscribe(Action<Color> updater)
        {
            for (int i = _subscribers.Count - 1; i >= 0; i--)
                if (_subscribers[i].cb == updater) _subscribers.RemoveAt(i);
        }

        public static void Apply(int themeIndex)
        {
            SaveStore.SetTheme(themeIndex);
            Current = ThemeLibrary.All[themeIndex];
            foreach (var (role, cb) in _subscribers) cb(ColorOf(role));
        }

        /// <summary>Applies the stored selection without re-saving (boot path).</summary>
        public static void ApplySaved()
        {
            Current = ThemeLibrary.All[Mathf.Clamp(SaveStore.ThemeIndex, 0, ThemeLibrary.All.Length - 1)];
            foreach (var (role, cb) in _subscribers) cb(ColorOf(role));
        }

        public static Color Parse(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }
}
