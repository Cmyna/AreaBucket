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
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>()
            {
                { "AREABUCKET.ToolOptions.Title", "AreaBucket" },
                { "AREABUCKET.ToolOptions.Switch.TooltipTitle", "Switch" },
                { "AREABUCKET.ToolOptions.Switch.TooltipDesc", "Enable/Disable Area Bucket" }
            };
        }

        public void Unload() {}
    }
}
