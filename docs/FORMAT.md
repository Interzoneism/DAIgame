# Formatting policy for DAIgame

This file lists the canonical formatting rules and where they are defined.

## Canonical sources (in order)
- `.editorconfig` (repo root) — **authoritative** for whitespace, braces, naming, and many C# rules. ✅
- `omnisharp.json` — OmniSharp formatting options; now set to *respect* `.editorconfig` and to match import-grouping semantics. ✅
- `.vscode/settings.json` — sets the C# default formatter and per-language format-on-save settings so only one formatter is used. ✅
- `.clang-format` — used for shaders/C/C++-style files and **matches** the shader EditorConfig (which uses tabs for shader files). ✅

## What I changed
- `omnisharp.json`: set `SeparateImportDirectiveGroups` to `false` to match `dotnet_separate_import_directive_groups = false` in `.editorconfig`.
- `.vscode/settings.json`: set `"editor.defaultFormatter" : "ms-dotnettools.csharp"` for `[csharp]` so a single formatter runs on save.
- Added this `docs/FORMAT.md` to document the decisions.

## How to verify locally
1. Ensure you have the recommended extensions installed (see `.vscode/extensions.json`) — at minimum `EditorConfig.EditorConfig` and `ms-dotnettools.csharp`.
2. Open any C# file (e.g., `scripts/Player/PlayerController.cs`), make an intentional format change (e.g., convert two-space indentation to tabs or move an import), and save the file.
3. Confirm that the saved file is formatted to 2-space indentation and that using directives follow the single-group policy (no extra blank line inserted by separate groups).

If you still see conflicting formatting after this, re-open VS Code or reload the window so the updated settings and OmniSharp config are applied.

---

If you want, I can also add a small test (`EditorConfig` or `dotnet format`) to CI to assert there are no formatting diffs across the repository.
