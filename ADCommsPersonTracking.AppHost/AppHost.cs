var builder = DistributedApplication.CreateBuilder(args);


builder.AddProject<Projects.ADCommsPersonTracking_Api>("adcommspersontracking-api");
builder.AddProject<Projects.ADCommsPersonTracking_Web>("adcommspersontracking-web");


builder.Build().Run();
