using System.Text;

namespace JunkyWizard.Prototype
{
    public enum Spell { Pyro = 0, Heal = 1, Aegis = 2 }

    /// <summary>Typing buffer: Space opens, letters must follow a spell prefix, Enter submits.</summary>
    public class SpellTyper
    {
        readonly string[] words;
        readonly StringBuilder buffer = new StringBuilder();

        public SpellTyper(string[] words)
        {
            this.words = words;
        }

        public bool IsOpen { get; private set; }
        public string Buffer => buffer.ToString();

        public void Open()
        {
            IsOpen = true;
            buffer.Clear();
        }

        public void Cancel()
        {
            IsOpen = false;
            buffer.Clear();
        }

        /// <summary>Non-letters are ignored; a letter that breaks every prefix cancels the buffer.</summary>
        public void Type(char c)
        {
            if (!IsOpen || !char.IsLetter(c)) return;

            string candidate = buffer.ToString() + char.ToLowerInvariant(c);
            foreach (var word in words)
            {
                if (word.StartsWith(candidate, System.StringComparison.Ordinal))
                {
                    buffer.Append(char.ToLowerInvariant(c));
                    return;
                }
            }
            Cancel();
        }

        /// <summary>Closes the buffer. Returns the index of the completed spell, or -1 if the word is incomplete.</summary>
        public int Submit()
        {
            string typed = buffer.ToString();
            Cancel();
            for (int i = 0; i < words.Length; i++)
                if (words[i] == typed) return i;
            return -1;
        }
    }
}
