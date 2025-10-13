---
mode: 'agent'
model: GPT-5
description: 'Generate a new console app that will execute test script'
---

Your goal is to generate a new .NET 10 Console app which can be used to simplify testing `dotnet watch` and Hot Reload.
Below in `Script instructions` there is a set of manual instructions that a tester will follow to run an end-to-end test.
You will create a .NET Core console app that uses Spectre.Console (https://spectreconsole.net/). This console app will be
interactive. For each step below you'll:

1. Show the text of the step
2. Show this list of commands 
   1. Execute step (default)
   2. Go back
   3. Quit

If the user selects `Execute step` you'll perform that step. If there are any manual steps, make it clear that the user needs to take
a specfic action. When the step is executed, you'll proceed to the next step.
The steps are designed to be run from start to finish.
You don't need to create any interface to let users pick certain steps to execute.

When the app first starts, show the info in Prereqs and wait for the user to perform those steps.

The goal is to automate as much of this as possible.

The code that you generate should go in the folder `src-tester`. Any project you create, should be
in a subfolder.

When you think you are done generating code the code **MUST** build successfully. If there are build
errors, edit the code until there are no build errors.

If you run powershell, pwsh, you should always pass in `-NoProfile`.

Do **NOT** reference any of the files in these folders
- `assets`
- `src`
You **MUST** ignore those files.

### Script instructions

# PhotoGallary Setup

## Prereqs
1. Install the [latest dotnet](https://github.com/dotnet/dotnet/blob/main/docs/builds-table.md)
1. Install the [latest daily aspire ](https://github.com/dotnet/aspire/blob/main/docs/using-latest-daily.md)

## Demo Steps
To reproduce the PhotoGallery using the dotnet CLI, all steps are below. When running a command, run it from the new directory that you create in step 1.

To reproduce the PhotoGallery in VS, follow the steps through step 4. Then start with step 5 in VS.

1. Create, or open, an empty folder.
   1. Copilot: For this step, prompt the user to provide the path to the folder to use.
2. Create a `Directory.Build.props` with the content below.
    ```xml
    <Project>
      <PropertyGroup>
        <HotReloadAutoRestart>true</HotReloadAutoRestart>
      </PropertyGroup>
    </Project>
   ```
3. Run command below. This will enable `dotnet watch` to be on by default in non interactive mode. 
   ```bash
   aspire config set features.defaultWatchEnabled true -g
   ```
4. Create Aspire projects.
    ```bash
    aspire new
    ```
   - Template:  `AppHost and service defaults`
   - Name: `PhotoGallery`
   - Path: `.\`
   - Template version: `daily`
   - Copilot: For this step call `aspire new aspire`. When you invoke it, invoke it in a way
            that the user can interact with it and complete the interaction.
5. If you are using VS, open the solution file that was created by `aspire run`
6. Use the command below to create the Razor Pages web app. In VS select `ASP.NET Core Empty (9.0)` as the project template. When using in VS make sure to check "Enlist in Aspire Orchestration".
    ```bash
    dotnet new web -o PhotoGallery.Web -f net9.0
    ```
7. If using the CLI add the project to the solution with the command below. Skip this step if using VS.
   ```
   dotnet sln .\PhotoGallery.sln add .\PhotoGallery.Web\PhotoGallery.Web.csproj
   ```
8. Run `dotnet watch --verbose --non-interactive` or `F5`/`CTRL-F5` in Visual Studio.
9.  Dashboard should show "No Resources Found"
10. AppHost: `Add Project Reference` to `PhotoGallery.Web`
    ```bash
    dotnet add reference --project .\PhotoGallery.AppHost\PhotoGallery.AppHost.csproj .\PhotoGallery.Web\PhotoGallery.Web.csproj
    ```
    Copilot: run the command in the snippet above for this step.
11. `AppHost.cs` add after `var builder = …`
    ```cs
    builder.AddProject<Projects.PhotoGallery_Web>("webapp");
    ```
12. Dashboard should show "webapp" and it should get to running state.
13. AppHost: Add NuGet pkg reference to `Aspire.Hosting.Azure.Storage`
    ```bash
     dotnet add package --project .\PhotoGallery.AppHost\PhotoGallery.AppHost.csproj Aspire.Hosting.Azure.Storage --prerelease
    ```
    - Adjust the version number as needed
    - Note: version must match the version of `Aspire.Hosting.AppHost`
14. AppHost.cs – add code directly below `var builder = …`
    ```cs
    var photos = builder.AddAzureStorage("storage")
                        .RunAsEmulator()
                        .AddBlobs("blobs")
                        .AddBlobContainer("photos");
    ```
15. `PG.Web.Program.cs` – add after the first line (`var builder = …`)
    ```cs
    builder.Services.AddRazorComponents();
    ```
16. PG.Web: Add `Components` folder
17. PG.Web: Add new file `Components\PhotoList.razor` with the contents below.
    ```
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
    ```
18. `PG.Web.Program.cs` update app.MapGet to be the following
    ```
    app.MapGet("/", () => 
    {
        return new RazorComponentResult<PhotoList>(new {Photos = Array.Empty<string>() } );
    });
    ```
    Note: if you paste this code in VS it should add the following using statements.
    ```
    using PhotoGallery.Web.Components;
    using Microsoft.AspNetCore.Http.HttpResults;
    ```    
19. `PhotoList.razor` update with the following
    ```
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
    ```
20. The title of the web page should be "Photo List"
21. View dashboard there shouldn’t be any errors
22. `PG.Web` Add NuGet Pkg ref to `Aspire.Azure.Storage.Blobs`
    ```bash
    dotnet add package --project .\PhotoGallery.Web\PhotoGallery.Web.csproj Aspire.Azure.Storage.Blobs --prerelease
    ```
    - Adjust the version number as needed
    - Version must match the version of `Aspire.Hosting.Azure.Storage` in AppHost project.
23. `AppHost.cs` – replace `builder.AddProject<Projects.PhotoGallery_Web>("webapp");` with
    ```cs
    builder.AddProject<Projects.PhotoGallery_Web>("webapp")
            .WithReference(photos)
            .WaitFor(photos);
    ```
24. `PG.Web.Program.cs` add after `var builder = …`
    ```cs
    builder.AddAzureBlobContainerClient("photos");
    ```
25. `PG.Web.Program.cs` update add using statement. _Skip if using VS. VS Shoud insert this on paste automatically._
    ```cs
    using Azure.Storage.Blobs;
    ```
26. `PG.Web.Program.cs` update `app.MapGet` to be the following.
    ```cs
    app.MapGet("/", async (BlobContainerClient client) =>
    {
        var blobs = client.GetBlobsAsync();
        var photos = new List<string>();
        await foreach(var photo in blobs)
        {
            photos.Add(photo.Name);
        }
        return new RazorComponentResult<PhotoList>(new {Photos = photos } );
    });
    ```
27. `PG.Web.PhotoList.razor` – replace with the code below
    ```
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
    ```
28. `PG.Web`: add Project Reference to ServiceDefaults project. In VS if you checked "Enlist in Aspire" in the New Project Dialog for the web project, you can skip this step.
    ```bash
    dotnet add reference --project .\PhotoGallery.Web\PhotoGallery.Web.csproj .\PhotoGallery.ServiceDefaults\PhotoGallery.ServiceDefaults.csproj
    ```
29. `PG.Web.Program.cs` add after `var builder = …`. In VS if you checked "Enlist in Aspire" in the New Project Dialog for the web project, you can skip this step.
    ```cs
    builder.AddServiceDefaults();
    ```
30. `PG.Web.Program.cs` add after `var app = builder.Build()`. In VS if you checked "Enlist in Aspire" in the New Project Dialog for the web project, you can skip this step.
    ```cs
    app.MapDefaultEndpoints();
    ```
31. `PG.Web.Program.cs` before the line 'app.Run();' add the code below
    ```cs
    app.MapPost("/upload", async (IFormFile photo, BlobContainerClient client) =>
    {
        if (photo.Length > 0)
        {
            var blobClient = client.GetBlobClient(photo.FileName);
            await blobClient.UploadAsync(photo.OpenReadStream(), true);
        }
    
        return Results.Redirect("/");
    });
    ```
32. Verify in the dashboard that Traces has webapp showing up in the Resource dropdown.
33. If you try webapp, you’ll get antiforgery errors. The exception should be in Structured logs in the dashboard.
34. `PG.Web.Program.cs` – add before `var app = builder.Build();`
    ```cs
    builder.Services.AddAntiforgery();
    ```
35. `PG.Web.Program.cs` – add after `var app = builder.Build()`
    ```cs
    app.UseAntiforgery();
    ```
36. `PG.Web.PhotoList.razor` – add using at the top of the file. _Skip if using VS. VS Shoud insert this on paste automatically._
    ```
    @using Microsoft.AspNetCore.Components.Forms
    ```
37. `PG.Web.PhotoList.razor` – Add on a new line after the line contining the `<form>` tag.
    ```html
    <AntiforgeryToken />
    ```
38. The app should be working, after uploading an image, the file name should be listed on the web page.
39. `PG.Web` - add a `wwwroot` folder
40. `PG.Web` - add a new file at `wwwroot/theme.css`, with the content below
    ```css
    body {
        background-color: gray;
    }
    ```
41. `PG.Web.Program.cs` - add after `var app = builder.Build()`
    ```cs
    app.UseStaticFiles();
    ```
42. `PG.Web.PhotoList.razor` - add in `<head>`
    ```html
    <link rel="stylesheet" href="/theme.css"/>
    ```
43. `PG.Web.PhotoList.razor` - replace with the code below
    ```
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
    ```
44. `PG.Web.wwwroot.theme.css` - replace with the content below
    ```css
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

    body {
    background: var(--color-bg);
    background-image: radial-gradient(circle at 25% 20%, hsl(222 40% 18% / .55), transparent 55%),
        radial-gradient(circle at 80% 70%, hsl(222 55% 22% / .35), transparent 60%);
    color: var(--text-strong);
    font-family: system-ui, -apple-system, "Segoe UI", Roboto, Ubuntu, sans-serif;
    min-height: 100dvh;
    margin: 0;
    }

    /* Global layout spacing for gallery component */
    .photo-list-root { padding: clamp(2rem,4vw,3rem) clamp(2rem,5vw,4rem) 4rem; }
    .photo-list-root .content-container { width: 100%; max-width: 1200px; margin-inline: auto; }

    /* Ensure gallery UL default bullets never appear (defensive) */
    ul.gallery { list-style: none !important; margin: 0; padding: 0; }
    ul.gallery > li { list-style: none !important; }
    ```
45. The app should be working now.