using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Configuration;
using Newtonsoft.Json;

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
                ITileListComposer offlineMap = new OfflineMapDefinition();
                offlineMap.FromQueryString(context.Request.QueryString);

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

                if (deleteCookie != null)
                {
                    string deleteCookieValue = context.Server.UrlDecode(deleteCookie.Value);
                    JsonReader jsonReader = new JsonTextReader(new System.IO.StringReader(deleteCookieValue));
                    JsonSerializer jsonSerializer = new JsonSerializer();

                    ITileListComposer offlineMapToDelete = null;
                    if (offlineMap as OfflineMapDefinition != null)
                    {
                        offlineMapToDelete = jsonSerializer.Deserialize<OfflineMapDefinition>(jsonReader);
                    }
                    else if (offlineMap as WMTSRequestData != null) {
                        offlineMapToDelete = jsonSerializer.Deserialize<WMTSRequestData>(jsonReader);
                    }
                    if (offlineMap.Equals(offlineMapToDelete))
                    {
                        // El manifiesto está marcado para borrar: Devolvemos un código HTTP 410 (recurso ya no existe) para que en cliente se borre la cache
                        context.Response.Cookies.Remove(cookieKey);
                        deleteCookie.Expires = DateTime.Now.AddDays(-10);
                        deleteCookie.Value = null;
                        context.Response.SetCookie(deleteCookie);
                        context.Response.StatusCode = 410;
                        context.Response.End();
                    }
                }

                tileListText.AppendLine(string.Join("\n", offlineMap.GetRequestList()));
            }

            context.Response.Write(manifestTemplate.Replace("{0}", tileListText.ToString()));
            context.Response.End();
        }
    }
}
