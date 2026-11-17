using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Veilward.Infrastructure
{
    // Проверка формы до JsonUtility: пропущенные нули, overflow, duplicate keys и неверные JSON-типы не становятся defaults.
    internal sealed class WaveCheckpointJson
    {
        private readonly string _json;
        private int _position;
        private WaveCheckpointJson(string json) { _json = json; }
        private sealed class Number { public string Text; }

        internal static bool HasRequiredFields(string json, out string error)
        {
            try
            {
                var reader = new WaveCheckpointJson(json);
                var root = Object(reader.Read(0)); reader.White();
                if (reader._position != json.Length) throw new FormatException();
                Int(root, "schemaVersion"); String(root, "scenarioContentId");
                var schema = long.Parse(((Number)root["schemaVersion"]).Text, CultureInfo.InvariantCulture);
                var state = Object(Field(root, "state"));
                foreach (var key in new[] { "phase", "waveIndex", "baseHealth", "gold", "kills", "nextEnemyId", "nextTowerId" }) Int(state, key);
                Int(state, "tick", true);
                foreach (var value in Array(Field(state, "towers")))
                {
                    var tower = Object(value);
                    foreach (var key in new[] { "towerId", "x", "y", "level", "investedGold" }) Int(tower, key);
                    Int(tower, "nextAttackTick", true);
                    if (schema != 1) String(tower, "towerTypeId");
                }
                if (schema == 3)
                {
                    var p = Object(Field(state, "elementProgression"));
                    foreach (var key in new[] { "rulesId", "startResonanceId", "pendingChoiceId" }) String(p, key);
                    Int(p, "choicesEarned"); Int(p, "choicesSpent");
                    foreach (var id in Array(Field(p, "unlockedElementIds"))) if (!(id is string)) throw new FormatException();
                    foreach (var choice in Array(Field(p, "spentChoices")))
                    { var c = Object(choice); String(c, "choiceId"); String(c, "elementId"); }
                }
                error = null;
                return true;
            }
            catch (Exception e) when (e is FormatException || e is OverflowException || e is ArgumentException)
            { error = "Checkpoint JSON has malformed or missing required fields."; return false; }
        }

        private static object Field(Dictionary<string, object> obj, string key)
        { if (!obj.TryGetValue(key, out var value) || value == null) throw new FormatException(); return value; }
        private static Dictionary<string, object> Object(object value) => value as Dictionary<string, object> ?? throw new FormatException();
        private static List<object> Array(object value) => value as List<object> ?? throw new FormatException();
        private static void String(Dictionary<string, object> obj, string key)
        { if (!(Field(obj, key) is string)) throw new FormatException(); }
        private static void Int(Dictionary<string, object> obj, string key, bool wide = false)
        {
            if (!(Field(obj, key) is Number n) || !long.TryParse(n.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ||
                (!wide && (value < int.MinValue || value > int.MaxValue))) throw new FormatException();
        }
        private void White() { while (_position < _json.Length && " \t\r\n".IndexOf(_json[_position]) >= 0) _position++; }
        private char Take() { if (_position >= _json.Length) throw new FormatException(); return _json[_position++]; }
        private bool Eat(char c) { White(); if (_position < _json.Length && _json[_position] == c) { _position++; return true; } return false; }
        private void Require(char c) { if (!Eat(c)) throw new FormatException(); }
        private object Read(int depth)
        {
            if (depth > 32) throw new FormatException();
            White(); var c = Take();
            if (c == '{')
            {
                var obj = new Dictionary<string, object>(StringComparer.Ordinal);
                if (Eat('}')) return obj;
                do
                {
                    White(); if (Take() != '"') throw new FormatException(); var key = ReadString(); Require(':');
                    if (obj.ContainsKey(key)) throw new FormatException(); obj.Add(key, Read(depth + 1));
                    if (Eat('}')) return obj;
                } while (Eat(','));
                throw new FormatException();
            }
            if (c == '[')
            {
                var list = new List<object>(); if (Eat(']')) return list;
                do { list.Add(Read(depth + 1)); if (Eat(']')) return list; } while (Eat(','));
                throw new FormatException();
            }
            if (c == '"') return ReadString();
            if (c == 'n') { Literal("ull"); return null; }
            if (c == 't') { Literal("rue"); return true; }
            if (c == 'f') { Literal("alse"); return false; }
            var start = _position - 1;
            if (c == '-') c = Take();
            if (c < '0' || c > '9') throw new FormatException();
            if (c != '0') while (_position < _json.Length && _json[_position] >= '0' && _json[_position] <= '9') _position++;
            if (_position < _json.Length && _json[_position] == '.')
            { _position++; Digits(); }
            if (_position < _json.Length && (_json[_position] == 'e' || _json[_position] == 'E'))
            { _position++; if (_position < _json.Length && (_json[_position] == '+' || _json[_position] == '-')) _position++; Digits(); }
            return new Number { Text = _json.Substring(start, _position - start) };
        }
        private void Digits()
        { var start = _position; while (_position < _json.Length && _json[_position] >= '0' && _json[_position] <= '9') _position++; if (start == _position) throw new FormatException(); }
        private void Literal(string tail) { foreach (var c in tail) if (Take() != c) throw new FormatException(); }
        private string ReadString()
        {
            var result = new StringBuilder();
            while (true)
            {
                var c = Take(); if (c == '"') return result.ToString(); if (c < 32) throw new FormatException();
                if (c != '\\') { result.Append(c); continue; }
                c = Take();
                switch (c)
                {
                    case '"': case '\\': case '/': result.Append(c); break;
                    case 'b': result.Append('\b'); break; case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break; case 'r': result.Append('\r'); break; case 't': result.Append('\t'); break;
                    case 'u':
                        var hex = new string(new[] { Take(), Take(), Take(), Take() });
                        result.Append((char)ushort.Parse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)); break;
                    default: throw new FormatException();
                }
            }
        }
    }
}
