using System.Text;
using Chit.Context;
using Chit.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddTransient<IEncryptionService, EncryptionService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// register service for request encryption
builder.Services.AddTransient<RequestEncryptionsMiddleware>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    // intercept requests coming from swagger and encrypt them
    app.Use((context, next) =>
    {
        if (context.Request.Method == "GET" || context.Request.Method == "OPTIONS" || context.Request.ContentLength <= 0)
        {
            return next();
        }
        string referrer = context.Request.Headers["Referer"];
        if (referrer != null && referrer.Contains("swagger"))
        {
            // var encryptedRequest = app.Services.GetRequiredService<IEncryptionService>().EncryptRequest(context.Request.Body);
            // convert body from stream to json string
            var body = context.Request.Body;
            // confirm if the body is empty
            // body.Seek(0, SeekOrigin.Begin);
            var requestBody = new StreamReader(body).ReadToEndAsync().Result;
            // body.Seek(0, SeekOrigin.Begin);
            // convert json string to dynamic object
            var request = JsonConvert.DeserializeObject<dynamic>(requestBody);
            // encrypt the dynamic object
            var encryptedRequest = app.Services.GetRequiredService<IEncryptionService>().EncryptRequest(request);

            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new RequestModel
            {
                EncryptedRequest = encryptedRequest
            })));
        }
        return next();

        // decrypt response for swagger requests

    });
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();
// add a decryption middleware to decrypt the request before it hits the controller

app.UseAuthorization();

app.MapControllers();

app.UseRequestEncryptionsMiddleware();
// add an encryption middleware to encrypt the response before it leaves the controller

app.Run();

