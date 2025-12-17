using System.IO;
using Microsoft.AspNetCore.Http;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http.HttpResults;
using PhotoGallery.Web.Components;
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddAzureBlobContainerClient("photos");
builder.Services.AddRazorComponents();
builder.Services.AddAntiforgery();
var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapDefaultEndpoints();

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

app.MapPost("/upload", async (IFormFile photo, BlobContainerClient client) =>
{
    if (photo.Length > 0)
    {
        var blobClient = client.GetBlobClient(photo.FileName);
        await blobClient.UploadAsync(photo.OpenReadStream(), true);
    }
    return Results.Redirect("/");
});
app.MapGet("/photos/{*name}", async (string name, BlobContainerClient client) =>
{
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest();
    }

    var blob = client.GetBlobClient(name);
    if (!await blob.ExistsAsync())
    {
        return Results.NotFound();
    }

    // Try to infer a simple content type from the extension; fall back to octet-stream
    var contentType = GetContentType(name);
    var stream = await blob.OpenReadAsync();
    return Results.File(stream, contentType);
});
app.Run();
static string GetContentType(string name)
{
    var ext = Path.GetExtension(name).ToLowerInvariant();
    return ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream"
    };
}

