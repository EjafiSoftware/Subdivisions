using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace Subdivisions
{
    [FileLocation(nameof(Subdivisions))]
    [SettingsUIGroupOrder(KgAbout)]
    [SettingsUIShowGroupName(KgAbout)]
    public class Settings : ModSetting
    {
        public const string KsMain = "Main";
        public const string KgAbout = "About";

        public Settings(IMod mod) : base(mod) { }

        [SettingsUISection(KsMain, KgAbout)]
        public string Version => Mod.Version;

        public override void SetDefaults() { }
    }
}
