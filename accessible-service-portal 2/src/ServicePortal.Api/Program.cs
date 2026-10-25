using ServicePortal.Api.Configuration;
using ServicePortal.Application;
using ServicePortal.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration);

var app = builder.Build();

app.UseApiPipeline();
app.MapApiEndpoints();

await app.RunAsync();

/// <summary>Entry point; exposed so integration tests can host the app with WebApplicationFactory.</summary>
public partial class Program;
