using System;
using System.Collections.Generic;
using System.Threading;

namespace RevitMCP
{
    /// <summary>
    /// Thread-safe queue that pairs a work item with a completion signal.
    /// HTTP threads enqueue work and block; Revit's Idling thread drains the queue.
    /// </summary>
    internal class RequestQueue
    {
        private readonly Queue<(Action work, ManualResetEventSlim done)> _q = new();
        private readonly object _lock = new();

        internal void Enqueue(Action work, ManualResetEventSlim done)
        {
            lock (_lock) _q.Enqueue((work, done));
        }

        internal void ProcessAll()
        {
            while (true)
            {
                (Action work, ManualResetEventSlim done) item;
                lock (_lock)
                {
                    if (_q.Count == 0) break;
                    item = _q.Dequeue();
                }
                try { item.work(); }
                catch { /* swallow — caller's result captures the exception */ }
                finally { item.done.Set(); }
            }
        }

        /// <summary>
        /// Runs <paramref name="work"/> on Revit's main thread and blocks until done.
        /// Returns after at most <paramref name="timeoutMs"/> milliseconds.
        /// </summary>
        internal void RunSync(Action work, int timeoutMs = 30_000)
        {
            using var done = new ManualResetEventSlim(false);
            Enqueue(work, done);
            done.Wait(timeoutMs);
        }
    }
}
