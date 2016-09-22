﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace SITNA.AppCacheFactory
{
    public class OfflineMapDefinition : ITileListComposer
    {
        public double[] BBox { get; set; }
        public double Res { get; set; }
        public string[] URL { get; set; }
        public string[] TMS { get; set; }
        public string[] Format { get; set; }
        public WMTSLayerDefinition[] Layers { get; set; }

        public List<string> GetRequestList()
        {
            List<string> result = new List<string>();
            Dictionary<string, XmlDocument> capabilities = new Dictionary<string, XmlDocument>();
            if (Layers != null)
            {
                for (int i = 0; i < Layers.Length; i++)
                {
                    bool isKVP = false;
                    WMTSLayerDefinition layer = Layers[i];
                    XmlDocument xmlDoc;
                    string layerUrl = URL[layer.UrlIdx];
                    if (layerUrl.EndsWith("?"))
                    {
                        layerUrl = layerUrl.Substring(0, layerUrl.Length - 1);
                    }
                    string capabilitiesUrl = null;
                    string restfulCapabilitiesUrl = layerUrl + "/1.0.0/WMTSCapabilities.xml";
                    string kvpCapabilitiesUrl = layerUrl + "?SERVICE=WMTS&VERSION=1.0.0&REQUEST=GetCapabilities";
                    if (capabilities.ContainsKey(restfulCapabilitiesUrl))
                    {
                        capabilitiesUrl = restfulCapabilitiesUrl;
                        xmlDoc = capabilities[capabilitiesUrl];
                    }
                    else if (capabilities.ContainsKey(kvpCapabilitiesUrl))
                    {
                        capabilitiesUrl = kvpCapabilitiesUrl;
                        xmlDoc = capabilities[capabilitiesUrl];
                    }
                    else
                    {
                        xmlDoc = new XmlDocument();
                        XmlReader xmlReader = null;
                        xmlDoc.XmlResolver = null; // Evita que se descargue la DTD
                        capabilitiesUrl = restfulCapabilitiesUrl;
                        try
                        {
                            xmlReader = XmlReader.Create(capabilitiesUrl);
                        }
                        catch (Exception e)
                        {
                            if (e is FileNotFoundException || e is WebException)
                            {
                                isKVP = true;
                                capabilitiesUrl = kvpCapabilitiesUrl;
                                xmlReader = XmlReader.Create(capabilitiesUrl);
                            }
                        }
                        xmlDoc.Load(xmlReader);
                        capabilities[capabilitiesUrl] = xmlDoc;
                    }
                    bool isSSL = capabilitiesUrl.StartsWith("https:");
                    result.Add(capabilitiesUrl);


                    var xnm = new XmlNamespaceManager(xmlDoc.NameTable);
                    xnm.AddNamespace("ns", "http://www.opengis.net/wmts/1.0");
                    xnm.AddNamespace("ows", "http://www.opengis.net/ows/1.1");
                    XmlNode layerNode = xmlDoc.SelectSingleNode(string.Format("/ns:Capabilities/ns:Contents/ns:Layer[ows:Identifier='{0}']", layer.Id), xnm);

                    if (layerNode != null)
                    {
                        string urlPattern = null;
                        var tms = TMS[layer.TmsIdx];
                        if (isKVP)
                        {
                            urlPattern = string.Format("{0}?layer={1}&style=default&tilematrixset={2}&Service=WMTS&Request=GetTile&Version=1.0.0&Format=image%2F{3}&TileMatrix={{TileMatrix}}&TileCol={{TileCol}}&TileRow={{TileRow}}",
                                layerUrl, layer.Id, tms.Replace(":", "%3A"), Format[layer.FormatIdx].Replace(":", "%3A"));
                        }
                        else
                        {
                            XmlNode urlPatternNode = layerNode.SelectSingleNode(string.Format("ns:ResourceURL[@format='image/{0}'][@resourceType='tile']", Format[layer.FormatIdx]), xnm);
                            if (urlPatternNode != null)
                            {
                                urlPattern = urlPatternNode.Attributes["template"].Value;
                            }
                        }
                        if (urlPattern != null)
                        {
                            if (isSSL)
                            {
                                urlPattern = urlPattern.Replace("http:", "https:");
                            }
                            XmlNodeList tmlNodes = layerNode.SelectNodes(string.Format("ns:TileMatrixSetLink[ns:TileMatrixSet='{0}']/ns:TileMatrixSetLimits/ns:TileMatrixLimits", tms), xnm);
                            XmlNodeList tmNodes = xmlDoc.SelectNodes(string.Format("/ns:Capabilities/ns:Contents/ns:TileMatrixSet[ows:Identifier='{0}']/ns:TileMatrix", tms), xnm);
                            XmlNode tmNode;
                            for (int j = 0; j < tmNodes.Count; j++)
                            {
                                tmNode = tmNodes[j];
                                int[] indexRange = GetIndexRange(tmNode, tmlNodes, xnm);
                                if (indexRange != null)
                                {
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
            }
            return result;
        }

        protected int[] GetIndexRange(XmlNode tmNode, XmlNodeList tmlNodes, XmlNamespaceManager xnm)
        {
            int[] result = null;
            string identifier = tmNode.SelectSingleNode("ows:Identifier", xnm).InnerText;

            string[] topLeftCorner = tmNode.SelectSingleNode("ns:TopLeftCorner", xnm).InnerText.Split(' ');
            double tl0 = Convert.ToDouble(topLeftCorner[0], CultureInfo.InvariantCulture);
            double tl1 = Convert.ToDouble(topLeftCorner[1], CultureInfo.InvariantCulture);
            double[] origin = new double[2] { tl0, tl1 };

            double scaleDenominator = Convert.ToDouble(tmNode.SelectSingleNode("ns:ScaleDenominator", xnm).InnerText, CultureInfo.InvariantCulture);
            int tileWidth = Convert.ToInt32(tmNode.SelectSingleNode("ns:TileWidth", xnm).InnerText);
            int tileHeight = Convert.ToInt32(tmNode.SelectSingleNode("ns:TileHeight", xnm).InnerText);
            double resolution = 0.00028 * scaleDenominator; // OGC assumes 0.28 mm / pixel
            if (resolution >= Res)
            {
                result = new int[4];
                double xStep = resolution * tileWidth;
                double yStep = resolution * tileHeight;

                int matrixWidth = Convert.ToInt32(tmNode.SelectSingleNode("ns:MatrixWidth", xnm).InnerText);
                int matrixHeight = Convert.ToInt32(tmNode.SelectSingleNode("ns:MatrixHeight", xnm).InnerText);

                int mMin = Convert.ToInt32(Math.Floor((BBox[0] - origin[0]) / xStep));
                int nMin = Convert.ToInt32(Math.Floor((origin[1] - BBox[3]) / yStep));
                int mMax = Convert.ToInt32(Math.Floor((BBox[2] - origin[0]) / xStep));
                int nMax = Convert.ToInt32(Math.Floor((origin[1] - BBox[1]) / yStep));

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
            }
            return result;
        }

        private string[] EscapeUriStringArray(string[] a)
        {
            return a.Select(x => Uri.EscapeUriString(x)).ToArray();
        }

        public string ToQueryString()
        {
            StringBuilder qs = new StringBuilder();
            qs.AppendFormat("B={0}&R={1}&U={2}&T={3}&F={4}&L={5}", 
                string.Join(",", BBox.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray()), 
                Res.ToString(CultureInfo.InvariantCulture),
                string.Join(",", EscapeUriStringArray(URL)),
                string.Join(",", EscapeUriStringArray(TMS)),
                string.Join(",", EscapeUriStringArray(Format)),
                string.Join(",", Layers.Select(x => string.Format("u-{0}_i-{1}_t-{2}_f-{3}", x.UrlIdx, x.Id, x.TmsIdx, x.FormatIdx)).ToArray()));
            return qs.ToString();
        }

        public void FromQueryString(NameValueCollection queryString)
        {
            BBox = queryString["B"].Split(',').Select(x => Convert.ToDouble(x, CultureInfo.InvariantCulture)).ToArray();
            Res = Convert.ToDouble(queryString["R"], CultureInfo.InvariantCulture);
            URL = queryString["U"].Split(',');
            TMS = queryString["T"].Split(',');
            Format = queryString["F"].Split(',');
            Layers = queryString["L"].Split(',').Select(x => WMTSLayerDefinition.FromQueryStringPart(x)).ToArray();
        }

        public override bool Equals(object other)
        {
            bool result = false;
            OfflineMapDefinition theOther = other as OfflineMapDefinition;
            if (theOther != null)
            {
                result = BBox.SequenceEqual(theOther.BBox) &&
                    Res == theOther.Res &&
                    URL.SequenceEqual(theOther.URL) &&
                    TMS.SequenceEqual(theOther.TMS) &&
                    Format.SequenceEqual(theOther.Format) &&
                    Layers.SequenceEqual(theOther.Layers);
            }
            return result;
        }
    }
}
