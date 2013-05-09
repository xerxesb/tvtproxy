using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Web;
using System.Linq;

namespace TvTProxy
{
    public class RemoteServer
    {
        readonly HttpContext _context;
        public string RemoteUrl { get; private set; }

        public RemoteServer(HttpContext context)
        {
            _context = context;

            var serverUrl = ConfigurationSettings.AppSettings["RemoteWebSite"];

            Uri proxiedUri;
            Uri.TryCreate(serverUrl + context.Request.Url.AbsolutePath, UriKind.Absolute, out proxiedUri);
            RemoteUrl = proxiedUri.ToString();
        }

        public HttpWebRequest GetRequest()
        {
            var request = (HttpWebRequest)WebRequest.Create(RemoteUrl);

            request.Method = _context.Request.HttpMethod;
            request.UserAgent = _context.Request.UserAgent;
            request.KeepAlive = true;
            request.CookieContainer = BuildCookieContainer();

            if (request.Method == "POST")
            {
                var clientStream = _context.Request.InputStream;
                var clientPostData = new byte[_context.Request.InputStream.Length];
                clientStream.Read(clientPostData, 0, (int)_context.Request.InputStream.Length);

                request.ContentType = _context.Request.ContentType;
                request.ContentLength = clientPostData.Length;

                var stream = request.GetRequestStream();
                stream.Write(clientPostData, 0, clientPostData.Length);
                stream.Close();
            }

            return request;
        }

        public HttpWebResponse GetResponse(HttpWebRequest request)
        {
            return (HttpWebResponse)request.GetResponse();
        }

        private CookieContainer BuildCookieContainer()
        {
            var cookieContainer = new CookieContainer();

            // Send Cookie extracted from the incoming request
            for (var i = 0; i < _context.Request.Cookies.Count; i++)
            {
                var navigatorCookie = _context.Request.Cookies[i];
                var c = new Cookie(navigatorCookie.Name, navigatorCookie.Value);
                c.Domain = new Uri(RemoteUrl).Host;
                c.Expires = navigatorCookie.Expires;
                c.HttpOnly = navigatorCookie.HttpOnly;
                c.Path = navigatorCookie.Path;
                c.Secure = navigatorCookie.Secure;
                cookieContainer.Add(c);
            }

            return cookieContainer;
        }

        public byte[] GetResponseStreamBytes(HttpWebResponse response)
        {
            const int bufferSize = 256;
            var buffer = new byte[bufferSize];
            var memoryStream = new MemoryStream();

            var responseStream = response.GetResponseStream();
            var remoteResponseCount = responseStream.Read(buffer, 0, bufferSize);

            while (remoteResponseCount > 0)
            {
                memoryStream.Write(buffer, 0, remoteResponseCount);
                remoteResponseCount = responseStream.Read(buffer, 0, bufferSize);
            }

            var responseData = memoryStream.ToArray();

            memoryStream.Close();
            responseStream.Close();

            memoryStream.Dispose();
            responseStream.Dispose();

            return responseData;
        }

        public void SetContextCookies(HttpWebResponse response)
        {
            _context.Response.Cookies.Clear();

            foreach (Cookie receivedCookie in response.Cookies)
            {
                var c = new HttpCookie(receivedCookie.Name, receivedCookie.Value);
                c.Domain = _context.Request.Url.Host;
                c.Expires = receivedCookie.Expires;
                c.HttpOnly = receivedCookie.HttpOnly;
                c.Path = receivedCookie.Path;
                c.Secure = receivedCookie.Secure;
                _context.Response.Cookies.Add(c);
            }
        }
    }
}