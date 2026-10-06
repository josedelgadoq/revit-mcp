"""
Revit MCP Server
================
Exposes MCP tools that proxy to the RevitMCP C# add-in running inside Revit
on http://127.0.0.1:6767.
"""

import json
import httpx
from mcp.server.fastmcp import FastMCP

ADDIN_BASE = "http://127.0.0.1:6767"

mcp = FastMCP("revit-mcp", description="Clash detection between Revit linked models")


def _get(path: str) -> dict:
    resp = httpx.get(f"{ADDIN_BASE}{path}", timeout=60)
    resp.raise_for_status()
    return resp.json()


def _post(path: str, body: dict | None = None) -> dict:
    resp = httpx.post(f"{ADDIN_BASE}{path}", json=body or {}, timeout=300)
    resp.raise_for_status()
    return resp.json()


@mcp.tool()
def list_linked_models() -> str:
    """
    List all Revit link instances in the currently active Revit document.

    Returns a JSON array where each entry contains:
      - element_id  : integer element ID of the RevitLinkInstance
      - name        : display name of the link
      - path        : file path of the linked RVT file (empty if not loaded)
      - is_loaded   : whether the linked model is currently loaded
    """
    result = _get("/linked-models")
    return json.dumps(result, indent=2)


@mcp.tool()
def run_clash_detection(link_element_ids: list[int] | None = None) -> str:
    """
    Run geometry clash detection between the host model and linked Revit models.

    Args:
        link_element_ids: Optional list of RevitLinkInstance element IDs to check.
                          Pass None or an empty list to check all loaded linked models.

    Returns a JSON object with:
      - clash_count : total number of detected clashes
      - message     : summary message
      - clashes     : list of clash pairs, each containing host and link element details
                      (element ID, category, family, type, location)

    Note: This can take several minutes for large models.
    """
    body = {}
    if link_element_ids:
        body["link_element_ids"] = link_element_ids

    result = _post("/clash-detection", body)
    return json.dumps(result, indent=2)


@mcp.tool()
def get_clash_summary(link_element_ids: list[int] | None = None) -> str:
    """
    Run clash detection and return a human-readable summary grouped by category pair.

    Args:
        link_element_ids: Optional list of RevitLinkInstance element IDs to check.
                          Pass None or an empty list to check all loaded linked models.

    Returns a text summary of clashes grouped by (host category -> link category).
    """
    body = {}
    if link_element_ids:
        body["link_element_ids"] = link_element_ids

    result = _post("/clash-detection", body)
    clashes = result.get("clashes", [])

    if not clashes:
        return f"No clashes detected. {result.get('message', '')}"

    groups: dict[tuple, list] = {}
    for c in clashes:
        key = (c["host_category"], c["link_category"], c["link_name"])
        groups.setdefault(key, []).append(c)

    lines = [
        "Clash Detection Summary",
        "=======================",
        f"Total clashes: {result['clash_count']}",
        f"{result.get('message', '')}",
        "",
    ]
    for (hcat, lcat, lname), items in sorted(groups.items()):
        lines.append(f"[{hcat}] vs [{lcat}] in '{lname}': {len(items)} clash(es)")
        for item in items[:5]:
            lines.append(
                f"  Host #{item['host_element_id']} ({item['host_family']} / {item['host_type']}) @ {item['host_location']}"
                f"  \u2194  Link #{item['link_element_id']} ({item['link_family']} / {item['link_type']}) @ {item['link_location']}"
            )
        if len(items) > 5:
            lines.append(f"  ... and {len(items) - 5} more")
        lines.append("")

    return "\n".join(lines)


if __name__ == "__main__":
    mcp.run()
