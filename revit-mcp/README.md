# Revit MCP — Clash Detection for Linked Models

Chat naturally with Claude to find geometry clashes between Revit linked models.

```
User: find all clashes between the structural and MEP linked models
Claude: (calls run_clash_detection) → returns 47 clashes grouped by category …
```

---

## How it works

```
Claude Desktop  ──MCP──►  Python server (server.py)  ──HTTP──►  RevitMCP.dll (inside Revit)
```

- **RevitMCP.dll** is a Revit add-in (C#) that starts a local HTTP server on `127.0.0.1:6767` the moment Revit launches.
- **server.py** is a thin Python MCP server that wraps those HTTP endpoints as MCP tools.
- **Claude Desktop** calls the MCP tools when you ask about clashes in the chat.

---

## Requirements

| Component | Requirement |
|-----------|-------------|
| Revit | 2027 |
| .NET | Framework 4.8 |
| Python | 3.11+ |
| Claude Desktop | Latest — [download](https://claude.ai/download) |

---

## 1 — Build the Revit add-in

```powershell
cd revit-mcp\addin
dotnet build -c Release
# Output: bin\Release\net48\RevitMCP.dll
```

If `RevitAPI.dll` is not found, confirm Revit 2027 is at `C:\Program Files\Autodesk\Revit 2027\`. If your path differs, edit the `<HintPath>` entries in `RevitMCP.csproj`.

---

## 2 — Install the Revit add-in

```powershell
$dest = "$env:APPDATA\Autodesk\Revit\Addins\2027"
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item revit-mcp\addin\RevitMCP.addin $dest
Copy-Item revit-mcp\addin\bin\Release\net48\RevitMCP.dll $dest
Copy-Item revit-mcp\addin\bin\Release\net48\Newtonsoft.Json.dll $dest
```

Restart Revit. Verify the HTTP server is running:

```powershell
Invoke-RestMethod http://127.0.0.1:6767/linked-models
```

---

## 3 — Set up the Python MCP server

```powershell
cd revit-mcp\server
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
```

---

## 4 — Configure Claude Desktop

Open `%APPDATA%\Claude\claude_desktop_config.json` and add:

```json
{
  "mcpServers": {
    "revit-mcp": {
      "command": "C:\\path\\to\\revit-mcp\\server\\.venv\\Scripts\\python.exe",
      "args": ["C:\\path\\to\\revit-mcp\\server\\server.py"]
    }
  }
}
```

Replace `C:\\path\\to\\revit-mcp` with the actual path where you cloned this repo. Restart Claude Desktop.

---

## 5 — Example prompts

```
List all linked models in my current project.
Run clash detection between all linked models and show me the results.
Give me a summary of clashes grouped by category.
```

---

## MCP tools

| Tool | Description |
|------|-------------|
| `list_linked_models` | Lists all RevitLinkInstance elements in the active document |
| `run_clash_detection` | Full clash detection — returns raw JSON with every clash pair |
| `get_clash_summary` | Clash detection with a human-readable grouped summary |

---

## Troubleshooting

**Add-in doesn't load** — check Revit journals at `%LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit 2027\Journals\`.

**Port 6767 in use** — change the prefix in `App.cs` and `ADDIN_BASE` in `server.py`.

**Clash detection times out** — increase the `timeout=300` in `server.py` for very large models.

**Claude Desktop doesn't see the server** — confirm the JSON is valid (no trailing commas) and restart Claude Desktop after editing the config.
