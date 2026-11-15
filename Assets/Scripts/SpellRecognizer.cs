namespace JunkyWizard.Prototype
{
    public struct RecognizedSpell
    {
        public Spell Spell;
        /// <summary>Spell letters / buffer letters (a powered spell counts its letters twice).</summary>
        public float Precision;
        /// <summary>The word appears twice as disjoint, ordered subsequences.</summary>
        public bool Powered;
    }

    /// <summary>
    /// GridV3 spell recognition: a spell is recognised when its word is a subsequence of the buffer
    /// (extra letters in between or around are allowed).
    /// </summary>
    public static class SpellRecognizer
    {
        /// <param name="words">Spell words indexed by <see cref="Spell"/>; on equal precision the lower index wins.</param>
        /// <returns>False if no spell is recognised.</returns>
        public static bool TryRecognize(string buffer, string[] words, out RecognizedSpell result)
        {
            result = default;
            bool found = false;
            if (string.IsNullOrEmpty(buffer)) return false;

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (string.IsNullOrEmpty(word) || !IsSubsequence(word, buffer)) continue;

                // Two disjoint ordered occurrences == the doubled word is a subsequence (greedy match).
                bool powered = IsSubsequence(word + word, buffer);
                float precision = (float)(powered ? 2 * word.Length : word.Length) / buffer.Length;
                if (found && precision <= result.Precision) continue;

                result = new RecognizedSpell { Spell = (Spell)i, Precision = precision, Powered = powered };
                found = true;
            }
            return found;
        }

        public static bool IsSubsequence(string pattern, string text)
        {
            int p = 0;
            for (int t = 0; t < text.Length && p < pattern.Length; t++)
                if (text[t] == pattern[p]) p++;
            return p == pattern.Length;
        }
    }
}
