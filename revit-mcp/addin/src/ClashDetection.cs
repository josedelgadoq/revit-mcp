using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;

namespace RevitMCP
{
    internal static class ClashDetection
    {
        internal static ClashReport Run(string requestBody)
        {
            var request = string.IsNullOrWhiteSpace(requestBody)
                ? new ClashRequest()
                : JsonConvert.DeserializeObject<ClashRequest>(requestBody) ?? new ClashRequest();

            var report = new ClashReport();

            App.Queue.RunSync(() =>
            {
                var doc = App.UiApp?.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active Revit document.");

                var links = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .Where(l =>
                        l.GetLinkDocument() != null &&
                        (request.LinkElementIds == null ||
                         request.LinkElementIds.Count == 0 ||
                         request.LinkElementIds.Contains(l.Id.Value)))
                    .ToList();

                if (links.Count == 0)
                {
                    report.Message = "No loaded linked models found (or none matched the requested IDs).";
                    return;
                }

                var hostElements = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .Where(e => e.Category != null)
                    .ToList();

                report.Message = $"Checked {hostElements.Count} host elements against {links.Count} linked model(s).";

                var options = new Options { ComputeReferences = false, IncludeNonVisibleObjects = false };

                foreach (var link in links)
                {
                    var linkDoc   = link.GetLinkDocument()!;
                    var transform = link.GetTotalTransform();

                    var linkElements = new FilteredElementCollector(linkDoc)
                        .WhereElementIsNotElementType()
                        .Where(e => e.Category != null)
                        .ToList();

                    foreach (var linkElem in linkElements)
                    {
                        Solid? linkSolid = GetUnionSolid(linkElem, options, transform);
                        if (linkSolid == null || linkSolid.Volume < 1e-9) continue;

                        BoundingBoxXYZ bb = linkSolid.GetBoundingBox();
                        var outline = new Outline(bb.Min, bb.Max);

                        var bbFilter = new BoundingBoxIntersectsFilter(outline, tolerance: 0.01);
                        var hostCandidates = new FilteredElementCollector(doc)
                            .WhereElementIsNotElementType()
                            .WherePasses(bbFilter)
                            .Where(e => e.Category != null)
                            .ToList();

                        foreach (var hostElem in hostCandidates)
                        {
                            if (SolidsIntersect(hostElem, options, linkSolid))
                            {
                                report.Clashes.Add(new ClashResult
                                {
                                    HostElementId = hostElem.Id.Value,
                                    HostCategory  = hostElem.Category?.Name ?? "",
                                    HostFamily    = GetFamilyName(hostElem),
                                    HostType      = GetTypeName(hostElem, doc),
                                    HostLocation  = GetLocationString(hostElem),

                                    LinkName      = link.Name,
                                    LinkElementId = linkElem.Id.Value,
                                    LinkCategory  = linkElem.Category?.Name ?? "",
                                    LinkFamily    = GetFamilyName(linkElem),
                                    LinkType      = GetTypeName(linkElem, linkDoc),
                                    LinkLocation  = GetTransformedLocationString(linkElem, transform),
                                });
                            }
                        }
                    }
                }
            });

            return report;
        }

        private static Solid? GetUnionSolid(Element elem, Options options, Transform transform)
        {
            GeometryElement? geom;
            try { geom = elem.get_Geometry(options); }
            catch { return null; }
            if (geom == null) return null;

            Solid? result = null;
            foreach (var obj in geom)
            {
                Solid? s = obj as Solid;
                if (s == null && obj is GeometryInstance gi)
                    s = gi.GetInstanceGeometry()?.OfType<Solid>().FirstOrDefault(x => x.Volume > 1e-9);

                if (s == null || s.Volume < 1e-9) continue;

                var transformed = SolidUtils.CreateTransformed(s, transform);
                result = result == null
                    ? transformed
                    : BooleanOperationsUtils.ExecuteBooleanOperation(result, transformed, BooleanOperationsType.Union);
            }
            return result;
        }

        private static bool SolidsIntersect(Element hostElem, Options options, Solid linkSolid)
        {
            Solid? hostSolid = GetUnionSolid(hostElem, options, Transform.Identity);
            if (hostSolid == null || hostSolid.Volume < 1e-9) return false;

            try
            {
                var intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                    hostSolid, linkSolid, BooleanOperationsType.Intersect);
                return intersection != null && intersection.Volume > 1e-9;
            }
            catch { return false; }
        }

        private static string GetFamilyName(Element e) =>
            (e as FamilyInstance)?.Symbol?.FamilyName ?? e.GetType().Name;

        private static string GetTypeName(Element e, Document doc)
        {
            var typeId = e.GetTypeId();
            if (typeId == null || typeId == ElementId.InvalidElementId) return "";
            return doc.GetElement(typeId)?.Name ?? "";
        }

        private static string GetLocationString(Element e) =>
            e.Location switch
            {
                LocationPoint lp => $"({lp.Point.X:F2}, {lp.Point.Y:F2}, {lp.Point.Z:F2})",
                LocationCurve lc => $"curve from ({lc.Curve.GetEndPoint(0).X:F2},{lc.Curve.GetEndPoint(0).Y:F2},{lc.Curve.GetEndPoint(0).Z:F2}) to ({lc.Curve.GetEndPoint(1).X:F2},{lc.Curve.GetEndPoint(1).Y:F2},{lc.Curve.GetEndPoint(1).Z:F2})",
                _ => ""
            };

        private static string GetTransformedLocationString(Element e, Transform t) =>
            e.Location switch
            {
                LocationPoint lp => $"({t.OfPoint(lp.Point).X:F2}, {t.OfPoint(lp.Point).Y:F2}, {t.OfPoint(lp.Point).Z:F2})",
                LocationCurve lc => $"curve from ({t.OfPoint(lc.Curve.GetEndPoint(0)).X:F2},{t.OfPoint(lc.Curve.GetEndPoint(0)).Y:F2},{t.OfPoint(lc.Curve.GetEndPoint(0)).Z:F2}) to ({t.OfPoint(lc.Curve.GetEndPoint(1)).X:F2},{t.OfPoint(lc.Curve.GetEndPoint(1)).Y:F2},{t.OfPoint(lc.Curve.GetEndPoint(1)).Z:F2})",
                _ => ""
            };
    }

    internal sealed class ClashRequest
    {
        [JsonProperty("link_element_ids")]
        public List<long>? LinkElementIds { get; set; }
    }

    internal sealed class ClashReport
    {
        public string            Message    { get; set; } = string.Empty;
        public List<ClashResult> Clashes    { get; set; } = new();
        public int               ClashCount => Clashes.Count;
    }

    internal sealed class ClashResult
    {
        public long   HostElementId { get; set; }
        public string HostCategory  { get; set; } = string.Empty;
        public string HostFamily    { get; set; } = string.Empty;
        public string HostType      { get; set; } = string.Empty;
        public string HostLocation  { get; set; } = string.Empty;

        public string LinkName      { get; set; } = string.Empty;
        public long   LinkElementId { get; set; }
        public string LinkCategory  { get; set; } = string.Empty;
        public string LinkFamily    { get; set; } = string.Empty;
        public string LinkType      { get; set; } = string.Empty;
        public string LinkLocation  { get; set; } = string.Empty;
    }
}
