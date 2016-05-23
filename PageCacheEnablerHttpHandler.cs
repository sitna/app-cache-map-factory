using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;

namespace AppCacheFactory
{
    class PageCacheEnablerHttpHandler : System.Web.IHttpHandler
    {
        public bool IsReusable
        {
            get { return false; }
        }

        public void ProcessRequest(System.Web.HttpContext context)
        {
            HttpRequest request = context.Request;

            string offlineSchemaKey = WebConfigurationManager.AppSettings["AppCacheFactory.Keys.OfflineSchema"];
            if (offlineSchemaKey == null)
            {
                offlineSchemaKey = "offline-schema";
            }
            string offlineSchemaValue = request.QueryString[offlineSchemaKey];
            string mapNameKey = WebConfigurationManager.AppSettings["AppCacheFactory.Keys.MapName"];
            if (mapNameKey == null)
            {
                mapNameKey = "map-name";
            }

            string targetUrl = WebConfigurationManager.AppSettings["AppCacheFactory.DefaultPage"];
            if (targetUrl == null)
            {
                targetUrl = string.Format("{0}://{1}:{2}/", request.Url.Scheme, request.Url.Host, request.Url.Port);
            }

            #region eliminación de parámetros de querystring que están dirigidos a este handler
            Regex schemaRe = new Regex(offlineSchemaKey + "=[^&]*", RegexOptions.IgnoreCase);
            Regex nameRe = new Regex(mapNameKey + "=[^&]*", RegexOptions.IgnoreCase);
            string query = request.Url.Query
                .Replace(schemaRe.Match(request.Url.Query).ToString(), "")
                .Replace(nameRe.Match(request.Url.Query).ToString(), "")
                .Replace("?&", "?")
                .Replace("&&", "&");
            targetUrl += query + request.Url.Fragment;
            #endregion

            HttpWebRequest innerRequest = (HttpWebRequest)WebRequest.Create(targetUrl);
            //if (this.UseHttps)
            //{
            //    innerRequest.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(AcceptAllCertifications);
            //}
            
            #region réplica de POST a la petición interna
            if (request.InputStream.Length > 0)
            {
                //byte[] postBytes = null;
                //long inputStreamLength = request.InputStream.Length;
                //postBytes = new byte[inputStreamLength];
                //request.InputStream.Read(postBytes, 0, (int)inputStreamLength);

                //innerRequest.ContentLength = postBytes.Length;
                //using (Stream outputStream = innerRequest.GetRequestStream())
                //{
                //    outputStream.Write(postBytes, 0, postBytes.Length);
                //}
            }
            #endregion

            HttpWebResponse innerResponse = (HttpWebResponse)innerRequest.GetResponse();
            Encoding inputEncoding = Encoding.GetEncoding(innerResponse.ContentEncoding == "" ? innerResponse.CharacterSet : innerResponse.ContentEncoding);
            StreamReader innerResponseReader = new StreamReader(innerResponse.GetResponseStream(), inputEncoding);
            string responseText = innerResponseReader.ReadToEnd();

            Regex htmlRe = new Regex("<html(?:\\s?.*)>", RegexOptions.IgnoreCase);
            string htmlPart = htmlRe.Match(responseText).ToString();
            Regex manifestRe = new Regex("manifest=\".*\"", RegexOptions.IgnoreCase);
            string manifestPart = manifestRe.Match(htmlPart).ToString();
            string newHtmlPart = null;

            #region inserción de atributo manifest
            if (htmlPart.Length > 0)
            {
                if (manifestPart.Length == 0)
                {
                    newHtmlPart = htmlPart.Replace(">", string.Format(" manifest=\"manifest?{0}\">", offlineSchemaValue));
                }
                else
                {
                    newHtmlPart = htmlPart.Replace(manifestPart, string.Format("manifest=\"manifest?{0}\"", request.QueryString[offlineSchemaKey]));
                }
                responseText = responseText.Replace(htmlPart, newHtmlPart);
            }
            #endregion

            context.Response.ContentType = innerResponse.ContentType;
            context.Response.Write(responseText);
            context.Response.End();
        }
    }
}
