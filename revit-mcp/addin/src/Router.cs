using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace RevitMCP
{
    /// <summary>
    /// Maps incoming HTTP requests to handler methods and writes JSON responses.
    /// Runs on thread-pool threads; calls that need the Revit API are dispatched
    /// to the main thread via App.Queue.
    /// </summary>
    internal static class Router
    {
        internal static void Handle(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var resp = ctx.Response;
            resp.ContentType = "application/json; charset=utf-8";

            try
            {
                object result = (req.HttpMethod, req.Url?.AbsolutePath) switch
                {
                    ("GET",  "/linked-models")   => LinkedModels.List(),
                    ("POST", "/clash-detection") => ClashDetection.Run(ReadBody(req)),
                    _ => throw new InvalidOperationException($"Unknown route: {req.HttpMethod} {req.Url?.AbsolutePath}")
                };

                Write(resp, 200, result);
            }
            catch (Exception ex)
            {
                Write(resp, 500, new { error = ex.Message });
            }
        }

        private static string ReadBody(HttpListenerRequest req)
        {
            using var sr = new StreamReader(req.InputStream, req.ContentEncoding);
            return sr.ReadToEnd();
        }

        private static void Write(HttpListenerResponse resp, int status, object payload)
        {
            resp.StatusCode = status;
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload, Formatting.Indented));
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.OutputStream.Close();
        }
    }
}
