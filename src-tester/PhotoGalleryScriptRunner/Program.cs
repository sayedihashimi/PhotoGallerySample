using Spectre.Console;
using System.Diagnostics;
using System.Text;

namespace PhotoGalleryScriptRunner;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        AnsiConsole.Write(new FigletText("PhotoGallery E2E").Color(Color.CadetBlue));
        AnsiConsole.MarkupLine("[grey]Interactive script runner for PhotoGallery Hot Reload demo.[/]\n");

        var ctx = new ScriptContext();
        var steps = BuildSteps();

        ShowPrereqs();
        AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Press [green]Enter[/] to begin steps")
            .AddChoices("Start"));

        int index = 0;
        while (index < steps.Count)
        {
            var step = steps[index];
            RenderStepHeader(step);

            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"Step {step.Number}: {step.Title}")
                .MoreChoicesText("Use arrows to select")
                .AddChoices("Execute step", "Go back", "Quit"));

            if (choice.StartsWith("Quit"))
            {
                if (AnsiConsole.Confirm("Are you sure you want to quit? Progress is linear; on next run you'll need to re-execute prior steps manually.", false))
                    return 0;
                continue;
            }
            if (choice.StartsWith("Go back"))
            {
                if (index > 0) index--;
                continue;
            }

            try
            {
                await step.Action(ctx);
                index++;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Error executing step {step.Number}: {Escape(ex.Message)}[/]");
                if (!AnsiConsole.Confirm("Continue to next step anyway?", false))
                {
                    AnsiConsole.MarkupLine("Exiting.");
                    return 1;
                }
                index++;
            }
        }

        AnsiConsole.MarkupLine("[green]All scripted steps completed (some may have required manual actions).[/]");
        return 0;
    }

    private static void ShowPrereqs()
    {
        var prereqPanel = new Panel(new Markup("1. Install latest .NET SDK (see: https://github.com/dotnet/dotnet)\n" +
                                             "2. Install latest daily Aspire (see: https://github.com/dotnet/aspire)\n\n" +
                                             "After installing, return here and choose 'Start'."))
        {
            Header = new PanelHeader("Prerequisites", Justify.Center),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1,1,1,1)
        };
        AnsiConsole.Write(prereqPanel);
    }

    private static List<Step> BuildSteps()
    {
        var steps = new List<Step>();
        int n = 1;

        steps.Add(new Step(n++, "Select or create working folder (Step 1)", async ctx =>
        {
            var path = AnsiConsole.Ask<string>("Enter absolute path of empty (or new) folder to use:");
            path = Environment.ExpandEnvironmentVariables(path.Trim());
            if (!Path.IsPathRooted(path))
            {
                throw new InvalidOperationException("Path must be absolute.");
            }
            Directory.CreateDirectory(path);
            ctx.WorkingDirectory = path;
            AnsiConsole.MarkupLineInterpolated($"Using working directory: [cyan]{Escape(path)}[/]");
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Create Directory.Build.props enabling HotReloadAutoRestart (Step 2)", async ctx =>
        {
            EnsureWorking(ctx);
            var file = Path.Combine(ctx.WorkingDirectory!, "Directory.Build.props");
            if (!File.Exists(file))
            {
                File.WriteAllText(file, """
<Project>
  <PropertyGroup>
    <HotReloadAutoRestart>true</HotReloadAutoRestart>
  </PropertyGroup>
</Project>
""".Trim() + Environment.NewLine, Encoding.UTF8);
                AnsiConsole.MarkupLine("Created Directory.Build.props");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Directory.Build.props already exists; skipping.[/]");
            }
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Enable default watch via Aspire config (Step 3)", async ctx =>
        {
            EnsureWorking(ctx);
            await RunPwsh("aspire config set features.defaultWatchEnabled true -g", ctx.WorkingDirectory!);
        }));

        steps.Add(new Step(n++, "Run 'aspire new' interactively to scaffold solution (Step 4)", async ctx =>
        {
            EnsureWorking(ctx);
            AnsiConsole.MarkupLine("Launching interactive 'aspire new' (choose template: AppHost and service defaults; Name: PhotoGallery; Path: .\\ ; Template version: daily). Complete prompts then return here.");
            await RunInteractiveProcess("pwsh", "-NoProfile -Command \"aspire new -n PhotoGallery -o ./ \"", ctx.WorkingDirectory!);
            AnsiConsole.MarkupLine("If generation succeeded you should now have a solution (e.g. PhotoGallery.sln).");
        }));

        steps.Add(new Step(n++, "(Manual) If using VS, open the solution (Step 5)", ctx => ManualPause()));
        steps.Add(new Step(n++, "Create Razor Pages web app (Step 6)", async ctx =>
        {
            EnsureWorking(ctx);
            var cmd = "dotnet new web -o PhotoGallery.Web -f net9.0"; // per script requirement
            await RunPwsh(cmd, ctx.WorkingDirectory!);
        }));
        steps.Add(new Step(n++, "Add web project to solution (Step 7, skip if using VS)", async ctx =>
        {
            EnsureWorking(ctx);
            var sln = Directory.GetFiles(ctx.WorkingDirectory!, "*.sln").FirstOrDefault();
            if (sln is null)
            {
                AnsiConsole.MarkupLine("[yellow]No solution file found; skipping add project.[/]");
                return;
            }
            await RunPwsh($"dotnet sln \"{Path.GetFileName(sln)}\" add .\\PhotoGallery.Web\\PhotoGallery.Web.csproj", ctx.WorkingDirectory!);
        }));
        // Steps 8 & 9 remain manual (watch run + dashboard initial state)
        steps.Add(new Step(n++, "Run 'dotnet watch --verbose --non-interactive' or use F5 in VS (Step 8)", ctx => ManualPause()));
        steps.Add(new Step(n++, "Verify dashboard shows 'No Resources Found' (Step 9)", ctx => ManualPause()));

        // Automation from Step 10 onwards
        steps.Add(new Step(n++, "Add project reference AppHost -> Web (Step 10)", async ctx =>
        {
            EnsureWorking(ctx);
            var (appHostDir, webDir) = ResolveProjectDirs(ctx);
            if (appHostDir is null || webDir is null)
            {
                AnsiConsole.MarkupLine("[yellow]Could not locate AppHost or Web project; skipping.[/]");
                return;
            }
            var appHostProj = Directory.GetFiles(appHostDir, "*.csproj").First();
            var webProj = Directory.GetFiles(webDir, "*.csproj").First();
            // Add reference only if not already present
            if (!File.ReadAllText(appHostProj).Contains(Path.GetFileName(webProj), StringComparison.OrdinalIgnoreCase))
            {
                await RunPwsh($"dotnet add \"{appHostProj}\" reference \"{webProj}\"", ctx.WorkingDirectory!);
            }
            else
            {
                AnsiConsole.MarkupLine("[grey]Reference already exists.[/]");
            }
        }));

        steps.Add(new Step(n++, "Modify AppHost.cs add builder.AddProject (Step 11)", async ctx =>
        {
            EnsureWorking(ctx);
            var (appHostDir, _) = ResolveProjectDirs(ctx);
            if (appHostDir is null) { AnsiConsole.MarkupLine("[yellow]AppHost not found.[/]"); return; }
            var appHostCs = Path.Combine(appHostDir, "AppHost.cs");
            if (!File.Exists(appHostCs)) { AnsiConsole.MarkupLine("[yellow]AppHost.cs missing.[/]"); return; }
            var txt = File.ReadAllText(appHostCs);
            if (!txt.Contains("PhotoGallery_Web", StringComparison.Ordinal))
            {
                txt = InsertAfterLineContaining(txt, "var builder", "builder.AddProject<Projects.PhotoGallery_Web>(\"webapp\");");
                File.WriteAllText(appHostCs, txt);
                AnsiConsole.MarkupLine("Inserted builder.AddProject line.");
            }
            else AnsiConsole.MarkupLine("[grey]builder.AddProject already present.[/]");
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Verify dashboard shows webapp running (Step 12)", ctx => ManualPause()));

        steps.Add(new Step(n++, "Add Aspire.Hosting.Azure.Storage package (Step 13)", async ctx =>
        {
            EnsureWorking(ctx);
            var (appHostDir, _) = ResolveProjectDirs(ctx);
            if (appHostDir is null) return;
            var appHostProj = Directory.GetFiles(appHostDir, "*.csproj").First();
            // Determine version of Aspire.Hosting.AppHost to match
            string? version = ExtractPackageVersion(appHostProj, "Aspire.Hosting.AppHost");
            var pkgCmd = version is null
                ? "dotnet add package Aspire.Hosting.Azure.Storage --prerelease"
                : $"dotnet add package Aspire.Hosting.Azure.Storage --version {version}";
            if (!File.ReadAllText(appHostProj).Contains("Aspire.Hosting.Azure.Storage", StringComparison.Ordinal))
            {
                await RunPwsh(pkgCmd + " --project \"" + appHostProj + "\"", ctx.WorkingDirectory!);
            }
            else AnsiConsole.MarkupLine("[grey]Package already referenced.[/]");
        }));

        steps.Add(new Step(n++, "Add storage resource code to AppHost.cs (Step 14)", async ctx =>
        {
            EnsureWorking(ctx);
            var (appHostDir, _) = ResolveProjectDirs(ctx);
            if (appHostDir is null) return;
            var appHostCs = Path.Combine(appHostDir, "AppHost.cs");
            if (!File.Exists(appHostCs)) return;
            var txt = File.ReadAllText(appHostCs);
            if (!txt.Contains("AddAzureStorage", StringComparison.Ordinal))
            {
                var snippet = "var photos = builder.AddAzureStorage(\"storage\")\n                        .RunAsEmulator()\n                        .AddBlobs(\"blobs\")\n                        .AddBlobContainer(\"photos\");";
                txt = InsertAfterLineContaining(txt, "var builder", snippet);
                File.WriteAllText(appHostCs, txt);
                AnsiConsole.MarkupLine("Inserted storage resource code.");
            }
            else AnsiConsole.MarkupLine("[grey]Storage resource code already present.[/]");
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Add RazorComponents service to Web Program.cs (Step 15)", ctx => ModifyWebProgram(ctx, content =>
        {
            if (!content.Contains("AddRazorComponents", StringComparison.Ordinal))
                content = InsertAfterLineContaining(content, "var builder", "builder.Services.AddRazorComponents();");
            return content;
        })));

        steps.Add(new Step(n++, "Create Components folder (Step 16)", async ctx =>
        {
            EnsureWorking(ctx);
            var (_, webDir) = ResolveProjectDirs(ctx);
            if (webDir is null) return;
            Directory.CreateDirectory(Path.Combine(webDir, "Components"));
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Add initial PhotoList.razor (Step 17)", async ctx =>
        {
            EnsureWorking(ctx);
            var (_, webDir) = ResolveProjectDirs(ctx);
            if (webDir is null) return;
            var file = Path.Combine(webDir, "Components", "PhotoList.razor");
            if (!File.Exists(file))
            {
                File.WriteAllText(file, """
@code
{
    [Parameter]
    public IEnumerable<string> Photos{get;set;} = [];
}

<ul>
@foreach(var photo in Photos)
{
    <li>@photo</li>
}
</ul>
""".Trim());
            }
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Update MapGet to return RazorComponentResult (Step 18)", ctx => ModifyWebProgram(ctx, content =>
        {
            if (!content.Contains("RazorComponentResult<PhotoList>", StringComparison.Ordinal))
            {
                content = EnsureUsing(content, "using PhotoGallery.Web.Components;");
                content = EnsureUsing(content, "using Microsoft.AspNetCore.Http.HttpResults;");
                content = ReplaceMapGet(content, "app.MapGet(\"/\", () => \n    {\n        return new RazorComponentResult<PhotoList>(new {Photos = Array.Empty<string>() } );\n    });");
            }
            return content;
        })));

        steps.Add(new Step(n++, "Update PhotoList.razor with full html layout (Step 19)", async ctx =>
        {
            EnsureWorking(ctx);
            var file = ResolveWebComponent(ctx, "PhotoList.razor");
            if (file is null) return;
            var txt = File.ReadAllText(file);
            if (!txt.Contains("<html>", StringComparison.Ordinal))
            {
                File.WriteAllText(file, """
@code
{
    [Parameter]
    public IEnumerable<string> Photos{get;set;} = [];
}

<html>
    <head>
        <title>Photo List</title>
    </head>
    <body>
        <script src="/_framework/aspnetcore-browser-refresh.js"></script>
        <ul>
        @foreach(var photo in Photos)
        {
            <li>@photo</li>
        }
        </ul>
    </body>
</html>
""".Trim());
            }
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Page title should be 'Photo List' (Step 20)", ctx => ManualPause()));
        steps.Add(new Step(n++, "Dashboard error check (Step 21)", ctx => ManualPause()));

        steps.Add(new Step(n++, "Add Aspire.Azure.Storage.Blobs package to web (Step 22)", async ctx =>
        {
            EnsureWorking(ctx);
            var (_, webDir) = ResolveProjectDirs(ctx);
            var (appHostDir, _) = ResolveProjectDirs(ctx);
            if (webDir is null) return;
            var webProj = Directory.GetFiles(webDir, "*.csproj").First();
            string? version = null;
            if (appHostDir is not null)
            {
                var appHostProj = Directory.GetFiles(appHostDir, "*.csproj").First();
                version = ExtractPackageVersion(appHostProj, "Aspire.Hosting.Azure.Storage");
            }
            if (!File.ReadAllText(webProj).Contains("Aspire.Azure.Storage.Blobs", StringComparison.Ordinal))
            {
                var cmd = version is null ? "dotnet add package Aspire.Azure.Storage.Blobs --prerelease" : $"dotnet add package Aspire.Azure.Storage.Blobs --version {version}";
                await RunPwsh(cmd + " --project \"" + webProj + "\"", ctx.WorkingDirectory!);
            }
        }));

        steps.Add(new Step(n++, "Update AppHost builder.AddProject with references (Step 23)", async ctx =>
        {
            EnsureWorking(ctx);
            var (appHostDir, _) = ResolveProjectDirs(ctx);
            if (appHostDir is null) return;
            var appHostCs = Path.Combine(appHostDir, "AppHost.cs");
            if (!File.Exists(appHostCs)) return;
            var txt = File.ReadAllText(appHostCs);
            if (txt.Contains("builder.AddProject<Projects.PhotoGallery_Web>(\"webapp\");") && txt.Contains("photos"))
            {
                txt = txt.Replace("builder.AddProject<Projects.PhotoGallery_Web>(\"webapp\");", "builder.AddProject<Projects.PhotoGallery_Web>(\"webapp\")\n            .WithReference(photos)\n            .WaitFor(photos);");
                File.WriteAllText(appHostCs, txt);
                AnsiConsole.MarkupLine("Updated builder.AddProject with references.");
            }
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Add AddAzureBlobContainerClient to web Program (Step 24)", ctx => ModifyWebProgram(ctx, content =>
        {
            if (!content.Contains("AddAzureBlobContainerClient", StringComparison.Ordinal))
                content = InsertAfterLineContaining(content, "var builder", "builder.AddAzureBlobContainerClient(\"photos\");");
            return content;
        })));

        steps.Add(new Step(n++, "Ensure using Azure.Storage.Blobs (Step 25)", ctx => ModifyWebProgram(ctx, content => EnsureUsing(content, "using Azure.Storage.Blobs;"))));

        steps.Add(new Step(n++, "Update MapGet to enumerate blobs (Step 26)", ctx => ModifyWebProgram(ctx, content =>
        {
            if (!content.Contains("BlobContainerClient", StringComparison.Ordinal) || !content.Contains("GetBlobsAsync", StringComparison.Ordinal))
            {
                content = EnsureUsing(content, "using PhotoGallery.Web.Components;");
                content = EnsureUsing(content, "using Microsoft.AspNetCore.Http.HttpResults;");
                var map = "app.MapGet(\"/\", async (BlobContainerClient client) =>\n    {\n        var blobs = client.GetBlobsAsync();\n        var photos = new List<string>();\n        await foreach(var photo in blobs)\n        {\n            photos.Add(photo.Name);\n        }\n        return new RazorComponentResult<PhotoList>(new {Photos = photos } );\n";
                content = ReplaceMapGet(content, map);
            }
            return content;
        })));

        steps.Add(new Step(n++, "Replace PhotoList.razor with upload form (Step 27)", async ctx =>
        {
            var file = ResolveWebComponent(ctx, "PhotoList.razor");
            if (file is null) return;
            var txt = File.ReadAllText(file);
            if (!txt.Contains("Upload Photo", StringComparison.Ordinal))
            {
                File.WriteAllText(file, """
@code
{
    [Parameter]
    public IEnumerable<string> Photos { get; set; } = [];
}

<html>
<head>
    <title>Photo List</title>
</head>
<body>
    <script src="/_framework/aspnetcore-browser-refresh.js"></script>
    <h2>Photo List</h2>
    <div>
        <form action="/upload" method="post" enctype="multipart/form-data">
            <div>
                <label for="photo">Choose photo:</label>
                <input type="file" id="photo" name="photo" accept="image/*" required > 
            </div>
            <div>
                <button type="submit">Upload Photo</button>
            </div>
        </form>
    </div>

    <ul>
        @foreach (var photo in Photos)
        {
            <li>@photo</li>
        }
    </ul>
</body>
</html>
""".Trim());
            }
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Add project reference Web -> ServiceDefaults (Step 28)", async ctx =>
        {
            EnsureWorking(ctx);
            var (appHostDir, webDir) = ResolveProjectDirs(ctx);
            if (webDir is null || appHostDir is null) return;
            // ServiceDefaults project at sibling folder maybe PhotoGallery.ServiceDefaults
            var root = ctx.WorkingDirectory!;
            var svcDir = Directory.GetDirectories(root, "*.ServiceDefaults", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (svcDir is null) return;
            var webProj = Directory.GetFiles(webDir, "*.csproj").First();
            var svcProj = Directory.GetFiles(svcDir, "*.csproj").First();
            if (!File.ReadAllText(webProj).Contains(Path.GetFileName(svcProj), StringComparison.OrdinalIgnoreCase))
            {
                await RunPwsh($"dotnet add \"{webProj}\" reference \"{svcProj}\"", ctx.WorkingDirectory!);
            }
        }));

        steps.Add(new Step(n++, "Add builder.AddServiceDefaults (Step 29)", ctx => ModifyWebProgram(ctx, content =>
        {
            if (!content.Contains("AddServiceDefaults()", StringComparison.Ordinal))
                content = InsertAfterLineContaining(content, "var builder", "builder.AddServiceDefaults();");
            return content;
        })));

        steps.Add(new Step(n++, "Add app.MapDefaultEndpoints (Step 30)", ctx => ModifyWebProgram(ctx, content =>
        {
            if (!content.Contains("MapDefaultEndpoints", StringComparison.Ordinal))
                content = InsertAfterLineContaining(content, "var app = builder.Build()", "app.MapDefaultEndpoints();");
            return content;
        })));

        steps.Add(new Step(n++, "Add upload endpoint MapPost /upload before app.Run() (Step 31)", ctx => ModifyWebProgram(ctx, content =>
        {
            if (!content.Contains("/upload", StringComparison.Ordinal))
            {
                var upload = "app.MapPost(\"/upload\", async (IFormFile photo, BlobContainerClient client) =>\n{\n    if (photo.Length > 0)\n    {\n        var blobClient = client.GetBlobClient(photo.FileName);\n        await blobClient.UploadAsync(photo.OpenReadStream(), true);\n    }\n    return Results.Redirect(\"/\");\n});";
                content = EnsureUsing(content, "using Azure.Storage.Blobs;");
                content = EnsureUsing(content, "using Microsoft.AspNetCore.Http;" );
                if (content.Contains("app.Run()", StringComparison.Ordinal))
                {
                    content = InsertBeforeLineContaining(content, "app.Run()", upload);
                }
                else
                {
                    // Fallback to after MapGet if app.Run() not yet present
                    content = InsertBeforeLineContaining(content, "app.MapGet", upload);
                }
            }
            return content;
        })));

        steps.Add(new Step(n++, "Verify traces show webapp (Step 32)", ctx => ManualPause()));
        steps.Add(new Step(n++, "Observe antiforgery errors (Step 33)", ctx => ManualPause()));

        steps.Add(new Step(n++, "Add AddAntiforgery service (Step 34)", ctx => ModifyWebProgram(ctx, content =>
        {
            if (!content.Contains("AddAntiforgery", StringComparison.Ordinal))
                content = InsertBeforeLineContaining(content, "var app = builder.Build()", "builder.Services.AddAntiforgery();");
            return content;
        })));

        steps.Add(new Step(n++, "Add UseAntiforgery middleware (Step 35)", ctx => ModifyWebProgram(ctx, content =>
        {
            if (!content.Contains("UseAntiforgery()", StringComparison.Ordinal))
                content = InsertAfterLineContaining(content, "var app = builder.Build()", "app.UseAntiforgery();");
            return content;
        })));

        steps.Add(new Step(n++, "Add @using for Antiforgery in razor (Step 36)", async ctx =>
        {
            var file = ResolveWebComponent(ctx, "PhotoList.razor");
            if (file is null) return;
            var txt = File.ReadAllText(file);
            if (!txt.Contains("@using Microsoft.AspNetCore.Components.Forms", StringComparison.Ordinal))
            {
                txt = "@using Microsoft.AspNetCore.Components.Forms\n" + txt;
                File.WriteAllText(file, txt);
            }
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Add <AntiforgeryToken /> after form line (Step 37)", async ctx =>
        {
            var file = ResolveWebComponent(ctx, "PhotoList.razor");
            if (file is null) return;
            var lines = File.ReadAllLines(file).ToList();
            if (lines.Any(l => l.Contains("AntiforgeryToken", StringComparison.Ordinal))) { await Task.CompletedTask; return; }
            int formIndex = lines.FindIndex(l => l.Contains("<form", StringComparison.Ordinal));
            if (formIndex >= 0)
            {
                // Determine indentation from next line or form line
                string indent = new string(lines[formIndex].TakeWhile(char.IsWhiteSpace).ToArray()) + "    ";
                lines.Insert(formIndex + 1, indent + "<AntiforgeryToken />");
                File.WriteAllLines(file, lines);
                AnsiConsole.MarkupLine("[grey]Inserted AntiforgeryToken after form line.[/]");
            }
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Upload and verify listing (Step 38)", ctx => ManualPause()));

        steps.Add(new Step(n++, "Add wwwroot folder (Step 39)", async ctx =>
        {
            var (_, webDir) = ResolveProjectDirs(ctx);
            if (webDir is null) return;
            Directory.CreateDirectory(Path.Combine(webDir, "wwwroot"));
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Add theme.css file (Step 40)", async ctx =>
        {
            var (_, webDir) = ResolveProjectDirs(ctx);
            if (webDir is null) return;
            var file = Path.Combine(webDir, "wwwroot", "theme.css");
            if (!File.Exists(file))
            {
                File.WriteAllText(file, "body {\n    background-color: gray;\n}\n");
            }
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Add UseStaticFiles (Step 41)", ctx => ModifyWebProgram(ctx, content =>
        {
            if (!content.Contains("UseStaticFiles()", StringComparison.Ordinal))
                content = InsertAfterLineContaining(content, "var app = builder.Build()", "app.UseStaticFiles();");
            return content;
        })));

        steps.Add(new Step(n++, "Link theme.css in head (Step 42)", async ctx =>
        {
            var file = ResolveWebComponent(ctx, "PhotoList.razor");
            if (file is null) return;
            var txt = File.ReadAllText(file);
            if (txt.Contains("<head>") && !txt.Contains("theme.css", StringComparison.Ordinal))
            {
                txt = txt.Replace("<head>", "<head>\n        <link rel=\"stylesheet\" href=\"/theme.css\"/>");
                File.WriteAllText(file, txt);
            }
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Replace PhotoList.razor with final UI (Step 43)", async ctx =>
        {
            var file = ResolveWebComponent(ctx, "PhotoList.razor");
            if (file is null) return;
            var finalMarker = "photo-list-root";
            if (!File.ReadAllText(file).Contains(finalMarker, StringComparison.Ordinal))
            {
                File.WriteAllText(file, FinalPhotoListContent);
            }
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Replace theme.css with dark theme (Step 44)", async ctx =>
        {
            var (_, webDir) = ResolveProjectDirs(ctx);
            if (webDir is null) return;
            var file = Path.Combine(webDir, "wwwroot", "theme.css");
            if (File.Exists(file))
            {
                if (!File.ReadAllText(file).Contains("--gallery-gap", StringComparison.Ordinal))
                {
                    File.WriteAllText(file, DarkThemeCssSnippet);
                }
            }
            await Task.CompletedTask;
        }));

        steps.Add(new Step(n++, "Final verification (Step 45)", ctx => ManualPause()));

        return steps;
    }

    private static Task ManualPause()
    {
        AnsiConsole.MarkupLine("[italic yellow]Manual action required. Perform the step externally, then continue.[/]");
        AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Continue when done").AddChoices("Proceed"));
        return Task.CompletedTask;
    }

    private static void EnsureWorking(ScriptContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.WorkingDirectory))
            throw new InvalidOperationException("Working directory not set. Execute Step 1 first.");
    }

    private static async Task RunPwsh(string command, string workingDir)
    {
        AnsiConsole.MarkupLineInterpolated($"[grey]Running:[/] [blue]{Escape(command)}[/]");
        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = $"-NoProfile -Command \"{command}\"",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var proc = Process.Start(psi)!;
        string stdout = await proc.StandardOutput.ReadToEndAsync();
        string stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        if (!string.IsNullOrWhiteSpace(stdout))
            AnsiConsole.WriteLine(stdout.TrimEnd());
        if (!string.IsNullOrWhiteSpace(stderr))
            AnsiConsole.MarkupLineInterpolated($"[red]{Escape(stderr.TrimEnd())}[/]");
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"Command failed with exit code {proc.ExitCode}");
    }

    private static async Task RunInteractiveProcess(string fileName, string arguments, string workingDir)
    {
        AnsiConsole.MarkupLineInterpolated($"[grey]Launching interactive process:[/] [blue]{Escape(fileName + " " + arguments)}[/]");
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            UseShellExecute = true // allow direct interaction
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process");
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
            AnsiConsole.MarkupLineInterpolated($"[yellow]Process exited with code {proc.ExitCode}. Continue if acceptable.[/]");
    }

    private static (string? appHostDir, string? webDir) ResolveProjectDirs(ScriptContext ctx)
    {
        if (ctx.WorkingDirectory is null) return (null, null);
        string? appHostDir = Directory.GetDirectories(ctx.WorkingDirectory, "*.AppHost", SearchOption.TopDirectoryOnly).FirstOrDefault();
        string? webDir = Directory.GetDirectories(ctx.WorkingDirectory, "*.Web", SearchOption.TopDirectoryOnly).FirstOrDefault();
        return (appHostDir, webDir);
    }

    private static string? ResolveWebComponent(ScriptContext ctx, string fileName)
    {
        var (_, webDir) = ResolveProjectDirs(ctx);
        if (webDir is null) return null;
        var compDir = Path.Combine(webDir, "Components");
        return Path.Combine(compDir, fileName);
    }

    private static string ReplaceMapGet(string content, string newMapGet)
    {
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("app.MapGet("))
            {
                // capture until semicolon line
                var j = i;
                while (j < lines.Length && !lines[j].Contains(";")) j++;
                if (j < lines.Length) j++;
                var before = lines.Take(i).ToList();
                var after = lines.Skip(j).ToList();
                before.AddRange(newMapGet.Split('\n'));
                before.AddRange(after);
                return string.Join('\n', before);
            }
        }
        // If not found, append
        return content + "\n" + newMapGet + "\n";
    }

    private static string InsertAfterLineContaining(string text, string marker, string insert, bool allowMultiple = false)
    {
        var lines = text.Split('\n').ToList();
        if (!allowMultiple && text.Contains(insert, StringComparison.Ordinal)) return text; // already there
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains(marker, StringComparison.Ordinal))
            {
                lines.Insert(i + 1, insert);
                break;
            }
        }
        return string.Join('\n', lines);
    }

    private static string InsertBeforeLineContaining(string text, string marker, string insert)
    {
        var lines = text.Split('\n').ToList();
        if (text.Contains(insert, StringComparison.Ordinal)) return text;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains(marker, StringComparison.Ordinal))
            {
                lines.Insert(i, insert);
                break;
            }
        }
        return string.Join('\n', lines);
    }

    private static string EnsureUsing(string content, string usingLine)
    {
        if (!content.Contains(usingLine, StringComparison.Ordinal))
        {
            content = usingLine + '\n' + content;
        }
        return content;
    }

    private static string ExtractPackageVersion(string csprojPath, string packageId)
    {
        try
        {
            var xml = File.ReadAllText(csprojPath);
            var tag = $"<PackageReference Include=\"{packageId}\"";
            var idx = xml.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null!;
            var verAttr = "Version=\"";
            var vIdx = xml.IndexOf(verAttr, idx, StringComparison.OrdinalIgnoreCase);
            if (vIdx < 0) return null!;
            vIdx += verAttr.Length;
            var end = xml.IndexOf('"', vIdx);
            if (end < 0) return null!;
            return xml.Substring(vIdx, end - vIdx);
        }
        catch { return null!; }
    }

    private static void RenderStepHeader(Step step)
    {
        var table = new Table().Centered();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn($"Step {step.Number}").Centered());
        table.AddRow("[bold]" + Escape(step.Title) + "[/]");
        AnsiConsole.Write(table);
    }

    private static Task ModifyWebProgram(ScriptContext ctx, Func<string, string> transform)
    {
        EnsureWorking(ctx);
        var (_, webDir) = ResolveProjectDirs(ctx);
        if (webDir is null)
            return Task.CompletedTask;
        var programFile = Path.Combine(webDir, "Program.cs");
        if (!File.Exists(programFile))
            return Task.CompletedTask;
        var content = File.ReadAllText(programFile);
        var updated = transform(content);
        if (!ReferenceEquals(content, updated) && content != updated)
        {
            File.WriteAllText(programFile, updated);
            AnsiConsole.MarkupLine("[grey]Updated Web Program.cs[/]");
        }
        return Task.CompletedTask;
    }

    private static readonly string FinalPhotoListContent = """
@using Microsoft.AspNetCore.Components.Forms
@code {
    [Parameter]
    public IEnumerable<string> Photos { get; set; } = [];

    private static string BuildPhotoUrl(string name) => $"/photos/{Uri.EscapeDataString(name)}";
}

<html>
<head>
    <title>Photo List</title>
    <link rel="stylesheet" href="/theme.css" />
</head>
<body>
    <section class="photo-list-root">
        <div class="content-container">
            <header class="upload-header">
                <h1 class="page-title">Photo Gallery</h1>
                <div class="upload-panel" aria-labelledby="upload-title">
                    <h2 id="upload-title" class="upload-title">Upload a Photo</h2>
                    <form class="upload-form" action="/upload" method="post" enctype="multipart/form-data" id="uploadForm">
                        <AntiforgeryToken />
                        <div class="field-group">
                            <label class="file-input-label" for="photo">Choose photo</label>
                            <input class="file-input" type="file" id="photo" name="photo" accept="image/*" required />
                        </div>
                        <button class="upload-button" type="submit" disabled id="uploadButton">Upload</button>
                    </form>
                </div>
            </header>
            <div class="section-separator" role="separator" aria-hidden="true"></div>
            <section class="gallery-section" aria-labelledby="gallery-heading">
                <div class="section-heading-row">
                    <h2 id="gallery-heading" class="section-heading">Your Photos</h2>
                    <span class="photo-count" aria-live="polite">@((Photos?.Count() ?? 0)) total</span>
                </div>
                @if (Photos is null || !Photos.Any())
                {
                    <div class="empty-state">
                        <p>No photos uploaded yet. Use the panel above to add one.</p>
                    </div>
                }
                else
                {
                    <ul class="gallery" aria-label="Photo gallery">
                        @foreach (var photo in Photos)
                        {
                            <li class="photo-card">
                                <div class="image-wrapper">
                                    <img src="@BuildPhotoUrl(photo)" alt="@photo" loading="lazy" decoding="async" />
                                    <form method="post" action="/photos/@Uri.EscapeDataString(photo)/delete" class="delete-form" onsubmit="return confirm('Delete this photo?');">
                                        <AntiforgeryToken />
                                        <button type="submit" class="delete-button" aria-label="Delete @photo" title="Delete @photo">✕</button>
                                    </form>
                                </div>
                                <div class="caption" title="@photo">@photo</div>
                            </li>
                        }
                    </ul>
                }
            </section>
        </div>
    </section>
    <script>
        (function() {
            const fileInput = document.getElementById('photo');
            const uploadBtn = document.getElementById('uploadButton');
            if (fileInput && uploadBtn) {
                fileInput.addEventListener('change', () => {
                    uploadBtn.disabled = !fileInput.files || fileInput.files.length === 0;
                });
            }
        })();
    </script>
</body>
</html>
""";

    private static readonly string DarkThemeCssSnippet = """
/* Global dark theme tokens */
:root {
--gallery-gap: clamp(.75rem, 1.6vw, 1.25rem);
--card-radius: 14px;
--color-bg: hsl(222 28% 9%);
--color-bg-accent: linear-gradient(145deg, hsl(222 30% 14%), hsl(222 32% 10%));
--card-bg: hsl(222 25% 15%);
--card-bg-alt: hsl(222 24% 18%);
--card-border: hsl(220 18% 65% / 0.16);
--card-border-strong: hsl(220 40% 70% / 0.35);
--shadow-sm: 0 2px 4px -2px hsl(0 0% 0% / .55);
--shadow-lg: 0 10px 32px -10px hsl(222 70% 20% / .45);
--focus-ring: 0 0 0 3px hsl(210 90% 60% / .55), 0 0 0 6px hsl(210 90% 60% / .2);
--upload-accent: hsl(215 90% 55%);
--upload-accent-hover: hsl(215 92% 50%);
--upload-accent-active: hsl(215 95% 46%);
--text-strong: hsl(220 33% 94%);
--text-dim: hsl(220 18% 70%);
--danger: hsl(0 78% 55%);
--danger-hover: hsl(0 80% 60%);
--danger-active: hsl(0 85% 52%);
--danger-ring: 0 0 0 3px hsl(0 85% 55% / .4), 0 0 0 6px hsl(0 85% 55% / .18);
--empty-fg: hsl(220 14% 60%);
}
body { background: var(--color-bg); color: var(--text-strong); }
ul.gallery { list-style: none !important; margin:0; padding:0; }
ul.gallery > li { list-style:none !important; }
""";

    private static string Escape(string value) => Spectre.Console.Markup.Escape(value ?? string.Empty);
}

internal sealed class ScriptContext
{
    public string? WorkingDirectory { get; set; }
}

internal sealed record Step(int Number, string Title, Func<ScriptContext, Task> Action);

// Large content constants (final UI & dark theme)
internal static class Snippets
{
}
