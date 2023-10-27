using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Chit.Identity;

public class EncryptionService : IEncryptionService
{
    private readonly Globals _globals;

    public EncryptionService(IOptions<Globals> globals)
    {
        _globals = globals.Value;
    }

    public dynamic EncryptRequest(dynamic request)
    {
        // compose path to certificate
        var certificatePath = _globals.RSAPublicCertificatePath;
        var certificateFullPath = Path.Combine(Directory.GetCurrentDirectory(), certificatePath);
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(certificateFullPath));
        // convert request to json string then to bytes array
        var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
        var encryptedBytes = rsa.Encrypt(bytes, RSAEncryptionPadding.Pkcs1);
        var encryptedString = Convert.ToBase64String(encryptedBytes);
        return encryptedString;
    }

    public T DecryptResponse<T>(string response)
    {
        var certificatePath = _globals.RSAPrivateCertificatePath;
        var certificateFullPath = Path.Combine(Directory.GetCurrentDirectory(), certificatePath);
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(certificateFullPath));
        var decryptedBytes = rsa.Decrypt(Convert.FromBase64String(response), RSAEncryptionPadding.Pkcs1);
        // var RSAParameters = rsa.ExportParameters(false);
        // var decryptedBytes = RSAHelper.RSADecrypt(Convert.FromBase64String(response), RSAParameters, false);
        var decryptedString = System.Text.Encoding.UTF8.GetString(decryptedBytes);
        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(decryptedString);
    }

}

public interface IEncryptionService
{
    dynamic EncryptRequest(dynamic request);
    T DecryptResponse<T>(string response);
}