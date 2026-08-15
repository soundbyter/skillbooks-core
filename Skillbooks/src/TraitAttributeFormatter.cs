using System.Collections.Generic;
using System.Text.RegularExpressions;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Skillbooks
{
    /// <summary>
    /// Formats a trait's raw Attributes (stat modifier deltas) into player-facing text,
    /// reusing the "charattribute-<key>-<value>" lang key convention the vanilla
    /// character-creation screen uses (CharacterSystem.getClassTraitText, confirmed via
    /// decompile). Most class mods (aldiclasses for example) already ship these entries
    /// for their own traits so they display correctly there too -- reusing the same lookup
    /// gets correct, human-written text for free rather than guessing a percent-vs-flat
    /// formatting rule. Falls back to a generic percentage guess only when nobody's written
    /// an entry for that exact (key, value) pair.
    /// </summary>
    public static class TraitAttributeFormatter
    {
        public static string Format(JsonObject attributesJson)
        {
            Dictionary<string, double> attributes = attributesJson?.AsObject<Dictionary<string, double>>();
            if (attributes == null || attributes.Count == 0) { return null; }

            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, double> attr in attributes)
            {
                string key = string.Format(GlobalConstants.DefaultCultureInfo, "charattribute-{0}-{1}", attr.Key, attr.Value);
                parts.Add(Lang.GetIfExists(key) ?? GenericFormat(attr.Key, attr.Value));
            }

            return string.Join(", ", parts);
        }

        private static string GenericFormat(string key, double value)
        {
            string label = Regex.Replace(key, "([a-z])([A-Z])", "$1 $2").ToLowerInvariant();
            string sign = value >= 0 ? "+" : "";
            return $"{sign}{(value * 100).ToString("0.#", GlobalConstants.DefaultCultureInfo)}% {label}";
        }
    }
}
