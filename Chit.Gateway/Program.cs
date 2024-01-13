using System.Text;
using Chit.Context;
using Chit.Context.Models.IdentityModels;
using Chit.Gateway;
using Chit.Gateway.Extensions;
using Chit.Gateway.Utilities;
using Chit.Utilities;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.Destructurers;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;
using Serilog.Exceptions.Refit.Destructurers;
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
    ConnectionString = connectionString,
    Configuration = builder.Configuration
});

builder.Host.UseSerilog((_, config) => config
            .ReadFrom.Configuration(configuration)
            .Enrich.WithSpan()
            .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder()
                .WithDefaultDestructurers()
                .WithDestructurers(new IExceptionDestructurer[]
                {
                    new DbUpdateExceptionDestructurer(),
                    new ApiExceptionDestructurer()
                }))
            .Enrich.WithDemystifiedStackTraces());

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


builder.WebHost.ConfigureKestrel(kestrelServerOptions=>
{
    kestrelServerOptions.ResponseHeaderEncodingSelector = _ => System.Text.Encoding.UTF8;
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CustomAuthenticationOptions.DefaultScheme;
    options.DefaultChallengeScheme = CustomAuthenticationOptions.DefaultScheme;

}).AddScheme<CustomAuthenticationOptions, CustomAuthenticationHandler>(CustomAuthenticationOptions.DefaultScheme, options => {});


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

app.UseSerilogRequestLogging();
app.UseChitExceptionHandler();

app.UseCors("AllowAll");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local"))
{
    app.UseSwagger();
    // intercept requests coming from swagger and encrypt them
    // app.UseSwaggerRequestEncryptionMiddleware();

    app.UseSwaggerUI(options =>
    {
        var config = app.Services.GetRequiredService<IOptionsMonitor<ReverseProxyDocumentFilterConfig>>().CurrentValue;
        foreach (var cluster in config.Clusters)
        {
            options.SwaggerEndpoint($"/swagger/{cluster.Key}/swagger.json", $"{cluster.Key} Microservice");
            options.DocumentTitle = $"{cluster.Key} Microservice";

        }
    });
}

app.UseRouting();
// add a decryption middleware to decrypt the request before it hits the controller


app.MapControllers();


// app.UseHttpsRedirection();
app.MapReverseProxy();

app.UseAuthentication();
app.UseAuthorization();
// app.UseRequestEncryptionsMiddleware();
// add an encryption middleware to encrypt the response before it leaves the controller

app.Run();

