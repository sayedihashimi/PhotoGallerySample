# PhotoGallery Test Runner

Interactive console app (net10.0) using Spectre.Console to guide and automate the PhotoGallery end-to-end script.

## Usage

From repository root:

```
dotnet run --project .\src-tester\PhotoGallery.TestRunner.csproj
```

Follow on-screen prompts. Automated steps create files and execute commands via PowerShell (`pwsh -NoProfile`).

Currently automated through original script step 9; remaining steps still manual (placeholder). Extend `Program.cs` to add more discrete steps.

## Notes

Per instructions this project is standalone and does not reference existing `assets` or `src` content.
