using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace AppCacheFactory
{
    public class OfflineMapDefinition : ITileListComposer
    {
        public double[] BBox { get; set; }
        public double Resolution { get; set; }
        public WMTSLayerDefinition[] Layers { get; set; }

        public List<string> GetRequestList()
        {
            List<string> result = new List<string>();
            Dictionary<string, XmlDocument> capabilities = new Dictionary<string, XmlDocument>();
            if (Layers != null)
            {
                for (int i = 0; i < Layers.Length; i++)
                {
                    WMTSLayerDefinition layer = Layers[i];
                    XmlDocument xmlDoc;
                    if (!capabilities.ContainsKey(layer.Url))
                    {
                        xmlDoc = new XmlDocument();
                        xmlDoc.Load(XmlReader.Create(layer.Url));
                        capabilities[layer.Url] = xmlDoc;
                    }
                    else
                    {
                        xmlDoc = capabilities[layer.Url];
                    }
                    XmlNode layerNode = xmlDoc.SelectSingleNode(string.Format("Capabilities/Contents/Layer[ows:Identifier='{0}']", layer.LayerId));
                    if (layerNode != null)
                    {
                        XmlNode urlPatternNode = layerNode.SelectSingleNode(string.Format("ResourceURL[@format='{0}'][@resourceType='tile']", layer.Format));
                        string urlPattern = null;
                        if (urlPatternNode != null)
                        {
                            urlPattern = urlPatternNode.Attributes["template"].Value;
                            XmlNodeList tmlNodes = layerNode.SelectNodes(string.Format("TileMatrixSetLink[TileMatrixSet='{0}']/TileMatrixSetLimits/TileMatrixLimits", layer.TMS));
                            XmlNodeList tmNodes = xmlDoc.SelectNodes(string.Format("Capabilities/Contents/TileMatrixSet[ows:Identifier='{0}']/TileMatrix", layer.TMS));
                            XmlNode tmNode;
                            for (int j = 0; j < tmNodes.Count; j++)
                            {
                                tmNode = tmNodes[j];
                                int[] indexRange = GetIndexRange(tmNode, tmlNodes);
                                string identifier = tmNode.SelectSingleNode("ows:Identifier").InnerText;
                                for (int x = indexRange[0]; x <= indexRange[2]; x++)
                                {
                                    for (int y = indexRange[1]; y <= indexRange[3]; y++)
                                    {
                                        result.Add(urlPattern
                                        .Replace("{TileMatrix}", identifier)
                                        .Replace("{TileCol}", x.ToString())
                                        .Replace("{TileRow}", y.ToString()));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        protected int[] GetIndexRange(XmlNode tmNode, XmlNodeList tmlNodes)
        {
            int[] result = new int[4];
            string identifier = tmNode.SelectSingleNode("ows:Identifier").InnerText;

            string[] topLeftCorner = tmNode.SelectSingleNode("TopLeftCorner").InnerText.Split(' ');
            double top = Convert.ToDouble(topLeftCorner[0]);
            double left = Convert.ToDouble(topLeftCorner[1]);

            double scaleDenominator = Convert.ToDouble(tmNode.SelectSingleNode("ScaleDenominator").InnerText);
            int tileWidth = Convert.ToInt32(tmNode.SelectSingleNode("TileWidth").InnerText);
            int tileHeight = Convert.ToInt32(tmNode.SelectSingleNode("TileHeight").InnerText);
            double resolution = 0.00028 * scaleDenominator; // OGC assumes 0.28 mm / pixel
            double xStep = resolution * tileWidth;
            double yStep = resolution * tileHeight;

            int matrixWidth = Convert.ToInt32(tmNode.SelectSingleNode("MatrixWidth").InnerText);
            int matrixHeight = Convert.ToInt32(tmNode.SelectSingleNode("MatrixHeight").InnerText);

            int mMin = Convert.ToInt32(Math.Floor((BBox[0] - left) / xStep));
            int nMin = Convert.ToInt32(Math.Floor((BBox[1] - top) / yStep));
            int mMax = Convert.ToInt32(Math.Floor((BBox[2] - left) / xStep));
            int nMax = Convert.ToInt32(Math.Floor((BBox[3] - top) / yStep));

            mMax = Math.Min(mMax, mMin + matrixWidth);
            nMax = Math.Min(nMax, nMin + matrixHeight);

            foreach (XmlNode tmlNode in tmlNodes)
            {
                if (tmlNode.SelectSingleNode("TileMatrix").InnerText == identifier)
                {
                    mMin = Math.Max(Convert.ToInt32(tmlNode.SelectSingleNode("MinTileCol").InnerText), mMin);
                    nMin = Math.Max(Convert.ToInt32(tmlNode.SelectSingleNode("MinTileRow").InnerText), nMin);
                    mMax = Math.Min(Convert.ToInt32(tmlNode.SelectSingleNode("MaxTileCol").InnerText), mMax);
                    nMax = Math.Min(Convert.ToInt32(tmlNode.SelectSingleNode("MaxTileRow").InnerText), nMax);
                    break;
                }
            }
            result[0] = mMin;
            result[1] = nMin;
            result[2] = mMax;
            result[3] = nMax;
            return result;
        }
    }
}
