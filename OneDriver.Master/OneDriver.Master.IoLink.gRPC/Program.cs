using DeviceDescriptor.Factory;
using OneDriver.Master.IoLink.gRPC.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure IODDFinder from configuration
var ioddFinderConfig = builder.Configuration.GetSection("IODDFinder");
var baseUrl = ioddFinderConfig["BaseUrl"];
var apiKey = ioddFinderConfig["ApiKey"];

if (!string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(apiKey))
{
    DeviceDescriptorFactory.ConfigureIoddFinder(baseUrl, apiKey);
    builder.Services.AddLogging(logging => logging.AddConsole());
}

// Add services to the container.
builder.Services.AddSingleton<AzureIoTHubService>();
builder.Services.AddSingleton<IoLinkMasterServiceImpl>();
builder.Services.AddSingleton<CloudCommandHandler>();
builder.Services.AddHostedService<IoLinkMasterHostedService>();
builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<IoLinkMasterServiceImpl>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
