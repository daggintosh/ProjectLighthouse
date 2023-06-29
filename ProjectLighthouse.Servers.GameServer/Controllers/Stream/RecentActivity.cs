using LBPUnion.ProjectLighthouse.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace LBPUnion.ProjectLighthouse.Servers.GameServer.Controllers.Stream;

[ApiController]
[Authorize]
[Route("LITTLEBIGPLANETPS3_XML/")]
[Produces("text/xml")]
public class RecentActivity : ControllerBase
{
    private readonly DatabaseContext database;
    public RecentActivity(DatabaseContext db) => this.database = db;

    [HttpGet("stream")]
    [HttpPost("stream")]
    public async Task<IActionResult> FetchActivity()
    {
        return this.Ok();
    }
}

