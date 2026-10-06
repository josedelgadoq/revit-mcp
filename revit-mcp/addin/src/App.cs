using System;
using System.Net;
using System.Threading;
using Autodesk.Revit.UI;

namespace RevitMCP
{
    /// <summary>
    /// Revit external application that starts the MCP bridge HTTP server.
    /// The server runs on a background thread and dispatches geometry work
    /// back to Revit's main thread via the Idling event.
    /// </summary>
    public class App : IExternalApplication
    {
        internal static UIApplication? UiApp;
        internal static HttpListener? Listener;
        private static Thread? _listenerThread;

        // Requests queued from the HTTP thread, executed on Revit's main thread.
        internal static readonly RequestQueue Queue = new();

        public Result OnStartup(UIControlledApplication app)
        {
            app.Idling += OnIdling;

            Listener = new HttpListener();
            Listener.Prefixes.Add("http://127.0.0.1:6767/");
            Listener.Start();

            _listenerThread = new Thread(ListenerLoop) { IsBackground = true, Name = "RevitMCP-HTTP" };
            _listenerThread.Start();

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            app.Idling -= OnIdling;
            Listener?.Stop();
            return Result.Succeeded;
        }

        private static void OnIdling(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
            UiApp = sender as UIApplication;
            Queue.ProcessAll();
        }

        private static void ListenerLoop()
        {
            while (Listener?.IsListening == true)
            {
                try
                {
                    var ctx = Listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => Router.Handle(ctx));
                }
                catch (HttpListenerException)
                {
                    // Listener stopped — normal shutdown.
                    break;
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("RevitMCP", $"HTTP listener error: {ex.Message}");
                }
            }
        }
    }
}
