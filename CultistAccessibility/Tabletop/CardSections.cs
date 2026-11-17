using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultistAccessibility.Core;
using SecretHistories.UI;

namespace CultistAccessibility.Tabletop
{
    /// <summary>
    /// Sorts table cards into sections by kind, the way sighted players arrange the table by hand. The kind comes
    /// from the card's aspects (core content, checked against content/core/elements): the first rule that matches
    /// wins, and the rule order is the order of the sections in the Cards list.
    /// The player can also put a kind of card (every card with the same id, so the same name) into one of nine
    /// numbered sections of their own, listed before the automatic ones. Those choices are kept in
    /// CultistAccessibility_sections.txt next to the dll (one "elementid=N" per line), for every save.
    /// </summary>
    internal static class CardSections
    {
        private sealed class Rule
        {
            public Func<string> Name;
            public string[] Aspects;
            public string[] Ids;
        }

        // Funds and Ability cards also carry "ingredient", and dread/fascination are both influences and ill
        // health, so the order matters.
        private static readonly Rule[] Rules =
        {
            new Rule { Name = () => Strings.SectionAbilities, Aspects = new[] { "ability", "language" } },
            new Rule { Name = () => Strings.SectionFunds, Aspects = new[] { "asset" }, Ids = new[] { "funds" } },
            new Rule { Name = () => Strings.SectionIllHealth, Aspects = new[] { "illhealth" } },
            new Rule { Name = () => Strings.SectionInfluences, Aspects = new[] { "influence" } },
            new Rule { Name = () => Strings.SectionFollowers, Aspects = new[] { "follower" } },
            new Rule { Name = () => Strings.SectionPeople, Aspects = new[] { "mortal", "patron" } },
            new Rule { Name = () => Strings.SectionLore, Aspects = new[] { "lore" } },
            new Rule { Name = () => Strings.SectionBooks, Aspects = new[] { "text" } },
            new Rule { Name = () => Strings.SectionTools, Aspects = new[] { "tool" } },
            new Rule { Name = () => Strings.SectionIngredients, Aspects = new[] { "ingredient" } },
            new Rule { Name = () => Strings.SectionMemories, Aspects = new[] { "memory" } },
            new Rule { Name = () => Strings.SectionWork, Aspects = new[] { "job", "commission" } },
            new Rule { Name = () => Strings.SectionPlaces, Aspects = new[] { "society", "hq", "location", "vault", "domicile" } },
            new Rule { Name = () => Strings.SectionReputation, Aspects = new[] { "reputation", "evidencelevel" } },
        };

        private static int OtherIndex => Rules.Length;
        private static int FaceDownIndex => Rules.Length + 1;

        /// <summary>Highest player section number (keys 1 to 9).</summary>
        public const int CustomCount = 9;

        // Player sections take the indexes -9..-1, so they sort before the automatic ones.
        private static int CustomIndex(int number) => number - CustomCount - 1;

        private static Dictionary<string, int> _custom;
        private static string FilePath => Path.Combine(Plugin.PluginDir ?? "", "CultistAccessibility_sections.txt");

        private static Dictionary<string, int> Custom
        {
            get
            {
                if (_custom != null) return _custom;
                _custom = new Dictionary<string, int>(StringComparer.Ordinal);
                try
                {
                    if (File.Exists(FilePath))
                        foreach (var raw in File.ReadAllLines(FilePath))
                        {
                            string line = raw.Trim();
                            int eq = line.LastIndexOf('=');
                            if (line.StartsWith("#") || eq <= 0) continue;
                            if (int.TryParse(line.Substring(eq + 1).Trim(), out int n) && n >= 1 && n <= CustomCount)
                                _custom[line.Substring(0, eq).Trim()] = n;
                        }
                }
                catch (Exception ex)
                {
                    Plugin.LogWarning("Could not read card sections: " + ex.Message);
                }
                return _custom;
            }
        }

        private static void Save()
        {
            try
            {
                var lines = new List<string> { "# Cultist Simulator Accessibility: your card sections (card id = section 1 to 9)." };
                lines.AddRange(Custom.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value));
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception ex)
            {
                Plugin.LogWarning("Could not save card sections: " + ex.Message);
            }
        }

        /// <summary>The player's section (1 to 9) for this kind of card, or 0.</summary>
        public static int CustomSectionOf(string entityId)
        {
            return entityId != null && Custom.TryGetValue(entityId, out int n) ? n : 0;
        }

        /// <summary>Put every card with this id in section 1 to 9; 0 gives it back to its automatic section.</summary>
        public static void Assign(string entityId, int number)
        {
            if (string.IsNullOrEmpty(entityId)) return;
            if (number <= 0) Custom.Remove(entityId);
            else Custom[entityId] = Math.Min(number, CustomCount);
            Save();
        }

        /// <summary>The automatic section name of a card, ignoring the player's sections.</summary>
        public static string AutomaticName(Token card) => Name(AutomaticIndex(card));

        /// <summary>Section index of a card: the player's sections, then the rules, then Other, then face-down cards.</summary>
        public static int IndexOf(Token card)
        {
            try
            {
                var p = card.Payload;
                // A face-down card's kind would give away what it is.
                if (!p.IsShrouded)
                {
                    int n = CustomSectionOf(p.EntityId);
                    if (n > 0) return CustomIndex(n);
                }
            }
            catch { }
            return AutomaticIndex(card);
        }

        private static int AutomaticIndex(Token card)
        {
            try
            {
                var p = card.Payload;
                if (p.IsShrouded) return FaceDownIndex;
                string id = p.EntityId;
                var aspects = p.GetAspects(false);
                for (int i = 0; i < Rules.Length; i++)
                {
                    var r = Rules[i];
                    if (r.Ids != null && r.Ids.Contains(id)) return i;
                    if (r.Aspects != null && r.Aspects.Any(a => aspects.ContainsKey(a))) return i;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogDebug("CardSections.IndexOf failed: " + ex.Message);
            }
            return OtherIndex;
        }

        public static string Name(int index)
        {
            if (index < 0) return Strings.SectionCustom(index + CustomCount + 1);
            if (index >= 0 && index < Rules.Length) return Rules[index].Name();
            return index == FaceDownIndex ? Strings.SectionFaceDown : Strings.SectionOther;
        }

        /// <summary>Table cards in Cards-list order: by section, then name, then the soonest to decay.</summary>
        public static List<KeyValuePair<Token, int>> Sorted(IEnumerable<Token> cards)
        {
            return cards
                .Select(t => new KeyValuePair<Token, int>(t, IndexOf(t)))
                .OrderBy(x => x.Value)
                .ThenBy(x => Describer.CardName(x.Key), StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => DecayTime(x.Key))
                .ToList();
        }

        private static float DecayTime(Token t)
        {
            try
            {
                var shadow = t.Payload.GetTimeshadow();
                return shadow != null && shadow.Transient ? shadow.LifetimeRemaining : float.MaxValue;
            }
            catch { return float.MaxValue; }
        }
    }
}
