# PhotoGallery Script Runner

Interactive console to guide and partially automate end-to-end PhotoGallery Hot Reload demo steps.

Features:
- Prerequisite reminder screen
- Linear step execution with ability to go back one step or quit
- Automated early steps (workspace setup, enabling watch, scaffold web project)
- Manual pause placeholders for later detailed editing steps (to avoid risky file mutations)

Run:

```pwsh
dotnet run --project .\src-tester\PhotoGalleryScriptRunner\PhotoGalleryScriptRunner.csproj
```

Notes:
- Commands are executed via PowerShell `pwsh -NoProfile`.
- Later steps are manual; you can extend the runner to automate file edits if desired.
- The runner intentionally avoids referencing existing repository `assets` or `src` folders per requirements.
