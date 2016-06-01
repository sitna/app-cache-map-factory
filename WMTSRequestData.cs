using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppCacheFactory
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
    }
}
