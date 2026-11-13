using System;
using System.Collections.Generic;
using System.Linq;
using CultistAccessibility.Core;
using SecretHistories.UI;

namespace CultistAccessibility.Tabletop
{
    /// <summary>
    /// Sorts table cards into sections by kind, the way sighted players arrange the table by hand. The kind comes
    /// from the card's aspects (core content, checked against content/core/elements): the first rule that matches
    /// wins, and the rule order is the order of the sections in the Cards list.
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

        /// <summary>Section index of a card: rules first, then Other, then face-down cards.</summary>
        public static int IndexOf(Token card)
        {
            try
            {
                var p = card.Payload;
                // A face-down card's kind would give away what it is.
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
