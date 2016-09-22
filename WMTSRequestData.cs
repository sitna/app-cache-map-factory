using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
using Newtonsoft.Json;

namespace SITNA.AppCacheFactory
{
    public class WMTSRequestData : ITileListComposer
    {
        public string Url { get; set; }
        public TileMatrixLimits[] TileMatrixLimits { get; set; }

        public List<string> GetRequestList()
        {
            List<string> result = new List<string>();
            if (TileMatrixLimits != null)
            {
                for (int i = 0; i < TileMatrixLimits.Length; i++)
                {
                    TileMatrixLimits tml = TileMatrixLimits[i];
                    for (int j = tml.Rt; j <= tml.Rb; j++)
                    {
                        for (int k = tml.Cl; k <= tml.Cr; k++)
                        {
                            result.Add(Url.Replace("{TileMatrix}", tml.MId).Replace("{TileRow}", j.ToString()).Replace("{TileCol}", k.ToString()));
                        }
                    }
                }
            }
            return result;
        }

        public string ToQueryString()
        {
            return "M=" + Uri.EscapeUriString(JsonConvert.SerializeObject(this));
        }

        public void FromQueryString(NameValueCollection queryString)
        {
            WMTSRequestData qsObject = null;
            JsonReader jsonReader = new JsonTextReader(new System.IO.StringReader(queryString["M"]));
            JsonSerializer jsonSerializer = new JsonSerializer();
            qsObject = jsonSerializer.Deserialize<WMTSRequestData>(jsonReader);
            Url = qsObject.Url;
            TileMatrixLimits = qsObject.TileMatrixLimits;
        }
    }
}
