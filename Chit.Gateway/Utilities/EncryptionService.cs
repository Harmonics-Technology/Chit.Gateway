using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Chit.Gateway;

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
        var serializedRequest = JsonConvert.SerializeObject(request);
        var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
        // increase the size of the array to fit the key
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
        // use system.text.json to convert the decrypted string to the type T

        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(decryptedString);
    }

    public AESEncryptionResponse EncryptWithAES(dynamic request)
    {
        var serializedRequest = JsonConvert.SerializeObject(request);
        var bytes = Encoding.UTF8.GetBytes(serializedRequest);

        using (var aes = Aes.Create())
        {
            aes.GenerateIV();
            aes.GenerateIV();

            using (var encryptor = aes.CreateEncryptor())
            using (var memoryStream = new MemoryStream())
            {
                using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(bytes, 0, bytes.Length);
                    cryptoStream.FlushFinalBlock();
                }

                var encryptedBytes = memoryStream.ToArray();
                var encryptedString = Convert.ToBase64String(encryptedBytes);
                return new AESEncryptionResponse
                {
                    data = Convert.ToBase64String(encryptedBytes),
                    IV = Convert.ToBase64String(aes.IV),
                    Key = Convert.ToBase64String(aes.Key)
                };
            }
        }
    }

    public T DecryptWithAES<T>(string response, string IV, string Key)
    {
        var encryptedBytes = Convert.FromBase64String(response);
        var aes = Aes.Create();
        aes.IV = Convert.FromBase64String(IV);
        aes.Key = Convert.FromBase64String(Key);

        using (var decryptor = aes.CreateDecryptor())
        using (var memoryStream = new MemoryStream(encryptedBytes))
        using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
        using (var streamReader = new StreamReader(cryptoStream))
        {
            var decryptedString = streamReader.ReadToEnd();
            return JsonConvert.DeserializeObject<T>(decryptedString);
        }
    }

}

public class AESEncryptionResponse
{
    public string data { get; set; }
    public string IV { get; set; }
    public string Key { get; set; }
}

public interface IEncryptionService
{
    dynamic EncryptRequest(dynamic request);
    T DecryptResponse<T>(string response);
    AESEncryptionResponse EncryptWithAES(dynamic request);
    T DecryptWithAES<T>(string response, string IV, string Key);
}