using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SITNA.AppCacheFactory
{
    public class WMTSLayerDefinition
    {
        public int UrlIdx { get; set; }
        public string Id { get; set; }
        public int TmsIdx { get; set; }
        public int FormatIdx { get; set; }

        public static WMTSLayerDefinition FromQueryStringPart(string part)
        {
            WMTSLayerDefinition result = new WMTSLayerDefinition();
            Regex re = new Regex("u-(.+)_i-(.+)_t-(.+)_f-(.+)");
            Match match = re.Match(part);
            if (match.Success)
            {
                result.UrlIdx = Convert.ToInt32(match.Groups[1].Value);
                result.Id = match.Groups[2].Value;
                result.TmsIdx = Convert.ToInt32(match.Groups[3].Value);
                result.FormatIdx = Convert.ToInt32(match.Groups[4].Value);
            }
            return result;
        }

        public override bool Equals(object other)
        {
            var result = false;
            WMTSLayerDefinition theOther = other as WMTSLayerDefinition;
            if (theOther != null)
            {
                result = UrlIdx.Equals(theOther.UrlIdx) &&
                    Id.Equals(theOther.Id) &&
                    TmsIdx.Equals(theOther.TmsIdx) &&
                    FormatIdx.Equals(theOther.FormatIdx);
            }
            return result;
        }
    }

}
