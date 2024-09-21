using Colossal;
using System.Collections.Generic;


namespace AreaBucket
{
    internal class BasicLocale : IDictionarySource
    {
        private Setting modSetting;

        public const string toolLocaleKey = "AREA_BUCKT_TOOL";

        public BasicLocale(Setting modSetting) 
        {
            this.modSetting = modSetting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>()
            {

                { ToolOptionKey("Active"), "Area Bucket" },
                { ToolOptionDescKey("Active"), "Switch Area Bucket Tools" },

                { ToolOptionKey("ActiveAreaBucket"), "Area Bucket" },
                { ToolOptionDescKey("ActiveAreaBucket"), "Enable/Disable Area Bucket Tool" },

                { ToolOptionKey("ActiveAreaReplacement"), "Area Replacement" },
                { ToolOptionDescKey("ActiveAreaReplacement"), "Enable/Disable Area Replacement Tool" },

                { ToolOptionKey("DetectCrossing"), "Detect Boundaries Crossing" },
                { ToolOptionDescKey("DetectCrossing"), "Detect boundaries crossing and fix some filling gaps (performance issue)" },

                { ToolOptionKey("FloodingDepth"), "Flooding Depth" },
                { ToolOptionDescKey("FloodingDepth"), "Set Tool Flooding Depth" },

                { ToolOptionKey("FloodingRange"), "Flooding Range" },
                { ToolOptionDescKey("FloodingRange"), "Set Tool Filling Range" },

                { ToolOptionKey("BoundaryMask"), "Used Boundaries" },
                { ToolOptionKey("MaskNet"), "Road & Net" },
                { ToolOptionKey("MaskLot"), "Building Lot" },
                { ToolOptionKey("MaskArea"), "Surface & Area" },
                { ToolOptionKey("MaskNetLane"), "Net Lane" },
                { ToolOptionKey("MaskSubNet"), "Sub Net" },
                

                { modSetting.GetOptionTabLocaleID(Setting.ksMain), "Area Bucket" },
                { modSetting.GetOptionGroupLocaleID(Setting.kgMain), "Area Bucket" },
                { modSetting.GetSettingsLocaleID(), "Area Bucket" },

                { modSetting.GetOptionLabelLocaleID(nameof(Setting.MinGeneratedLineLength)), "Mininal Area Edge Length (meter)" },
                { modSetting.GetOptionDescLocaleID(nameof(Setting.MinGeneratedLineLength)), "Restrict mininal edge length of generated area polygons, smaller value makes edges smoother and more fragmented" },

                { modSetting.GetOptionLabelLocaleID(nameof(Setting.DrawAreaOverlay)), "Show Area Color Overlays" },
                { modSetting.GetOptionDescLocaleID(nameof(Setting.DrawAreaOverlay)), "Show color overlays on area when using mod provided tools" },


                { modSetting.GetOptionLabelLocaleID(nameof(Setting.PreviewSurface)), "Preview Surface" },
                { modSetting.GetOptionDescLocaleID(nameof(Setting.PreviewSurface)), "Preview surface texture when using buckect tool" },

                { modSetting.GetOptionLabelLocaleID(nameof(Setting.UseExperientalOption)), "Experimental Options" },
                { modSetting.GetOptionDescLocaleID(nameof(Setting.UseExperientalOption)), "Use Experimental Options (May have performance or other issue)" }
            };
        }

        private string ToolOptionKey(string optionKey)
        {
            return toolLocaleKey + "." + optionKey;
        }

        private string ToolOptionDescKey(string optionKey)
        {
            return toolLocaleKey + ".DESC." + optionKey;
        }

        public void Unload() {}
    }
}
