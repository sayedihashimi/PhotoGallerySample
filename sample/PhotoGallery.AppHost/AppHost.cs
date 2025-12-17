var builder = DistributedApplication.CreateBuilder(args);
var photos = builder.AddAzureStorage("storage")
                        .RunAsEmulator()
                        .AddBlobContainer("photos");
builder.AddProject<Projects.PhotoGallery_Web>("webapp")
            .WithReference(photos)
            .WaitFor(photos);

builder.Build().Run();
