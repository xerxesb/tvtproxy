using System.Text;
using System.Web;

namespace TvTProxy
{
    /// <summary>
    /// Based on http://www.codeproject.com/Articles/7135/Simple-HTTP-Reverse-Proxy-with-ASP-NET-and-IIS
    /// </summary>
    public class SiteProxy : IHttpHandler
    {
        public bool IsReusable
        {
            get { return false; }
        }

        public void ProcessRequest(HttpContext context)
        {
            var server = new RemoteServer(context);

            var request = server.GetRequest();

            var response = server.GetResponse(request);
            var responseData = server.GetResponseStreamBytes(response);

            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentType = response.ContentType;
            context.Response.OutputStream.Write(responseData, 0, responseData.Length);

            server.SetContextCookies(response);

            response.Close();
            context.Response.End();
        }
    }
}
