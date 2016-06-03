using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace AppCacheFactory
{
    public class OfflineMapDefinition : ITileListComposer
    {
        public double[] BBox { get; set; }
        public double Res { get; set; }
        public string[] Urls { get; set; }
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
                    string capabilitiesUrl = Urls[layer.UrlIdx] + "/1.0.0/WMTSCapabilities.xml";
                    if (!capabilities.ContainsKey(capabilitiesUrl))
                    {
                        xmlDoc = new XmlDocument();
                        xmlDoc.XmlResolver = null; // Evita que se descargue la DTD
                        xmlDoc.Load(XmlReader.Create(capabilitiesUrl));
                        capabilities[capabilitiesUrl] = xmlDoc;
                    }
                    else
                    {
                        xmlDoc = capabilities[capabilitiesUrl];
                    }
                    var xnm = new XmlNamespaceManager(xmlDoc.NameTable);
                    xnm.AddNamespace("ns", "http://www.opengis.net/wmts/1.0");
                    xnm.AddNamespace("ows", "http://www.opengis.net/ows/1.1");
                    XmlNode layerNode = xmlDoc.SelectSingleNode(string.Format("/ns:Capabilities/ns:Contents/ns:Layer[ows:Identifier='{0}']", layer.LayerId), xnm);

                    if (layerNode != null)
                    {
                        XmlNode urlPatternNode = layerNode.SelectSingleNode(string.Format("ns:ResourceURL[@format='{0}'][@resourceType='tile']", layer.Format), xnm);
                        string urlPattern = null;
                        if (urlPatternNode != null)
                        {
                            urlPattern = urlPatternNode.Attributes["template"].Value;
                            XmlNodeList tmlNodes = layerNode.SelectNodes(string.Format("ns:TileMatrixSetLink[ns:TileMatrixSet='{0}']/ns:TileMatrixSetLimits/ns:TileMatrixLimits", layer.TMS), xnm);
                            XmlNodeList tmNodes = xmlDoc.SelectNodes(string.Format("/ns:Capabilities/ns:Contents/ns:TileMatrixSet[ows:Identifier='{0}']/ns:TileMatrix", layer.TMS), xnm);
                            XmlNode tmNode;
                            for (int j = 0; j < tmNodes.Count; j++)
                            {
                                tmNode = tmNodes[j];
                                int[] indexRange = GetIndexRange(tmNode, tmlNodes, xnm);
                                string identifier = tmNode.SelectSingleNode("ows:Identifier", xnm).InnerText;
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

        protected int[] GetIndexRange(XmlNode tmNode, XmlNodeList tmlNodes, XmlNamespaceManager xnm)
        {
            int[] result = new int[4];
            string identifier = tmNode.SelectSingleNode("ows:Identifier", xnm).InnerText;

            string[] topLeftCorner = tmNode.SelectSingleNode("ns:TopLeftCorner", xnm).InnerText.Split(' ');
            double tl0 = Convert.ToDouble(topLeftCorner[0], CultureInfo.InvariantCulture);
            double tl1 = Convert.ToDouble(topLeftCorner[1], CultureInfo.InvariantCulture);
            double[] origin = new double[2] { tl0, tl1 };

            double scaleDenominator = Convert.ToDouble(tmNode.SelectSingleNode("ns:ScaleDenominator", xnm).InnerText, CultureInfo.InvariantCulture);
            int tileWidth = Convert.ToInt32(tmNode.SelectSingleNode("ns:TileWidth", xnm).InnerText);
            int tileHeight = Convert.ToInt32(tmNode.SelectSingleNode("ns:TileHeight", xnm).InnerText);
            double resolution = 0.00028 * scaleDenominator; // OGC assumes 0.28 mm / pixel
            double xStep = resolution * tileWidth;
            double yStep = resolution * tileHeight;

            int matrixWidth = Convert.ToInt32(tmNode.SelectSingleNode("ns:MatrixWidth", xnm).InnerText);
            int matrixHeight = Convert.ToInt32(tmNode.SelectSingleNode("ns:MatrixHeight", xnm).InnerText);

            int mMin = Convert.ToInt32(Math.Floor((BBox[0] - origin[0]) / xStep));
            int nMin = Convert.ToInt32(Math.Floor((origin[1] - BBox[1]) / yStep));
            int mMax = Convert.ToInt32(Math.Floor((BBox[2] - origin[0]) / xStep));
            int nMax = Convert.ToInt32(Math.Floor((origin[1] - BBox[3]) / yStep));

            mMax = Math.Min(mMax, mMin + matrixWidth);
            nMax = Math.Min(nMax, nMin + matrixHeight);

            foreach (XmlNode tmlNode in tmlNodes)
            {
                if (tmlNode.SelectSingleNode("ns:TileMatrix", xnm).InnerText == identifier)
                {
                    mMin = Math.Max(Convert.ToInt32(tmlNode.SelectSingleNode("ns:MinTileCol", xnm).InnerText), mMin);
                    nMin = Math.Max(Convert.ToInt32(tmlNode.SelectSingleNode("ns:MinTileRow", xnm).InnerText), nMin);
                    mMax = Math.Min(Convert.ToInt32(tmlNode.SelectSingleNode("ns:MaxTileCol", xnm).InnerText), mMax);
                    nMax = Math.Min(Convert.ToInt32(tmlNode.SelectSingleNode("ns:MaxTileRow", xnm).InnerText), nMax);
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
