using Colossal;
using Colossal.IO.AssetDatabase.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                { ToolOptionDescKey("Active"), "Enable/Disable Area Bucket Tool" },

                { ToolOptionKey("DetectCrossing"), "Detect Boundaries Crossing" },
                { ToolOptionDescKey("DetectCrossing"), "Detect boundaries crossing and fix some filling gaps (performance issue)" },

                { ToolOptionKey("FillRange"), "Fill Range" },
                { ToolOptionDescKey("FillRange"), "Set Tool Filling Range" },

                { ToolOptionKey("BoundaryMask"), "Used Boundaries" },
                { ToolOptionKey("MaskNet"), "Road & Net" },
                { ToolOptionKey("MaskLot"), "Building Lot" },
                { ToolOptionKey("MaskArea"), "Surface & Area" },
                { ToolOptionKey("MaskSubNet"), "Sub Net (performance issue)" },

                { modSetting.GetOptionTabLocaleID(Setting.ksMain), "Area Bucket" },
                { modSetting.GetOptionGroupLocaleID(Setting.kgMain), "Area Bucket" },
                { modSetting.GetSettingsLocaleID(), "Area Bucket" },

                { modSetting.GetOptionLabelLocaleID(nameof(Setting.MinGeneratedLineLength)), "Mininal Area Edge Length (meter)" },
                { modSetting.GetOptionDescLocaleID(nameof(Setting.MinGeneratedLineLength)), "Restrict mininal edge length of generated area polygons, smaller value makes edges smoother and more fragmented" },

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
