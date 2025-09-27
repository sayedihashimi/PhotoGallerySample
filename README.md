# PhotoGallary Setup

The final version of the code is at https://github.com/sayedihashimi/PhotoGallerySample/tree/main.

## Prereqs
1. Install the [latest dotnet](https://github.com/dotnet/dotnet/blob/main/docs/builds-table.md)
1. Install the [latest daily aspire ](https://github.com/dotnet/aspire/blob/main/docs/using-latest-daily.md)
1. If using VS add the following NuGet package source in Tools > Options name=`dotnet10` value=`https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json`.

## Getting started
You can either follow the steps below to start from scratch below. Or you can start with the code in the [start-here](https://github.com/sayedihashimi/PhotoGallerySample/tree/start-here) branch.
When using the `start-here` branch, start with step 4 below.
The `start-here` branch has comments for all the code that will be added. 
There is also a [PhotoList.razor.txt](assets/PhotoList.razor.txt) in the assets folder. This is a version of the
PhotoList.razor file that has code to get started and some commented out code that you can paste in later.

## Demo Steps
1. Create, or open, an empty folder.
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
5. Use the command below to create the Razor Pages web app. In VS select `ASP.NET Core Empty (9.0)` as the project template.
    ```bash
    dotnet new web -o PhotoGallery.Web -f net9.0
    ```
6. Run `dotnet watch --verbose --non-interactive` or `F5`/`CTRL-F5` in Visual Studio.
7. Dashboard should show "No Resources Found"
8. AppHost: `Add Project Reference` to `PhotoGallery.Web`
    ```bash
    dotnet add reference --project .\PhotoGallery.AppHost\PhotoGallery.AppHost.csproj .\PhotoGallery.Web\PhotoGallery.Web.csproj
    ```
9.  `AppHost.cs` add after `var builder = …`
    ```cs
    builder.AddProject<Projects.PhotoGallery_Web>("webapp");
    ```
10. Dashboard should show "webapp" and it should get to running state.
11. AppHost: Add NuGet pkg reference to `Aspire.Hosting.Azure.Storage`
    ```bash
     dotnet add package --project .\PhotoGallery.AppHost\PhotoGallery.AppHost.csproj Aspire.Hosting.Azure.Storage --prerelease
    ```
    - Adjust the version number as needed
    - Note: version must match the version of `Aspire.Hosting.AppHost`
12. AppHost.cs – add code directly below `var builder = …`
    ```cs
    var photos = builder.AddAzureStorage("storage")
                        .RunAsEmulator()
                        .AddBlobs("blobs")
                        .AddBlobContainer("photos");
    ```
13. `PG.Web.Program.cs` – add after the first line (`var builder = …`)
    ```cs
    builder.Services.AddRazorComponents();
    ```
14. PG.Web: Add `Components` folder
15. PG.Web: Add new file `Components\PhotoList.razor` with the contents below.
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
16. `PG.Web.Program.cs` update app.MapGet to be the following
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
17. `PhotoList.razor` update with the following
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
18. The title of the web page should be "Photo List"
19. View dashboard there shouldn’t be any errors
20. `PG.Web` Add NuGet Pkg ref to `Aspire.Azure.Storage.Blobs`
    ```bash
    dotnet add package --project .\PhotoGallery.Web\PhotoGallery.Web.csproj Aspire.Azure.Storage.Blobs --prerelease
    ```
    - Adjust the version number as needed
    - Version must match the version of `Aspire.Hosting.Azure.Storage` in AppHost project.
21. `AppHost.cs` – replace `builder.AddProject<Projects.PhotoGallery_Web>("webapp");` with
    ```cs
    builder.AddProject<Projects.PhotoGallery_Web>("webapp")
            .WithReference(photos)
            .WaitFor(photos);
    ```
22. `PG.Web.Program.cs` add after `var builder = …`
    ```cs
    builder.AddAzureBlobContainerClient("photos");
    ```
23. `PG.Web.Program.cs` update add using statement. _Skip if using VS. VS Shoud insert this on paste automatically._
    ```cs
    using Azure.Storage.Blobs;
    ```
24. `PG.Web.Program.cs` update `app.MapGet` to be the following.
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
25. `PG.Web.PhotoList.razor` – replace with the code below
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
26. `PG.Web`: add Project Reference to ServiceDefaults project
    ```bash
    dotnet add reference src\PhotoGallery.ServiceDefaults\PhotoGallery.ServiceDefaults.csproj
    ```
27. `PG.Web.Program.cs` add after `var builder = …`
    ```cs
    builder.AddServiceDefaults();
    ```
28. `PG.Web.Program.cs` add after `var app = builder.Build()`
    ```cs
    app.MapDefaultEndpoints();
    ```
29. `PG.Web.Program.cs` add after `app.MapGet …`
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
30. Verify in the dashboard that Traces has webapp showing up in the Resource dropdown.
31. If you try webapp, you’ll get antiforgery errors. The exception should be in Structured logs in the dashboard.
32. `PG.Web.Program.cs` – add before `var app = builder.Build();`
    ```cs
    builder.Services.AddAntiforgery();
    ```
33. `PG.Web.Program.cs` – add after `var app = builder.Build()`
    ```cs
    app.UseAntiforgery();
    ```
34. `PG.Web.PhotoList.razor` – add using at the top of the file. _Skip if using VS. VS Shoud insert this on paste automatically._
    ```
    @using Microsoft.AspNetCore.Components.Forms
    ```
35. `PG.Web.PhotoList.razor` – add immediately after opening `<form>` tag.
    ```html
    <AntiforgeryToken />
    ```
36. The app should be working, after uploading an image, the file name should be listed on the web page.
