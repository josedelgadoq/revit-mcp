using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCP
{
    /// <summary>
    /// Returns information about all Revit link instances in the active document.
    /// </summary>
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
                        ElementId = link.Id.IntegerValue,
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
        public int    ElementId { get; set; }
        public string Name      { get; set; } = string.Empty;
        public string Path      { get; set; } = string.Empty;
        public bool   IsLoaded  { get; set; }
    }
}
