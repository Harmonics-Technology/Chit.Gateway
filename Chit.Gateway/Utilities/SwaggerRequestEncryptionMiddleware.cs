using System.Text;
using Newtonsoft.Json;

namespace Chit.Gateway;

public class SwaggerRequestEncryptionMiddleware : IMiddleware
{
    private readonly IEncryptionService _encryptionService;

    public SwaggerRequestEncryptionMiddleware(IEncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Method == "GET" || context.Request.Method == "OPTIONS" || context.Request.ContentLength <= 0)
        {
            await next(context);
        }
        else
        {

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
                var encryptedRequest = _encryptionService.EncryptRequest(request);

                context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new RequestModel
                {
                    EncryptedRequest = encryptedRequest
                })));
                await next(context);
            }
        }
        // call next middleware

        // decrypt response if the request is from swagger
        // string referrer2 = context.Request.Headers["Referer"];
        // if (referrer2 != null && referrer2.Contains("swagger"))
        // {
        //     var originalBodyStream = context.Response.Body;
        //     var memStream = new MemoryStream();
        //     context.Response.Body = memStream;

        //     memStream.Position = 0;
        //     var responseBody = new StreamReader(memStream).ReadToEnd();

        //     var decryptedResponse = _encryptionService.DecryptResponse<dynamic>(responseBody);

        //     context.Response.Body = originalBodyStream;
        //     // await ((object)context.Response).WriteAsync(JsonConvert.SerializeObject(decryptedResponse), Encoding.UTF8,default);
        //     // await HttpResponseWritingExtensions.WriteAsync(context.Response, JsonConvert.SerializeObject(decryptedResponse), Encoding.UTF8);
        // }
    }
}

public static class SwaggerRequestEncryptionMiddlewareExtensions
{
    public static IApplicationBuilder UseSwaggerRequestEncryptionMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SwaggerRequestEncryptionMiddleware>();
    }
}
