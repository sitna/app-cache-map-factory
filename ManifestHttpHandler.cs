using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Web.Configuration;

namespace AppCacheFactory
{
    class ManifestHttpHandler : System.Web.IHttpHandler
    {
        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(System.Web.HttpContext context)
        {
            context.Response.ContentType = "text/cache-manifest";

            string manifestTemplateKey = "AppCacheFactory.ManifestTemplate";
            string manifestTemplate = context.Application[manifestTemplateKey] as string;
            StringBuilder tileListText = new StringBuilder();

            if (manifestTemplate == null)
            {
                string manifestPath = WebConfigurationManager.AppSettings["AppCacheFactory.Manifest"];
                if (manifestPath != null)
                {
                    System.IO.StreamReader sr = System.IO.File.OpenText(context.Server.MapPath(manifestPath));
                    if (context.Request.Url.Scheme == "https")
                    {
                        string line;
                        StringBuilder sb = new StringBuilder();
                        do
                        {
                            line = sr.ReadLine();
                            if (line != null && line.StartsWith("http://"))
                            {
                                line = line.Replace("http://", "https://");
                            }
                            sb.AppendLine(line);
                        }
                        while (line != null);
                        manifestTemplate = sb.ToString();
                    }
                    else
                    {
                        manifestTemplate = sr.ReadToEnd();
                    }
                    sr.Close();

                    manifestTemplate = manifestTemplate.Replace("NETWORK:", "{0}\n\nNETWORK:");
                    context.Application.Add(manifestTemplateKey, manifestTemplate);
                }
            }


            string queryString = context.Request.Url.Query;
            if (queryString.Length > 0)
            {
                string encodedObjectString = context.Server.UrlDecode(queryString.Substring(1));
                string cookieKey = WebConfigurationManager.AppSettings["AppCacheFactory.Keys.Cookie"];
                if (cookieKey == null)
                {
                    cookieKey = "TC.offline.map.delete";
                }
                else
                {
                    cookieKey += "delete";
                }
                System.Web.HttpCookie deleteCookie = context.Request.Cookies.Get(cookieKey);
                string cachedTime = context.Cache.Get(encodedObjectString) as string;

                if (deleteCookie != null && context.Server.UrlDecode(deleteCookie.Value) == encodedObjectString)
                {
                    // El manifiesto está marcado para borrar: Devolvemos un código HTTP 410 (recurso ya no existe) para que en cliente se borre la cache
                    context.Response.Cookies.Remove(cookieKey);
                    deleteCookie.Expires = DateTime.Now.AddDays(-10);
                    deleteCookie.Value = null;
                    context.Response.SetCookie(deleteCookie); 
                    context.Response.StatusCode = 410;
                    context.Response.End();
                }

                string objectString = null;
                try
                {
                    objectString = Encoding.UTF8.GetString(Convert.FromBase64String(encodedObjectString));
                }
                catch (FormatException e)
                {
                    objectString = context.Server.UrlDecode(encodedObjectString);
                }
                if (objectString != null)
                {
                    JsonReader jsonReader = new JsonTextReader(new System.IO.StringReader(objectString));
                    JsonSerializer jsonSerializer = new JsonSerializer();

                    // Nueva definición corta, exige lectura de capabilities desde servidor
                    OfflineMapDefinition offlineMap = null;
                    try
                    {
                        offlineMap = jsonSerializer.Deserialize<OfflineMapDefinition>(jsonReader);
                        if (offlineMap != null)
                        {
                            tileListText.AppendLine(string.Join("\n", offlineMap.GetRequestList()));
                        }
                    }
                    catch (JsonSerializationException jse)
                    {
                        // Antigua definición larga
                        WMTSRequestData[] wmtsRequestDataList = null;

                        wmtsRequestDataList = jsonSerializer.Deserialize<WMTSRequestData[]>(jsonReader);
                        if (wmtsRequestDataList != null)
                        {
                            for (int i = 0; i < wmtsRequestDataList.Length; i++)
                            {
                                tileListText.AppendLine(string.Join("\n", wmtsRequestDataList[i].GetRequestList()));
                            }
                        }
                    }
                }
            }
            context.Response.Write(manifestTemplate.Replace("{0}", tileListText.ToString()));
            context.Response.End();
        }
    }
}
