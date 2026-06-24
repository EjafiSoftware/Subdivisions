using System.Collections.Generic;
using Colossal;

namespace Subdivisions
{
    /// <summary>English (en-US) localization source for the settings UI and the toolbar button.</summary>
    public class BasicLocale : IDictionarySource
    {
        private readonly Settings _settings;

        public BasicLocale(Settings settings)
        {
            _settings = settings;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { _settings.GetSettingsLocaleID(), "Subdivisions" },
                { _settings.GetOptionTabLocaleID(Settings.KsMain), "Main" },
                { _settings.GetOptionGroupLocaleID(Settings.KgAbout), "About" },

                { _settings.GetOptionLabelLocaleID(nameof(Settings.Version)), "Version" },
                { _settings.GetOptionDescLocaleID(nameof(Settings.Version)), "Mod version." },

                { "Subdivisions.TOOLTIP_TOGGLE", "Subdivisions - trace networks to enclose a district" },
            };
        }

        public void Unload() { }
    }
}
