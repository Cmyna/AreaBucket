using AreaBucket.Systems;
using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Unity.Entities;

namespace AreaBucket
{
    [FileLocation(nameof(AreaBucket))]
    [SettingsUIGroupOrder(kgMain)]
    [SettingsUIShowGroupName(kgMain)]
    [SettingsUIMouseAction(Mod.kModAreaToolApply, Mod.kModToolUsage)]
    public class Setting : ModSetting
    {
        public const string ksMain = "Main";
        public const string kgMain = "Main";


        private AreaBucketToolSystem _bucketToolSystem;



        public Setting(IMod mod) : base(mod)
        {
        }


        [SettingsUISlider(min = 0.5f, max = 4f, step = 0.5f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUISection(ksMain, kgMain)]
        public float MinGeneratedLineLength { get; set; } = 1f;

        [SettingsUISection(ksMain, kgMain)]
        public bool UseExperientalOption { get; set; } = false;

        [SettingsUIHidden]
        public bool ShowDebugOption { get; set; } = false;

        [SettingsUIHidden]
        public float MaxFillingRange { get; set; } = 250f;


        [SettingsUIHidden]
        [SettingsUIMouseBinding(Mod.kModAreaToolApply)]
        public ProxyBinding AreaBucketToolApply { get; set; }


        [SettingsUIHidden]
        public bool Contra { get; set; } = false;

        public override void Apply()
        {
            base.Apply();
            if (_bucketToolSystem == null)
            {
                _bucketToolSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<AreaBucketToolSystem>();
            }
            _bucketToolSystem.MinEdgeLength = MinGeneratedLineLength;
            _bucketToolSystem.UseExperimentalOptions = UseExperientalOption;
            _bucketToolSystem.MaxFillingRange = MaxFillingRange;
        }

        public override void SetDefaults()
        {
            MinGeneratedLineLength = 1f;
            UseExperientalOption = false;
            Contra = true;
        }


    }
}
