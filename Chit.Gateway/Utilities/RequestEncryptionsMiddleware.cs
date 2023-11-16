using System.Text;
using Newtonsoft.Json;

namespace Chit.Gateway;

// this middleware will decrypt the request before it hits the controller and encrypt the response before it leaves the controller
public class RequestEncryptionsMiddleware : IMiddleware
{
    private readonly IEncryptionService _encryptionService;

    public RequestEncryptionsMiddleware(IEncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        string referrer = context.Request.Headers["Referer"];
        var originalBodyStream = context.Response.Body;

        var memStream = new MemoryStream();
        context.Response.Body = memStream;
        // decrypt request
        // get the encrypted request from the body of the request
        var encryptedRequest = context.Request.Body;
        //parse the body of the request into the EncryptedRequestModel
        encryptedRequest.Seek(0, SeekOrigin.Begin);
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
            context.Request.Body.Position = 0;


        }

        // call next middleware
        await next(context);


        memStream.Position = 0;
        var responseBody = new StreamReader(memStream).ReadToEnd();

        // encrypt response
        if (referrer == null || !referrer.Contains("swagger"))
        {

            var encryptedResponse = _encryptionService.EncryptRequest(responseBody);
            // parse the body of the response into the EncryptedRequestModel

            var responseToReturn = new RequestModel
            {
                EncryptedResponse = encryptedResponse
            };
            responseBody = JsonConvert.SerializeObject(responseToReturn);
        }

        // stringify the response

        var memoryStreamModified = new MemoryStream();
        var sw = new StreamWriter(memoryStreamModified);
        sw.Write(responseBody);
        sw.Flush();
        memoryStreamModified.Position = 0;

        await memoryStreamModified.CopyToAsync(originalBodyStream).ConfigureAwait(false);

        context.Response.Body = originalBodyStream;

        // encrypt response
        // var response = context.Response.Body;
        // //parse the body of the response into the EncryptedRequestModel
        // // response.Seek(0, SeekOrigin.Begin);
        // var responseBody = await new StreamReader(response).ReadToEndAsync();
        // // response.Seek(0, SeekOrigin.Begin);
        // var responseModel = JsonConvert.DeserializeObject<dynamic>(responseBody);

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
