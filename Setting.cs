using AreaBucket.Systems;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace AreaBucket
{
    [FileLocation(nameof(AreaBucket))]
    [SettingsUIGroupOrder(kgMain)]
    [SettingsUIShowGroupName(kgMain)]
    public class Setting : ModSetting
    {
        public const string ksMain = "Main";
        public const string kgMain = "Main";

        private AreaBucketToolSystem _bucketToolSystem;

        public Setting(IMod mod, AreaBucketToolSystem bucketToolSystem) : base(mod)
        {
            _bucketToolSystem = bucketToolSystem;
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
        public bool Contra { get; set; } = false;

        public override void Apply()
        {
            base.Apply();
            _bucketToolSystem.MinEdgeLength = MinGeneratedLineLength;
            _bucketToolSystem.UseExperimentalOptions = UseExperientalOption;
            _bucketToolSystem.ShowDebugOptions = ShowDebugOption;
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
