using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GridCut.Core
{
    /// <summary>One exported level. Clues and Solution use the same order.</summary>
    public sealed class LevelData
    {
        public int Id;
        public int Cols;
        public int Rows;
        public ulong Seed;
        public float Score;
        public bool IsTutorial;
        public Clue[] Clues;
        public RectI[] Solution;
    }

    /// <summary>
    /// Manual, allocation-light JSON writer/parser for the level file.
    /// Format (one file, all levels):
    /// { "version":1, "levels":[{"id":1,"cols":4,"rows":4,"tutorial":false,"seed":123,"score":1.0,
    ///   "clues":[{"c":0,"r":0,"v":1,"color":0}], "solution":[{"x":0,"y":0,"w":1,"h":1}] }] }
    /// </summary>
    public static class LevelJson
    {
        public const int Version = 1;

        public static string Write(IList<LevelData> levels)
        {
            var sb = new StringBuilder(64 * 1024);
            sb.Append("{\"version\":").Append(Version).Append(",\"levels\":[");
            var num = new NumberFormatInfo();
            for (int li = 0; li < levels.Count; li++)
            {
                var l = levels[li];
                if (li > 0) sb.Append(',');
                sb.Append("{\"id\":").Append(l.Id);
                sb.Append(",\"cols\":").Append(l.Cols);
                sb.Append(",\"rows\":").Append(l.Rows);
                sb.Append(",\"tutorial\":").Append(l.IsTutorial ? "true" : "false");
                sb.Append(",\"seed\":").Append(l.Seed.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"score\":").Append(l.Score.ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append(",\"clues\":[");
                for (int c = 0; c < l.Clues.Length; c++)
                {
                    var clue = l.Clues[c];
                    if (c > 0) sb.Append(',');
                    sb.Append("{\"c\":").Append(clue.Col)
                        .Append(",\"r\":").Append(clue.Row)
                        .Append(",\"v\":").Append(clue.Value)
                        .Append(",\"color\":").Append(clue.ColorIndex).Append('}');
                }
                sb.Append("],\"solution\":[");
                for (int s = 0; s < l.Solution.Length; s++)
                {
                    var r = l.Solution[s];
                    if (s > 0) sb.Append(',');
                    sb.Append("{\"x\":").Append(r.X)
                        .Append(",\"y\":").Append(r.Y)
                        .Append(",\"w\":").Append(r.W)
                        .Append(",\"h\":").Append(r.H).Append('}');
                }
                sb.Append("]}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        // ------------------------- parser -------------------------

        public sealed class JsonReader
        {
            private readonly string _s;
            private int _i;
            public JsonReader(string s) { _s = s ?? ""; }

            public void Whitespace()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            public void Expect(char c)
            {
                Whitespace();
                if (_i >= _s.Length || _s[_i] != c) Fail($"expected '{c}'");
                _i++;
            }

            public bool TryConsumeWord(string word)
            {
                Whitespace();
                if (_i + word.Length <= _s.Length && string.CompareOrdinal(_s, _i, word, 0, word.Length) == 0)
                {
                    _i += word.Length;
                    return true;
                }
                return false;
            }

            public string GetString()
            {
                Whitespace();
                if (_i >= _s.Length || _s[_i] != '"') Fail("expected string");
                _i++;
                int start = _i;
                while (_i < _s.Length && _s[_i] != '"') _i++;
                var v = _s.Substring(start, _i - start);
                if (_i < _s.Length) _i++;
                return v;
            }

            public int GetInt()
            {
                Whitespace();
                int start = _i;
                if (_i < _s.Length && (_s[_i] == '-' || _s[_i] == '+')) _i++;
                while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                if (_i == start) Fail("expected int");
                return int.Parse(_s.Substring(start, _i - start), CultureInfo.InvariantCulture);
            }

            public long GetLong()
            {
                Whitespace();
                int start = _i;
                if (_i < _s.Length && (_s[_i] == '-' || _s[_i] == '+')) _i++;
                while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                if (_i == start) Fail("expected long");
                return long.Parse(_s.Substring(start, _i - start), CultureInfo.InvariantCulture);
            }

            public float GetFloat()
            {
                Whitespace();
                int start = _i;
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '-' || _s[_i] == '+' ||
                                          _s[_i] == '.' || _s[_i] == 'e' || _s[_i] == 'E'))
                    _i++;
                if (_i == start) Fail("expected number");
                return float.Parse(_s.Substring(start, _i - start), CultureInfo.InvariantCulture);
            }

            public char Peek()
            {
                Whitespace();
                return _i < _s.Length ? _s[_i] : '\0';
            }

            private void Fail(string why)
            {
                throw new FormatException($"LevelJson parse error at {_i}: {why}");
            }
        }

        public static IList<LevelData> Read(string json)
        {
            var reader = new JsonReader(json);
            var levels = new List<LevelData>(150);

            reader.Expect('{');
            reader.TryConsumeWord("\"version\"");
            reader.Expect(':');
            reader.GetInt(); // version (accepted for forward-compat; caller may verify)
            reader.Expect(',');
            reader.TryConsumeWord("\"levels\"");
            reader.Expect(':');
            reader.Expect('[');

            while (reader.Peek() == '{')
            {
                levels.Add(ParseLevel(reader));

                if (reader.Peek() == ',')
                {
                    reader.Expect(',');
                    continue;
                }
                break;
            }

            reader.Expect(']');
            reader.Expect('}');
            return levels;
        }

        private static LevelData ParseLevel(JsonReader reader)
        {
            var level = new LevelData();
            reader.Expect('{');
            while (true)
            {
                string key = reader.GetString();
                reader.Expect(':');
                switch (key)
                {
                    case "id": level.Id = reader.GetInt(); break;
                    case "cols": level.Cols = reader.GetInt(); break;
                    case "rows": level.Rows = reader.GetInt(); break;
                    case "tutorial": level.IsTutorial = reader.TryConsumeWord("true"); if (!level.IsTutorial) reader.TryConsumeWord("false"); break;
                    case "seed": level.Seed = (ulong)reader.GetLong(); break;
                    case "score": level.Score = reader.GetFloat(); break;
                    case "clues": level.Clues = ParseClues(reader); break;
                    case "solution": level.Solution = ParseRects(reader); break;
                    default: SkipValue(reader); break;
                }

                if (reader.Peek() == ',') { reader.Expect(','); continue; }
                break;
            }
            reader.Expect('}');
            return level;
        }

        private static Clue[] ParseClues(JsonReader reader)
        {
            reader.Expect('[');
            var list = new List<Clue>(16);
            while (reader.Peek() == '{')
            {
                reader.Expect('{');
                int c = 0, r = 0, v = 0, color = 0;
                bool first = true;
                while (true)
                {
                    if (!first) reader.Expect(',');
                    first = false;
                    string key = reader.GetString();
                    reader.Expect(':');
                    switch (key)
                    {
                        case "c": c = reader.GetInt(); break;
                        case "r": r = reader.GetInt(); break;
                        case "v": v = reader.GetInt(); break;
                        case "color": color = reader.GetInt(); break;
                        default: SkipValue(reader); break;
                    }
                    if (reader.Peek() == ',') continue;
                    break;
                }
                reader.Expect('}');
                list.Add(new Clue(c, r, v, color));
                if (reader.Peek() == ',') { reader.Expect(','); continue; }
                break;
            }
            reader.Expect(']');
            return list.ToArray();
        }

        private static RectI[] ParseRects(JsonReader reader)
        {
            reader.Expect('[');
            var list = new List<RectI>(16);
            while (reader.Peek() == '{')
            {
                reader.Expect('{');
                int x = 0, y = 0, w = 0, h = 0;
                bool first = true;
                while (true)
                {
                    if (!first) reader.Expect(',');
                    first = false;
                    string key = reader.GetString();
                    reader.Expect(':');
                    switch (key)
                    {
                        case "x": x = reader.GetInt(); break;
                        case "y": y = reader.GetInt(); break;
                        case "w": w = reader.GetInt(); break;
                        case "h": h = reader.GetInt(); break;
                        default: SkipValue(reader); break;
                    }
                    if (reader.Peek() == ',') continue;
                    break;
                }
                reader.Expect('}');
                list.Add(new RectI(x, y, w, h));
                if (reader.Peek() == ',') { reader.Expect(','); continue; }
                break;
            }
            reader.Expect(']');
            return list.ToArray();
        }

        private static void SkipValue(JsonReader reader)
        {
            char p = reader.Peek();
            if (p == '"') { reader.GetString(); return; }
            if (p == '{' || p == '[')
            {
                // skip a whole container by bracket matching
                char open = p;
                char close = p == '{' ? '}' : ']';
                reader.Expect(open);
                while (true)
                {
                    p = reader.Peek();
                    if (p == open) { reader.Expect(open); continue; }
                    if (p == close) { reader.Expect(close); return; }
                    if (p == ',') { reader.Expect(','); continue; }
                    reader.GetInt(); // consume a number/word chunk
                }
            }
            reader.GetFloat();
        }
    }
}
