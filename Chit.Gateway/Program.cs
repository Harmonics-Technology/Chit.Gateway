using System.Text;
using Chit.Context;
using Chit.Gateway;
using Chit.Gateway.Utilities;
using Chit.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.SwaggerGen;
using Yarp.ReverseProxy.Swagger;
using Yarp.ReverseProxy.Swagger.Extensions;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Add services to the container.
// load connection string from azure key vault depending on the environment
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// pull appsettings.json into a class Globals
var appSettingsSection = builder.Configuration.GetSection("AppSettings");
builder.Services.Configure<Globals>(builder.Configuration.GetSection("AppSettings"));
// pull the class Globals from the service container


builder.Services.AddChitContext(new ChitContextOptions
{
    ConnectionString = connectionString
});

// add utilities to the service container
builder.Services.AddUtilities(builder.Configuration);

builder.Services.AddTransient<IEncryptionService, EncryptionService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen();
// register service for request encryption
builder.Services.AddTransient<RequestEncryptionsMiddleware>();
builder.Services.AddTransient<SwaggerRequestEncryptionMiddleware>();

var yarpConfiguration = builder.Configuration.GetSection("ReverseProxy");
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(yarpConfiguration)
    .AddSwagger(yarpConfiguration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});



var app = builder.Build();

app.UseCors("AllowAll");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    // intercept requests coming from swagger and encrypt them
    app.UseSwaggerRequestEncryptionMiddleware();

    app.UseSwaggerUI(options =>
    {
        var config = app.Services.GetRequiredService<IOptionsMonitor<ReverseProxyDocumentFilterConfig>>().CurrentValue;
        foreach (var cluster in config.Clusters)
        {
            options.SwaggerEndpoint($"/swagger/{cluster.Key}/swagger.json", $"{cluster.Key.ToUpper()} Microservice");
        }
    });
}
app.UseHttpsRedirection();

// .SetIsOriginAllowed(origin => true)); // allow any origin
// .AllowCredentials());

app.UseRouting();
// add a decryption middleware to decrypt the request before it hits the controller

app.UseAuthorization();

app.MapControllers();


app.UseRequestEncryptionsMiddleware();

app.MapReverseProxy();
// add an encryption middleware to encrypt the response before it leaves the controller

app.Run();

