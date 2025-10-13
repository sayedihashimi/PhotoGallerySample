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

        steps.Add(new Step(n++, "Select or create working folder", async ctx =>
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

        steps.Add(new Step(n++, "Create Directory.Build.props enabling HotReloadAutoRestart", async ctx =>
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

        steps.Add(new Step(n++, "Enable default watch via Aspire config", async ctx =>
        {
            EnsureWorking(ctx);
            await RunPwsh("aspire config set features.defaultWatchEnabled true -g", ctx.WorkingDirectory!);
        }));

        steps.Add(new Step(n++, "Run 'aspire new' interactively to scaffold solution", async ctx =>
        {
            EnsureWorking(ctx);
            AnsiConsole.MarkupLine("Launching interactive 'aspire new' (choose template: AppHost and service defaults; Name: PhotoGallery; Path: .\\ ; Template version: daily). Complete prompts then return here.");
            await RunInteractiveProcess("pwsh", "-NoProfile -Command \"aspire new\"", ctx.WorkingDirectory!);
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

        // From here on we mark most steps manual placeholders (automation possible but omitted for brevity & safety editing user files)
        string[] manualTitles = new[]
        {
            "Run 'dotnet watch --verbose --non-interactive' or use F5 in VS (Step 8)",
            "Verify dashboard shows 'No Resources Found' (Step 9)",
            "Add project reference web->AppHost (Step 10)",
            "Modify AppHost.cs add builder.AddProject (Step 11)",
            "Verify dashboard shows webapp running (Step 12)",
            "Add Aspire.Hosting.Azure.Storage package (Step 13)",
            "Add storage resource code to AppHost.cs (Step 14)",
            "Add RazorComponents service to PG.Web Program.cs (Step 15)",
            "Create Components folder (Step 16)",
            "Add initial PhotoList.razor (Step 17)",
            "Update app.MapGet to return RazorComponentResult (Step 18)",
            "Update PhotoList.razor with full html layout (Step 19)",
            "Page title should be 'Photo List' (Step 20)",
            "Dashboard error check (Step 21)",
            "Add Aspire.Azure.Storage.Blobs package to web (Step 22)",
            "Update AppHost builder.AddProject with references (Step 23)",
            "Add AddAzureBlobContainerClient to web Program (Step 24)",
            "Ensure using Azure.Storage.Blobs (Step 25)",
            "Update MapGet to enumerate blobs (Step 26)",
            "Replace PhotoList.razor with upload form (Step 27)",
            "Add project reference to ServiceDefaults (Step 28)",
            "Add builder.AddServiceDefaults (Step 29)",
            "Add app.MapDefaultEndpoints (Step 30)",
            "Add upload endpoint MapPost /upload (Step 31)",
            "Verify traces show webapp (Step 32)",
            "Observe antiforgery errors (Step 33)",
            "Add AddAntiforgery service (Step 34)",
            "Add UseAntiforgery middleware (Step 35)",
            "Add @using for Antiforgery in razor (Step 36)",
            "Add <AntiforgeryToken /> to form (Step 37)",
            "Upload and verify listing (Step 38)",
            "Add wwwroot folder (Step 39)",
            "Add theme.css file (Step 40)",
            "Add UseStaticFiles (Step 41)",
            "Link theme.css in head (Step 42)",
            "Replace PhotoList.razor with final UI (Step 43)",
            "Replace theme.css with dark theme (Step 44)",
            "Final verification (Step 45)"
        };
        foreach (var title in manualTitles)
        {
            steps.Add(new Step(n++, title, ctx => ManualPause()));
        }

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

    private static void RenderStepHeader(Step step)
    {
        var table = new Table().Centered();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn($"Step {step.Number}").Centered());
        table.AddRow("[bold]" + Escape(step.Title) + "[/]");
        AnsiConsole.Write(table);
    }

    private static string Escape(string value) => Markup.Escape(value ?? string.Empty);
}

internal sealed class ScriptContext
{
    public string? WorkingDirectory { get; set; }
}

internal sealed record Step(int Number, string Title, Func<ScriptContext, Task> Action);
