#nullable enable
using LBPUnion.ProjectLighthouse.Extensions;
using LBPUnion.ProjectLighthouse.Helpers;
using LBPUnion.ProjectLighthouse.Levels;
using LBPUnion.ProjectLighthouse.PlayerData;
using LBPUnion.ProjectLighthouse.PlayerData.Profiles;
using LBPUnion.ProjectLighthouse.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LBPUnion.ProjectLighthouse.Servers.GameServer.Controllers.Slots;

[ApiController]
[Route("LITTLEBIGPLANETPS3_XML/")]
[Produces("text/xml")]
public class ListController : ControllerBase
{
    private readonly Database database;
    public ListController(Database database)
    {
        this.database = database;
    }

    #region Levels

    #region Level Queue (lolcatftw)

    [HttpGet("slots/lolcatftw/{username}")]
    public async Task<IActionResult> GetQueuedLevels
    (
        string username,
        [FromQuery] int pageStart,
        [FromQuery] int pageSize,
        [FromQuery] string? gameFilterType = null,
        [FromQuery] int? players = null,
        [FromQuery] bool? move = null,
        [FromQuery] string? dateFilterType = null
    )
    {
        GameToken? token = await this.database.GameTokenFromRequest(this.Request);
        if (token == null) return this.StatusCode(403, "");

        if (pageSize <= 0) return this.BadRequest();

        GameVersion gameVersion = token.GameVersion;

        IEnumerable<Slot> queuedLevels = FilterHelper.filterListByRequest(this.database, gameFilterType, dateFilterType, token.GameVersion, username, FilterHelper.ListFilterType.Queue)
            .Skip(Math.Max(0, pageStart - 1))
            .Take(Math.Min(pageSize, 30))
            .AsEnumerable();

        string response = queuedLevels.Aggregate(string.Empty, (current, q) => current + q.Serialize(gameVersion));

        return this.Ok
        (
            LbpSerializer.TaggedStringElement("slots", response, new Dictionary<string, object>
            {
                { "total", await this.database.QueuedLevels.CountAsync(q => q.UserId == token.UserId) },
                { "hint_start", pageStart + Math.Min(pageSize, 30) },
            })
        );
    }

    [HttpPost("lolcatftw/add/user/{id:int}")]
    public async Task<IActionResult> AddQueuedLevel(int id)
    {
        GameToken? token = await this.database.GameTokenFromRequest(this.Request);
        if (token == null) return this.StatusCode(403, "");

        Slot? slot = await this.database.Slots.FirstOrDefaultAsync(s => s.SlotId == id);
        if (slot == null) return this.NotFound();

        await this.database.QueueLevel(token.UserId, slot);

        return this.Ok();
    }

    [HttpPost("lolcatftw/remove/user/{id:int}")]
    public async Task<IActionResult> RemoveQueuedLevel(int id)
    {
        GameToken? token = await this.database.GameTokenFromRequest(this.Request);
        if (token == null) return this.StatusCode(403, "");

        Slot? slot = await this.database.Slots.FirstOrDefaultAsync(s => s.SlotId == id);
        if (slot == null) return this.NotFound();

        await this.database.UnqueueLevel(token.UserId, slot);

        return this.Ok();
    }

    [HttpPost("lolcatftw/clear")]
    public async Task<IActionResult> ClearQueuedLevels()
    {
        GameToken? token = await this.database.GameTokenFromRequest(this.Request);
        if (token == null) return this.StatusCode(403, "");

        this.database.QueuedLevels.RemoveRange(this.database.QueuedLevels.Where(q => q.UserId == token.UserId));

        await this.database.SaveChangesAsync();

        return this.Ok();
    }

    #endregion

    #region Hearted Levels

    [HttpGet("favouriteSlots/{username}")]
    public async Task<IActionResult> GetFavouriteSlots
    (
        string username,
        [FromQuery] int pageStart,
        [FromQuery] int pageSize,
        [FromQuery] string? gameFilterType = null,
        [FromQuery] int? players = null,
        [FromQuery] bool? move = null,
        [FromQuery] string? dateFilterType = null
    )
    {
        GameToken? token = await this.database.GameTokenFromRequest(this.Request);
        if (token == null) return this.StatusCode(403, "");

        if (pageSize <= 0) return this.BadRequest();

        GameVersion gameVersion = token.GameVersion;

        User? targetUser = await this.database.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (targetUser == null) return this.StatusCode(403, "");

        IEnumerable<Slot> heartedLevels = FilterHelper.filterListByRequest(this.database, gameFilterType, dateFilterType, token.GameVersion, username, FilterHelper.ListFilterType.FavouriteSlots)
            .Skip(Math.Max(0, pageStart - 1))
            .Take(Math.Min(pageSize, 30))
            .AsEnumerable();

        string response = heartedLevels.Aggregate(string.Empty, (current, q) => current + q.Serialize(gameVersion));

        return this.Ok
        (
            LbpSerializer.TaggedStringElement("favouriteSlots", response, new Dictionary<string, object>
            {
                { "total", await this.database.HeartedLevels.CountAsync(q => q.UserId == targetUser.UserId) },
                { "hint_start", pageStart + Math.Min(pageSize, 30) },
            })
        );
    }

    private const int FirstLbp2DeveloperSlotId = 124806; // This is the first known level slot GUID in LBP2. Feel free to change it if a lower one is found.

    [HttpPost("favourite/slot/{slotType}/{id:int}")]
    public async Task<IActionResult> AddFavouriteSlot(string slotType, int id)
    {
        GameToken? token = await this.database.GameTokenFromRequest(this.Request);
        if (token == null) return this.StatusCode(403, "");

        if (SlotHelper.IsTypeInvalid(slotType)) return this.BadRequest();

        if (slotType == "developer") id = await SlotHelper.GetPlaceholderSlotId(this.database, id, SlotType.Developer);

        Slot? slot = await this.database.Slots.FirstOrDefaultAsync(s => s.SlotId == id);
        if (slot == null) return this.NotFound();

        if (slotType == "developer")
        {
            GameVersion slotGameVersion = (slot.InternalSlotId < FirstLbp2DeveloperSlotId) ? GameVersion.LittleBigPlanet1 : token.GameVersion;
            slot.GameVersion = slotGameVersion;
        }

        await this.database.HeartLevel(token.UserId, slot);

        return this.Ok();
    }

    [HttpPost("unfavourite/slot/{slotType}/{id:int}")]
    public async Task<IActionResult> RemoveFavouriteSlot(string slotType, int id)
    {
        GameToken? token = await this.database.GameTokenFromRequest(this.Request);
        if (token == null) return this.StatusCode(403, "");

        if (SlotHelper.IsTypeInvalid(slotType)) return this.BadRequest();

        if (slotType == "developer") id = await SlotHelper.GetPlaceholderSlotId(this.database, id, SlotType.Developer);

        Slot? slot = await this.database.Slots.FirstOrDefaultAsync(s => s.SlotId == id);
        if (slot == null) return this.NotFound();

        if (slotType == "developer")
        {
            GameVersion slotGameVersion = (slot.InternalSlotId < FirstLbp2DeveloperSlotId) ? GameVersion.LittleBigPlanet1 : token.GameVersion;
            slot.GameVersion = slotGameVersion;
        }
        
        await this.database.UnheartLevel(token.UserId, slot);

        return this.Ok();
    }

    #endregion

    #endregion Levels

    #region Users

    [HttpGet("favouriteUsers/{username}")]
    public async Task<IActionResult> GetFavouriteUsers(string username, [FromQuery] int pageSize, [FromQuery] int pageStart)
    {
        GameToken? token = await this.database.GameTokenFromRequest(this.Request);
        if (token == null) return this.StatusCode(403, "");

        User? targetUser = await this.database.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (targetUser == null) return this.StatusCode(403, "");

        if (pageSize <= 0) return this.BadRequest();

        IEnumerable<User> heartedProfiles = this.database.HeartedProfiles.Include
                (q => q.HeartedUser)
            .OrderBy(q => q.HeartedProfileId)
            .Where(q => q.UserId == targetUser.UserId)
            .Include(q => q.HeartedUser.Location)
            .Select(q => q.HeartedUser)
            .Skip(Math.Max(0, pageStart - 1))
            .Take(Math.Min(pageSize, 30))
            .AsEnumerable();

        string response = heartedProfiles.Aggregate(string.Empty, (current, u) => current + u.Serialize(token.GameVersion));

        return this.Ok
        (
            LbpSerializer.TaggedStringElement("favouriteUsers", response, new Dictionary<string, object>
            {
                { "total", await this.database.HeartedProfiles.CountAsync(q => q.UserId == targetUser.UserId) },
                { "hint_start", pageStart + Math.Min(pageSize, 30) },
            })
        );
    }

    [HttpPost("favourite/user/{username}")]
    public async Task<IActionResult> AddFavouriteUser(string username)
    {
        GameToken? token = await this.database.GameTokenFromRequest(this.Request);
        if (token == null) return this.StatusCode(403, "");

        User? heartedUser = await this.database.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (heartedUser == null) return this.NotFound();

        await this.database.HeartUser(token.UserId, heartedUser);

        return this.Ok();
    }

    [HttpPost("unfavourite/user/{username}")]
    public async Task<IActionResult> RemoveFavouriteUser(string username)
    {
        GameToken? token = await this.database.GameTokenFromRequest(this.Request);
        if (token == null) return this.StatusCode(403, "");

        User? heartedUser = await this.database.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (heartedUser == null) return this.NotFound();

        await this.database.UnheartUser(token.UserId, heartedUser);

        return this.Ok();
    }

    #endregion
}
