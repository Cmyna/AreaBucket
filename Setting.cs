using AreaBucket.Systems;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace AreaBucket
{
    [FileLocation(nameof(AreaBucket))]
    [SettingsUIGroupOrder(kButtonGroup, kToggleGroup, kSliderGroup, kDropdownGroup)]
    [SettingsUIShowGroupName(kButtonGroup, kToggleGroup, kSliderGroup, kDropdownGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";

        public const string kButtonGroup = "Button";
        public const string kToggleGroup = "Toggle";
        public const string kSliderGroup = "Slider";
        public const string kDropdownGroup = "Dropdown";

        private AreaBucketToolSystem _bucketToolSystem;

        public Setting(IMod mod, AreaBucketToolSystem bucketToolSystem) : base(mod)
        {
            _bucketToolSystem = bucketToolSystem;
        }


        [SettingsUISlider(min = 0.5f, max = 4f, step = 1, scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(kSection, kSliderGroup)]
        public float MinGeneratedLineLength { get; set; }


        public override void Apply()
        {
            base.Apply();
            _bucketToolSystem.MinEdgeLength = MinGeneratedLineLength;
        }

        public override void SetDefaults()
        {

        }


    }
}
