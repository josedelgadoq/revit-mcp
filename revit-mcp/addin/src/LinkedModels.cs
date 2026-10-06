using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCP
{
    internal static class LinkedModels
    {
        internal static List<LinkedModelInfo> List()
        {
            var results = new List<LinkedModelInfo>();

            App.Queue.RunSync(() =>
            {
                var doc = App.UiApp?.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active Revit document.");

                var collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance));

                foreach (RevitLinkInstance link in collector)
                {
                    var linkDoc = link.GetLinkDocument();
                    results.Add(new LinkedModelInfo
                    {
                        ElementId = link.Id.Value,
                        Name      = link.Name,
                        Path      = linkDoc?.PathName ?? string.Empty,
                        IsLoaded  = linkDoc != null
                    });
                }
            });

            return results;
        }
    }

    internal sealed class LinkedModelInfo
    {
        public long   ElementId { get; set; }
        public string Name      { get; set; } = string.Empty;
        public string Path      { get; set; } = string.Empty;
        public bool   IsLoaded  { get; set; }
    }
}
