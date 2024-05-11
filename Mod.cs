using AreaBucket.Systems;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.SceneFlow;
using Game.Tools;
using Unity.Entities;

namespace AreaBucket
{
    public class Mod : IMod
    {
        public const string ToolId = "Area Bucket";

        public static ILog log = LogManager.GetLogger($"{nameof(AreaBucket)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        private Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            //updateSystem.UpdateAt<BlockAreaToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<AreaBucketToolSystem>(SystemUpdatePhase.ToolUpdate);

            updateSystem.UpdateAt<AreaBucketToolUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<NetDebugSystemCopy>(SystemUpdatePhase.DebugGizmos);

            var areaBucketToolSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<AreaBucketToolSystem>();

            m_Setting = new Setting(this, areaBucketToolSystem);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new BasicLocale());

            // AssetDatabase.global.LoadSettings(nameof(AreaBucket), m_Setting, new Setting(this));
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
            
        }
    }
}
