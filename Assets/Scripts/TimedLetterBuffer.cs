using System.Collections.Generic;
using System.Text;

namespace JunkyWizard.Prototype
{
    /// <summary>
    /// GridV3 free typing buffer. Every letter is stored with its cursed flag (typed off the beat);
    /// cursed letters count as normal letters for spell recognition.
    /// </summary>
    public class TimedLetterBuffer
    {
        public const string CursedColor = "#B566FF";

        readonly List<char> letters = new List<char>();
        readonly List<bool> cursed = new List<bool>();
        readonly int maxLetters;

        public TimedLetterBuffer(int maxLetters)
        {
            this.maxLetters = maxLetters;
        }

        public int Count => letters.Count;
        public int MaxLetters => maxLetters;

        public int CursedCount
        {
            get
            {
                int count = 0;
                foreach (bool c in cursed) if (c) count++;
                return count;
            }
        }

        /// <summary>Plain lowercase letters, for spell recognition.</summary>
        public string Text => new string(letters.ToArray());

        /// <returns>False if the letter would exceed the limit: the buffer explodes (and is cleared).</returns>
        public bool Add(char letter, bool isCursed)
        {
            if (letters.Count >= maxLetters)
            {
                Clear();
                return false;
            }
            letters.Add(char.ToLowerInvariant(letter));
            cursed.Add(isCursed);
            return true;
        }

        public void Clear()
        {
            letters.Clear();
            cursed.Clear();
        }

        /// <summary>uGUI rich text: cursed letters purple and italic.</summary>
        public string ToRichText()
        {
            var builder = new StringBuilder();
            for (int i = 0; i < letters.Count; i++)
            {
                if (cursed[i]) builder.Append("<color=").Append(CursedColor).Append("><i>").Append(letters[i]).Append("</i></color>");
                else builder.Append(letters[i]);
            }
            return builder.ToString();
        }
    }
}
