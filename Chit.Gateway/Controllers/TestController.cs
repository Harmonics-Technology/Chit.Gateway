using System.Text;
using Chit.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chit.Gateway;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly ChitContext _context;
    private readonly IEncryptionService _encryptionService;

    public TestController(ChitContext context, IEncryptionService encryptionService)
    {
        _context = context;
        _encryptionService = encryptionService;
    }

    [HttpGet]
    // [Authorize]
    public async Task<IActionResult> Get()
    {
        var newChit = new
        {
            Name = "Test Chit",
            CreatedBy = "Test User",
            CreatedOn = DateTime.UtcNow,
            ModifiedBy = "Test User",
            ModifiedOn = DateTime.UtcNow
        };
        // var encryptedRequest = _encryptionService.EncryptRequest(newChit);
        // var decryptedRequest = _encryptionService.DecryptResponse<dynamic>(encryptedRequest);
        var users = await _context.Users.ToListAsync();
        var ç = new
        {
            Name = "Test Chit",
            CreatedBy = "Test User",
            CreatedOn = DateTime.UtcNow,
            ModifiedBy = "Test User",
            ModifiedOn = DateTime.UtcNow
        };
        return Ok(ç);
    }

    [HttpPost]
    public async Task<ActionResult> CreateSomething(SomethingModel model)
    {
        Console.WriteLine(model);
        return Ok(model);
    }

    [HttpPost("encrypt")]
    public async Task<ActionResult> EncryptSomething(SomethingModel model)
    {
        var encryptedRequest = _encryptionService.EncryptWithAES(model.JsonToEncrypt);
        return Ok(encryptedRequest);
    }

    [HttpPost("decrypt")]
    public async Task<ActionResult> DecryptSomething(SomethingModel model)
    {
        var encryptedRequestParts = Encoding.UTF8.GetString(Convert.FromBase64String(model.StringToDecrypt)).Split("||");
        var decryptedRequest = _encryptionService.DecryptWithAES<dynamic>(encryptedRequestParts[1], encryptedRequestParts[0], encryptedRequestParts[2]);
        return Ok(decryptedRequest);
    }
}


public class SomethingModel
{
    public string Name { get; set; }
    public string Description { get; set; }

    public string JsonToEncrypt { get; set; }
    public string StringToDecrypt { get; set; }
}