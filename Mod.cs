using AreaBucket.Systems;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.UI;
using Game;
using Game.Modding;
using Game.SceneFlow;
using System.IO;
using System.Reflection;
using Unity.Entities;

namespace AreaBucket
{
    public class Mod : IMod
    {
        public const string ToolId = "Area Bucket";

        public static ILog Logger = LogManager.GetLogger($"{nameof(AreaBucket)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        internal static Setting modSetting;

        // Mod assembly path cache.
        private string s_assemblyPath = null;

        /// <summary>
        /// Reference from Algernon's UnifiedIconLibrary `Mod.cs`
        /// Gets the mod directory file path of the currently executing mod assembly.
        /// </summary>
        public string AssemblyPath
        {
            get
            {
                // Update cached path if the existing one is invalid.
                if (string.IsNullOrWhiteSpace(s_assemblyPath))
                {
                    // No path cached - find current executable asset.
                    string assemblyName = Assembly.GetExecutingAssembly().FullName;
                    ExecutableAsset modAsset = AssetDatabase.global.GetAsset(SearchFilter<ExecutableAsset>.ByCondition(x => x.definition?.FullName == assemblyName));
                    if (modAsset is null)
                    {
                        Logger.Error("mod executable asset not found");
                        return null;
                    }

                    // Update cached path.
                    s_assemblyPath = Path.GetDirectoryName(modAsset.GetMeta().path);
                }

                // Return cached path.
                return s_assemblyPath;
            }
        }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Logger.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Logger.Info($"Current mod asset at {asset.path}");
            }

            //updateSystem.UpdateAt<BlockAreaToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<AreaBucketToolSystem>(SystemUpdatePhase.ToolUpdate);
            // updateSystem.UpdateAt<AreaReplacementToolSystem>(SystemUpdatePhase.ToolUpdate);
            // updateSystem.UpdateAt<SimpleAreaHandleSystem>(SystemUpdatePhase.ModificationEnd);

            updateSystem.UpdateAt<AreaBucketToolUISystem>(SystemUpdatePhase.UIUpdate);
            // updateSystem.UpdateAt<NetDebugSystemCopy>(SystemUpdatePhase.DebugGizmos);

            var areaBucketToolSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<AreaBucketToolSystem>();

            modSetting = new Setting(this, areaBucketToolSystem);
            modSetting.RegisterInOptionsUI();
            // modSetting.RegisterKeyBindings();
            GameManager.instance.localizationManager.AddSource("en-US", new BasicLocale(modSetting));

            AssetDatabase.global.LoadSettings(nameof(AreaBucket), modSetting, new Setting(this, areaBucketToolSystem));

            // UIManager.defaultUISystem.AddHostLocation("areabucket", AssemblyPath + "/Icons/");

            modSetting.Apply(); // apply once
            areaBucketToolSystem.LogToolState(Logger, "Initial Area Bucket Tool States: ");
        }

        public void OnDispose()
        {
            Logger.Info(nameof(OnDispose));
            if (modSetting != null)
            {
                modSetting.UnregisterInOptionsUI();
                modSetting = null;
            }
            
        }
    }
}
