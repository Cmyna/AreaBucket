using AreaBucket.Systems;
using AreaBucket.Systems.SurfacePreviewSystem;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.UI;
using Game;
using Game.Modding;
using Game.SceneFlow;
using System.IO;
using System.Reflection;
using Unity.Entities;
using UnityEngine.Rendering;

namespace AreaBucket
{
    public class Mod : IMod
    {
        public const string ToolId = "Area Bucket";

        public const string kModAreaToolApply = "AreaToolModApplyAction";

        public const string kModToolUsage = "AreaBucketModToolUsage";

        public static ILog Logger = LogManager.GetLogger($"{nameof(AreaBucket)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        internal static Setting modSetting;

        internal static DebugUI.Panel AreaBucketDebugUI;

        /// <summary>
        /// determine any mod area tool enable to active/use or not
        /// </summary>
        internal static bool areaToolEnabled = false;

        /// <summary>
        /// controls multiple mod tool systems which tool is active
        /// </summary>
        internal static ModActiveTool modActiveTool = ModActiveTool.None;

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

            AreaBucketDebugUI = DebugManager.instance.GetPanel("Area Bucket", createIfNull: true, groupIndex: 0, overrideIfExist: true);

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Logger.Info($"Current mod asset at {asset.path}");
            }

            modSetting = new Setting(this);
            modSetting.RegisterInOptionsUI();
            modSetting.RegisterKeyBindings();
            GameManager.instance.localizationManager.AddSource("en-US", new BasicLocale(modSetting));

            AssetDatabase.global.LoadSettings(nameof(AreaBucket), modSetting, new Setting(this));

            // UIManager.defaultUISystem.AddHostLocation("areabucket", AssemblyPath + "/Icons/");

            modSetting.Apply(); // apply once


            updateSystem.UpdateAt<AreaBucketToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<SurfacePreviewSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAt<AreaReplacementToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<AreaBucketToolUISystem>(SystemUpdatePhase.UIUpdate);

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
