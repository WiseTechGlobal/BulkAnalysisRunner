# Bulk Analysis Runner — MCP server

A local [Model Context Protocol](https://modelcontextprotocol.io) server (stdio, `net10.0`) that
lets LLMs / AI agents drive the Bulk Analysis Runner engine: **discover** analyzer diagnostics in a
solution or project, **group / classify** them, and **apply code fixes** (including bulk FixAll
operations) for the rules that have a code fix provider.

It wraps the same `WTG.BulkAnalysis.Core` engine used by the CLI, exposed through a stateful
in-memory `AnalysisSession` so the (expensive) workspace load happens once per session and is reused
across calls.

## Tools

| Tool | Purpose |
| --- | --- |
| `open_analysis` | Open a `.sln`/`.slnx`/`.csproj` (or a directory/branch with a `Build.xml`) and return a `sessionId`. Keeps the workspace + analyzers loaded in memory. |
| `list_diagnostics` | Run all analyzers and return a grouped summary (`groupBy`: `rule` \| `severity` \| `project` \| `fixability`). Each group reports its `count` and `hasCodeFix` so you can see what is bulk-fixable. Optional `ruleIds` / `minSeverity` filters. |
| `get_diagnostics` | List the individual occurrences (file, line, column, message) for one rule id, paged (`skip`/`take`). |
| `apply_fixes` | Run FixAll for the given `;`-separated `ruleIds` until stable, writing changes to disk. Returns operations applied, changed files, and remaining counts. |
| `close_analysis` | Dispose a session and release its workspace. |

Re-running `list_diagnostics` re-analyzes the current (in-memory, fix-updated) workspace, so it
reflects fixes applied in the same session. To pick up edits made outside the session, re-open it.

## Running

Build once, then point your MCP client at the built assembly (using a pre-built DLL keeps the
protocol's stdout clean — avoid `dotnet run`, which writes build output to stdout):

```bash
dotnet build src/WTG.BulkAnalysis.Mcp -c Release
```

Builds use the artifacts output layout, so the server lands at
`artifacts/bin/WTG.BulkAnalysis.Mcp/release/BulkAnalysisRunner.Mcp.dll` (each project gets its own
isolated folder). For a standalone deployment, `dotnet publish src/WTG.BulkAnalysis.Mcp -c Release -o
<dir>` produces a self-contained folder you can point at instead.

### Claude Code / VS Code (`.mcp.json` or `.vscode/mcp.json`)

```json
{
  "mcpServers": {
    "bulk-analysis": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "/absolute/path/to/BulkAnalysisRunner/artifacts/bin/WTG.BulkAnalysis.Mcp/release/BulkAnalysisRunner.Mcp.dll"
      ]
    }
  }
}
```

Then, from the agent: `open_analysis` on a solution, `list_diagnostics` to triage, `apply_fixes`
for a fixable rule, and `close_analysis` when done.

## Requirements & notes

- **.NET 10 SDK** must be installed; the server registers the SDK's MSBuild via `MSBuildLocator`.
- The engine uses **Roslyn 5.x** to match the .NET 10 SDK's compiler, so it parses modern C#
  (e.g. C# 14). The reported diagnostics reflect the project's effective `.editorconfig` / global
  analyzer config, and `list_diagnostics` defaults to Warning+ to match what a build surfaces.
- First-time `open_analysis` on a large solution can take a few minutes (MSBuild evaluates the
  project graph out-of-process); subsequent calls in the same session reuse the loaded workspace.
