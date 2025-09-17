# PhotoGallary Setup

## Prereqs
1. Install the [latest dotnet](https://github.com/dotnet/dotnet/blob/main/docs/builds-table.md)
1. Install the [latest daily aspire ](https://github.com/dotnet/aspire/blob/main/docs/using-latest-daily.md)

## Getting started
You can either follow the steps below to start from scratch below. Or you can start with the code in the [start-here](https://github.com/sayedihashimi/PhotoGallerySample/tree/start-here) branch.
When using the `start-here` branch, start with step 3 below.
The `start-here` branch has comments for all the code that will be added. 
There is also a [PhotoList.razor.txt](assets/PhotoList.razor.txt) in the assets folder. This is a version of the
PhotoList.razor file that has code to get started and some commented out code that you can paste in later.

## Demo Steps
1. Create Aspire Empty App – named `PhotoGallery`
2. Add new project: ASP.NET Core Empty (9.0) – named `WebApplication1`
3. F5
4. Dashboard should show “No Resources Found”
5. App Host: `Add Project Reference` to `WebApplication1`
    - `dotnet add reference src\WebApplication1\WebApplication1.csproj`
6. Save All in VS
7. AppHost project - Add NuGet pkg reference to `Aspire.Hosting.Azure.Storage` (version `9.5.0-preview.1.25466.2`)
    - `dotnet add package Aspire.Hosting.Azure.Storage -v 9.5.0-preview.1.25466.2`
    - Adjust the version number as needed
    - Note: version must match the version of `Aspire.Hosting.AppHost`

8. `AppHost.cs` add after `var builder = …`

```cs
builder.AddProject<Projects.WebApplication1>(“webapp”);
```

9. Dashboard should show webapp1 and it should get to running state.
10. AppHost.cs – add code directly below `var builder = …`

```cs
var photos = builder.AddAzureStorage("storage")
.RunAsEmulator()
.AddBlobs("blobs")
.AddBlobContainer("photos");
```

11. WebApplication1.Program.cs – add after the first line (`var builder = …`)

```cs
builder.Services.AddRazorComponents();
```

12. WebApp1: Add `Components` folder
13. WebApp1: Add new file `Components\PhotoList.razor` with the contents below.

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

14. `WebApp1.Program.cs` update app.MapGet to be the following

```
app.MapGet("/", () => 
{
    return new RazorComponentResult<PhotoList>(new {Photos = Array.Empty<string>() } );
});
```

  - This should add a reference -- `using WebApplication1.Components;`

15. PhotoList.razor update with the following

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

16. The title of the web page should be “Photo List”
17. View dashboard there shouldn’t be any errors
18. WebApp1 Add NuGet Pkg ref to `Aspire.Azure.Storage.Blobs` - `dotnet add package Aspire.Azure.Storage.Blobs -v 9.5.0-preview.1.25466.2`
    - Adjust the version number as needed
    - Version must match the version of `Aspire.Hosting.Azure.Storage` in AppHost project.

19. AppHost.cs – add after `var photos = …`

```cs
builder.AddProject<Projects.WebApplication1>("webapp")
        .WithReference(photos)
        .WaitFor(photos);
```

20. WebApp1.Program.cs add after `var builder = …`

```cs
builder.AddAzureBlobContainerClient("photos");
```

21. WebApp1.Program.cs update `app.MapGet` to be the following. Note it will need this using statement to work `using Azure.Storage.Blobs;`

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

22. WebApp1.PhotoList.razor – replace with the code below

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

23. WebApp1 add Project Reference to ServiceDefaults project
    - `dotnet add reference src\PhotoGallery.ServiceDefaults\PhotoGallery.ServiceDefaults.csproj`
24. WebApp1.Program.cs add after `var builder = …`

```cs
builder.AddServiceDefaults();
```

25. WebApp1.Program.cs add after `var app = builder.Build()`

```cs
app.MapDefaultEndpoints();
```

26. WebApp1.Program.cs add after `app.MapGet …`

```
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

27. Verify in the dashboard that Traces has webapp1 showing up in the Resource dropdown.
28. If you try WebApp1, you’ll get antiforgery errors
29. WebApp1.Program.cs – add before `var app = builder.Build();`

```cs
builder.Services.AddAntiforgery();
```

30. WebApp1.Program.cs – add after `var app = builder.Build()`

```cs
app.UseAntiforgery();
```

31. WebApp1.PhotoList.razor – add immediately after opening `<form>` tag.

```html
<AntiforgeryToken />
```

32. Note: the previous step should have added a using statement in PhotoList.razor
@using Microsoft.AspNetCore.Components.Forms
• Issue: Missing using statements aren’t getting added when editing .razor files. They are getting added when editing .cs files.
33. The app should be working, after uploading an image, the file name should be listed on the web page.
