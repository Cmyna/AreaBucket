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



        public Setting(IMod mod) : base(mod)
        {
        }


        [SettingsUISlider(min = 0.5f, max = 4f, step = 0.5f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUISection(ksMain, kgMain)]
        public float MinGeneratedLineLength { get; set; } = 1f;

        [SettingsUISection(ksMain, kgMain)]
        public bool UseExperientalOption { get; set; } = false;

        [SettingsUISection(ksMain, kgMain)]
        public bool DrawAreaOverlay { get; set; } = true;

        [SettingsUIHidden]
        public bool AlterVanillaGeometrySystem { get; set; } = false;

        [SettingsUIHidden]
        public bool ShowDebugOption { get; set; } = false;

        [SettingsUIHidden]
        public float MaxFillingRange { get; set; } = 250f;


        [SettingsUIHidden]
        [SettingsUIMouseBinding(Mod.kModAreaToolApply)]
        public ProxyBinding AreaBucketToolApply { get; set; }


        public override void SetDefaults()
        {
            MinGeneratedLineLength = 1f;
            UseExperientalOption = false;
        }


    }
}
