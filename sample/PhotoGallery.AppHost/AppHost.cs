var builder = DistributedApplication.CreateBuilder(args);

builder.Build().Run();

/*
var photos = builder.AddAzureStorage("storage")
                .RunAsEmulator()
                .AddBlobs("blobs")
                .AddBlobContainer("photos");

builder.AddProject<Projects.WebApplication1>("webapp");
// remove line above when using code block below.

builder.AddProject<Projects.WebApplication1>("webapp")
        .WithReference(photos)
        .WaitFor(photos);
*/
