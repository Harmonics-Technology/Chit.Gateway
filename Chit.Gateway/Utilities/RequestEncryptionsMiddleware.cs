using System.Text;
using Newtonsoft.Json;

namespace Chit.Gateway;

// this middleware will decrypt the request before it hits the controller and encrypt the response before it leaves the controller
public class RequestEncryptionsMiddleware : IMiddleware
{
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<RequestEncryptionsMiddleware> _logger;

    public RequestEncryptionsMiddleware(IEncryptionService encryptionService, ILogger<RequestEncryptionsMiddleware> logger)
    {
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        string referrer = context.Request.Headers["Referer"];

        var originBody = context.Response.Body;
        using var newBody = new MemoryStream();
        context.Response.Body = newBody;
        var originalBodyStream = context.Response.Body;

        var memStream = new MemoryStream();
        // context.Response.Body = memStream;
        // decrypt request
        // get the encrypted request from the body of the request
        var encryptedRequest = context.Request.Body;
        //parse the body of the request into the EncryptedRequestModel
        // encryptedRequest.Seek(0, SeekOrigin.Begin);
        var encryptedRequestBody = await new StreamReader(encryptedRequest).ReadToEndAsync();
        // encryptedRequest.Seek(0, SeekOrigin.Begin);
        var encryptedRequestModel = JsonConvert.DeserializeObject<RequestModel>(encryptedRequestBody);

        if (encryptedRequestModel != null)
        {
            var decryptedRequest = _encryptionService.DecryptResponse<dynamic>(encryptedRequestModel.EncryptedRequest);
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(decryptedRequest)));
            // set request content length to the length of the new body
            context.Request.ContentLength = context.Request.Body.Length;
            // avoid issue with other middlewares reading the body
            // context.Request.Body.Position = 0;


        }

        // call next middleware
        await next(context);


        try
        {
            memStream.Position = 0;
            var responseBody = new StreamReader(memStream).ReadToEnd();

            // encrypt response
            if (referrer == null || !referrer.Contains("swagger"))
            {

                await ModifyResponseBody(context);
                // var encryptedResponse = _encryptionService.EncryptRequest(responseBody);
                // // parse the body of the response into the EncryptedRequestModel

                // var responseToReturn = new RequestModel
                // {
                //     EncryptedResponse = encryptedResponse
                // };
                // var serializedResponse = JsonConvert.SerializeObject(responseToReturn);
                // _logger.LogInformation("serializedResponse: {serializedResponse}", serializedResponse);
                // context.Response.ContentLength = serializedResponse.Length;
                // responseBody = serializedResponse;
            }

            // stringify the response

            newBody.Seek(0, SeekOrigin.Begin);
            await newBody.CopyToAsync(originBody);
            context.Response.Body = originBody;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error encrypting response body {ex}", ex);
            throw;
        }

        // encrypt response
        // var response = context.Response.Body;
        // //parse the body of the response into the EncryptedRequestModel
        // // response.Seek(0, SeekOrigin.Begin);
        // var responseBody = await new StreamReader(response).ReadToEndAsync();
        // // response.Seek(0, SeekOrigin.Begin);
        // var responseModel = JsonConvert.DeserializeObject<dynamic>(responseBody);

    }

    private async Task ModifyResponseBody(HttpContext context)
    {
        var originalBodyStream = context.Response.Body;

        originalBodyStream.Seek(0, SeekOrigin.Begin);
        var originalResponseBody = await new StreamReader(originalBodyStream).ReadToEndAsync();
        var encryptedResponse = _encryptionService.EncryptWithAES(originalResponseBody);

        var responseToReturn = new RequestModel
        {
            EncryptedResponse = encryptedResponse.IV + encryptedResponse.data + encryptedResponse.Key
        };

        var serializedResponse = JsonConvert.SerializeObject(responseToReturn);

        originalBodyStream.SetLength(0);

        await originalBodyStream.WriteAsync(Encoding.UTF8.GetBytes(serializedResponse));

        context.Response.Body = originalBodyStream;
        context.Response.ContentLength = originalBodyStream.Length;
    }
}

public static class RequestEncryptionsMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestEncryptionsMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestEncryptionsMiddleware>();
    }
}

public class RequestModel
{
    public string EncryptedRequest { get; set; }
    public string EncryptedResponse { get; set; }
}


