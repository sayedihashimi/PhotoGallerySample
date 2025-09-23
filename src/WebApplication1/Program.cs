using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http.HttpResults;
using WebApplication1.Components;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddAzureBlobContainerClient("photos");
builder.Services.AddRazorComponents();
builder.Services.AddAntiforgery();

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseAntiforgery();
// Enable serving static web assets (needed for CSS isolation, etc.)
app.UseStaticFiles();

app.MapGet("/", async (BlobContainerClient client) =>
{
    var blobs = client.GetBlobsAsync();
    var photos = new List<string>();
    await foreach (var photo in blobs)
    {
        photos.Add(photo.Name);
    }
    return new RazorComponentResult<PhotoList>(new { Photos = photos });
});

// Stream individual photo blobs so they can be referenced as /photos/{name}
app.MapGet("/photos/{name}", async Task<Results<NotFound, FileStreamHttpResult>> (string name, BlobContainerClient client, CancellationToken ct) =>
{
    var blob = client.GetBlobClient(name);
    if (!await blob.ExistsAsync(ct))
    {
        return TypedResults.NotFound();
    }
    var stream = await blob.OpenReadAsync(cancellationToken: ct);
    // Naive content type detection based on extension
    var contentType = name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" :
                      name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" :
                      name.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? "image/gif" :
                      name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp" :
                      "application/octet-stream";
    return TypedResults.File(stream, contentType);
});

app.MapPost("/upload", async (IFormFile photo, BlobContainerClient client) =>
{
    if (photo.Length > 0)
    {
        var blobClient = client.GetBlobClient(photo.FileName);
        await blobClient.UploadAsync(photo.OpenReadStream(), true);
    }

    return Results.Redirect("/");
});



app.Run();
