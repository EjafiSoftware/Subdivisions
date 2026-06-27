using System.IO;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Subdivisions.Systems;
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace Subdivisions
{
    public class Mod : IMod
    {
        public const string Version = "1.0.1";

        private static readonly ILog Log = LogManager.GetLogger($"{nameof(Subdivisions)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        private static Settings Settings { get; set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                var builtAt = File.GetLastWriteTime(asset.path).ToString("yyyy-MM-dd HH:mm:ss");
                Log.Info($"OnLoad - Subdivisions v{Version} (built {builtAt}) from {asset.path}");
            }
            else
            {
                Log.Info($"OnLoad - Subdivisions v{Version}");
            }

            Settings = new Settings(this);
            Settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new BasicLocale(Settings));
            AssetDatabase.global.LoadSettings(nameof(Subdivisions), Settings, new Settings(this));

            updateSystem.UpdateAt<SubdivisionsToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<SubdivisionsUISystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
            if (Settings is null)
            {
                return;
            }

            Settings.UnregisterInOptionsUI();
            Settings = null;
        }
    }
}
