using System;
using System.Collections.Generic;
using System.Text;
using ToolGood.Words.Pinyin;

namespace HyperResearch.Utils;

/// <summary>
/// Pinyin matching for item names and tooltip lines, backed by ToolGood.Words.Pinyin.
/// Supports full pinyin, initials, substrings and mixed Chinese/pinyin queries.
/// </summary>
public static class PinyinMatcher
{
    private const int MaxCacheEntries = 50000;
    private const int MaxAlternatePinyins = 8;
    private const int MaxAlternatePinyinTextLength = 24;

    private static readonly Dictionary<string, TextIndex> Cache = new(StringComparer.Ordinal);
    private static readonly object CacheLock = new();

    public static void ClearCache()
    {
        lock (CacheLock)
        {
            Cache.Clear();
        }
    }

    /// <summary>
    /// Returns true when <paramref name="query"/> matches <paramref name="text"/> as plain text or as pinyin.
    /// </summary>
    public static bool Matches(string? text, string? query)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query)) return false;

        if (text.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;

        if (!WordsHelper.HasChinese(text)) return false;

        string normalizedQuery = Normalize(query);
        if (normalizedQuery.Length == 0) return false;

        return GetOrBuildIndex(text).Matches(normalizedQuery);
    }

    private static TextIndex GetOrBuildIndex(string text)
    {
        lock (CacheLock)
        {
            if (Cache.TryGetValue(text, out TextIndex? cached)) return cached;
        }

        TextIndex index = TextIndex.Build(text);

        lock (CacheLock)
        {
            if (Cache.Count < MaxCacheEntries) Cache[text] = index;
        }

        return index;
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char raw in value)
        {
            char c = raw;

            // Full-width ASCII -> half-width
            if (c >= 'Ａ' && c <= 'Ｚ') c = (char)('A' + (c - 'Ａ'));
            else if (c >= 'ａ' && c <= 'ｚ') c = (char)('a' + (c - 'ａ'));

            // u-umlaut -> v
            if (c is 'ü' or 'ǖ' or 'ǘ' or 'ǚ' or 'ǜ') c = 'v';

            if (char.IsWhiteSpace(c) || c is '\'' or '’') continue;

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    private static string NormalizeReading(string reading) => Normalize(reading);

    private static bool IsCjk(char c) => c is >= '\u3400' and <= '\u9FFF' or >= '\uF900' and <= '\uFAFF';

    private static void AddDistinct(List<string> list, string value)
    {
        if (value.Length == 0 || list.Contains(value)) return;
        list.Add(value);
    }

    private static string[] GetReadings(char c, string? preferred)
    {
        var readings = new List<string>(4);

        if (preferred is not null) AddDistinct(readings, NormalizeReading(preferred));

        if (IsCjk(c))
        {
            List<string> all = WordsHelper.GetAllPinyin(c, false);
            foreach (string reading in all) AddDistinct(readings, NormalizeReading(reading));
        }

        if (readings.Count == 0) readings.Add(Normalize(c.ToString()));

        return readings.ToArray();
    }

    private static int CommonPrefixLength(string reading, string query, int queryIndex)
    {
        int max = Math.Min(reading.Length, query.Length - queryIndex);
        int i = 0;
        while (i < max && reading[i] == query[queryIndex + i]) i++;
        return i;
    }

    private sealed class TextIndex
    {
        private readonly char[] _chars;
        private readonly string[][] _readings;
        private readonly string _full;
        private readonly string _initials;
        private readonly string[] _alternateFull;
        private readonly string[] _alternateInitials;

        private TextIndex(char[] chars, string[][] readings, string full, string initials,
            string[] alternateFull, string[] alternateInitials)
        {
            _chars = chars;
            _readings = readings;
            _full = full;
            _initials = initials;
            _alternateFull = alternateFull;
            _alternateInitials = alternateInitials;
        }

        public static TextIndex Build(string text)
        {
            string[] preferredList = WordsHelper.GetPinyinList(text, false) ?? [];
            if (preferredList.Length != text.Length) preferredList = [];

            var chars = new List<char>(text.Length);
            var readings = new List<string[]>(text.Length);
            var preferred = new List<string>(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) continue;

                string? preferredReading = i < preferredList.Length && preferredList[i].Length > 0
                    ? preferredList[i]
                    : null;

                string[] charReadings = GetReadings(c, preferredReading);

                chars.Add(c);
                readings.Add(charReadings);
                preferred.Add(preferredReading is not null ? NormalizeReading(preferredReading) : charReadings[0]);
            }

            string full = string.Concat(preferred);

            var initialsBuilder = new StringBuilder(preferred.Count);
            foreach (string reading in preferred)
                initialsBuilder.Append(reading.Length > 0 ? reading[0] : ' ');

            string initials = initialsBuilder.ToString();

            BuildAlternates(readings, full, initials, out string[] alternateFull, out string[] alternateInitials);

            return new TextIndex(chars.ToArray(), readings.ToArray(), full, initials, alternateFull,
                alternateInitials);
        }

        private static void BuildAlternates(List<string[]> readings, string full, string initials,
            out string[] alternateFull, out string[] alternateInitials)
        {
            alternateFull = [];
            alternateInitials = [];

            bool hasAlternates = false;
            foreach (string[] charReadings in readings)
            {
                if (charReadings.Length > 1)
                {
                    hasAlternates = true;
                    break;
                }
            }

            if (!hasAlternates || readings.Count > MaxAlternatePinyinTextLength) return;

            var combinations = new List<(string Full, string Initials)>(MaxAlternatePinyins) { ("", "") };

            foreach (string[] charReadings in readings)
            {
                var next = new List<(string Full, string Initials)>(combinations.Count * charReadings.Length);
                foreach ((string comboFull, string comboInitials) in combinations)
                {
                    foreach (string reading in charReadings)
                    {
                        next.Add((comboFull + reading, comboInitials + (reading.Length > 0 ? reading[0] : ' ')));
                        if (next.Count >= MaxAlternatePinyins) break;
                    }

                    if (next.Count >= MaxAlternatePinyins) break;
                }

                combinations = next;
            }

            var fulls = new List<string>(combinations.Count);
            var initialsList = new List<string>(combinations.Count);
            foreach ((string comboFull, string comboInitials) in combinations)
            {
                if (comboFull == full || comboInitials == initials) continue;
                AddDistinct(fulls, comboFull);
                AddDistinct(initialsList, comboInitials);
            }

            alternateFull = fulls.ToArray();
            alternateInitials = initialsList.ToArray();
        }

        public bool Matches(string query)
        {
            if (_full.Contains(query, StringComparison.Ordinal)) return true;
            if (_initials.Contains(query, StringComparison.Ordinal)) return true;

            foreach (string alternate in _alternateFull)
                if (alternate.Contains(query, StringComparison.Ordinal))
                    return true;

            foreach (string alternate in _alternateInitials)
                if (alternate.Contains(query, StringComparison.Ordinal))
                    return true;

            return MixedMatch(query);
        }

        /// <summary>
        /// Matches queries that mix Chinese characters and pinyin, e.g. "土k" or "t块".
        /// </summary>
        private bool MixedMatch(string query)
        {
            return Walk(0, 0);

            bool Walk(int positionIndex, int queryIndex)
            {
                if (queryIndex >= query.Length) return true;
                if (positionIndex >= _chars.Length) return false;

                char queryChar = query[queryIndex];
                if (queryChar == _chars[positionIndex] && Walk(positionIndex + 1, queryIndex + 1))
                    return true;

                foreach (string reading in _readings[positionIndex])
                {
                    int length = CommonPrefixLength(reading, query, queryIndex);
                    if (length > 0 && Walk(positionIndex + 1, queryIndex + length))
                        return true;
                }

                return false;
            }
        }
    }
}