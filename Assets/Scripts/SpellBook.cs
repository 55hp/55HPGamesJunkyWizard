using System;
using System.Collections.Generic;

namespace JunkyWizard.Prototype
{
    public enum Element { None, Fire, Ice, Wind }

    public enum SpellKind { Base, Double, Beam, Line, Heal, FullHeal, Aegis }

    public readonly struct SpellDef
    {
        public readonly string Word;
        public readonly SpellKind Kind;
        public readonly Element Element;

        public SpellDef(string word, SpellKind kind, Element element = Element.None)
        {
            Word = word;
            Kind = kind;
            Element = element;
        }

        public bool IsElemental => Element != Element.None;
        /// <summary>Base, double, beam: cast on the selected lane. Line: any lane key, hits every lane.</summary>
        public bool TargetsLane => IsElemental;

        public string Effect
        {
            get
            {
                switch (Kind)
                {
                    case SpellKind.Base: return "colpisce il primo nemico della corsia";
                    case SpellKind.Double: return "colpisce i primi 2 nemici della corsia";
                    case SpellKind.Beam: return "colpisce tutti i nemici della corsia";
                    case SpellKind.Line: return "colpisce il primo nemico di ogni corsia";
                    case SpellKind.Heal: return "+1 vita";
                    case SpellKind.FullHeal: return "vite al massimo";
                    default: return "+1 carica scudo";
                }
            }
        }

        public string TargetLabel => Kind == SpellKind.Line ? "Bersaglio: tutte le corsie (Invio / ↑)"
            : IsElemental ? "Bersaglio: corsia selezionata (Invio / ↑)"
            : "Bersaglio: sé (Shift destro / ↓)";

        public string ElementLabel => Element == Element.Fire ? "fuoco"
            : Element == Element.Ice ? "ghiaccio"
            : Element == Element.Wind ? "vento"
            : "supporto";
    }

    /// <summary>Lanes1to5 spell book (v4). Words are fixed here; which ones are unlocked comes from the config.</summary>
    public static class SpellBook
    {
        public static readonly SpellDef[] All =
        {
            new SpellDef("pyro", SpellKind.Base, Element.Fire),
            new SpellDef("kryo", SpellKind.Base, Element.Ice),
            new SpellDef("aero", SpellKind.Base, Element.Wind),
            new SpellDef("pyropyro", SpellKind.Double, Element.Fire),
            new SpellDef("kryokryo", SpellKind.Double, Element.Ice),
            new SpellDef("aeroaero", SpellKind.Double, Element.Wind),
            new SpellDef("pyrsos", SpellKind.Beam, Element.Fire),
            new SpellDef("krymos", SpellKind.Beam, Element.Ice),
            new SpellDef("anemos", SpellKind.Beam, Element.Wind),
            new SpellDef("pyrkaia", SpellKind.Line, Element.Fire),
            new SpellDef("kryeros", SpellKind.Line, Element.Ice),
            new SpellDef("thyella", SpellKind.Line, Element.Wind),
            new SpellDef("iasis", SpellKind.Heal),
            new SpellDef("panakeia", SpellKind.FullHeal),
            new SpellDef("aegis", SpellKind.Aegis),
        };

        /// <summary>Spells unlocked for <paramref name="laneCount"/>, in book order. Unknown words are ignored.</summary>
        public static List<SpellDef> Unlocked(PrototypeConfig config, int laneCount)
        {
            var result = new List<SpellDef>();
            var lists = config.spellsUnlockedByLaneCount;
            if (lists == null || lists.Length == 0) return result;

            string list = lists[Math.Max(0, Math.Min(laneCount, lists.Length) - 1)] ?? "";
            var words = new HashSet<string>(list.ToLowerInvariant().Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
            foreach (var spell in All)
                if (words.Contains(spell.Word)) result.Add(spell);
            return result;
        }
    }
}
