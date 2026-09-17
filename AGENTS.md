# Project agent instructions

## Blender MCP

- Blender MCP tools are installed and may be deferred instead of appearing in the initially displayed tool list.
- For any Blender-related request, search the complete runtime tool catalog (`ALL_TOOLS`) for names beginning with `mcp__blender__` before claiming that Blender is unavailable or disconnected.
- Invoke matching deferred tools through the runtime tool namespace (for example, `tools.mcp__blender__get_blendfile_summary_path_info({})`).
- Verify connectivity with a read-only Blender MCP call. Do not infer disconnection merely because Blender tools are absent from the abbreviated tool documentation.
- Do not ask the user to restart Codex or VS Code unless a native Blender MCP call actually fails.
