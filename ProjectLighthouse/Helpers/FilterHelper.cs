using System;
using System.Linq;
using LBPUnion.ProjectLighthouse.Extensions;
using LBPUnion.ProjectLighthouse.Levels;
using LBPUnion.ProjectLighthouse.PlayerData;
using Microsoft.EntityFrameworkCore;

namespace LBPUnion.ProjectLighthouse.Helpers;

#nullable enable
public static class FilterHelper {
    public enum ListFilterType // used to collapse code that would otherwise be two separate functions
    {
        Queue,
        FavouriteSlots,
        LBP3_Search
    }

    public static GameVersion getGameFilter(string? gameFilterType, GameVersion version)
    {
        if (version == GameVersion.LittleBigPlanetVita) return GameVersion.LittleBigPlanetVita;
        if (version == GameVersion.LittleBigPlanetPSP) return GameVersion.LittleBigPlanetPSP;

        return gameFilterType switch
        {
            "lbp1" => GameVersion.LittleBigPlanet1,
            "lbp2" => GameVersion.LittleBigPlanet2,
            "lbp3" => GameVersion.LittleBigPlanet3,
            "both" => GameVersion.LittleBigPlanet2, // LBP2 default option
            null => GameVersion.LittleBigPlanet1,
            _ => GameVersion.Unknown,
        };
    }

    public static IQueryable<Slot> filterListByRequest(Database database, string? gameFilterType, string? dateFilterType, GameVersion version, string username, ListFilterType filterType)
    {
        if (version == GameVersion.LittleBigPlanetVita || version == GameVersion.LittleBigPlanetPSP || version == GameVersion.Unknown)
        {
            return database.Slots.ByGameVersion(version, false, true);
        }

        string _dateFilterType = dateFilterType ?? "";

        long oldestTime = _dateFilterType switch
        {
            "thisWeek" => DateTimeOffset.Now.AddDays(-7).ToUnixTimeMilliseconds(),
            "thisMonth" => DateTimeOffset.Now.AddDays(-31).ToUnixTimeMilliseconds(),
            _ => 0,
        };

        GameVersion gameVersion = getGameFilter(gameFilterType, version);

        if (filterType == ListFilterType.Queue)
        {
            IQueryable<QueuedLevel> whereQueuedLevels;

            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (gameFilterType == "both")
                // Get game versions less than the current version
                // Needs support for LBP3 ("both" = LBP1+2)
                whereQueuedLevels = database.QueuedLevels.Where(q => q.User.Username == username)
                .Where(q => q.Slot.Type == SlotType.User && !q.Slot.Hidden && q.Slot.GameVersion <= gameVersion && q.Slot.FirstUploaded >= oldestTime);
            else
                // Get game versions exactly equal to gamefiltertype
                whereQueuedLevels = database.QueuedLevels.Where(q => q.User.Username == username)
                .Where(q => q.Slot.Type == SlotType.User && !q.Slot.Hidden && q.Slot.GameVersion == gameVersion && q.Slot.FirstUploaded >= oldestTime);

            return whereQueuedLevels.OrderByDescending(q => q.QueuedLevelId).Include(q => q.Slot.Creator).Include(q => q.Slot.Location).Select(q => q.Slot).ByGameVersion(gameVersion, false, false, true);
        } else
        {
            IQueryable<HeartedLevel> whereHeartedLevels;

            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (gameFilterType == "both")
                // Get game versions less than the current version
                // Needs support for LBP3 ("both" = LBP1+2)
                whereHeartedLevels = database.HeartedLevels.Where(h => h.User.Username == username)
                .Where(h => (h.Slot.Type == SlotType.User || h.Slot.Type == SlotType.Developer) && !h.Slot.Hidden && h.Slot.GameVersion <= gameVersion && h.Slot.FirstUploaded >= oldestTime);
            else
                // Get game versions exactly equal to gamefiltertype
                whereHeartedLevels = database.HeartedLevels.Where(h => h.User.Username == username)
                .Where(h => (h.Slot.Type == SlotType.User || h.Slot.Type == SlotType.Developer) && !h.Slot.Hidden && h.Slot.GameVersion == gameVersion && h.Slot.FirstUploaded >= oldestTime);

            return whereHeartedLevels.OrderByDescending(h => h.HeartedLevelId).Include(h => h.Slot.Creator).Include(h => h.Slot.Location).Select(h => h.Slot).ByGameVersion(gameVersion, false, false, true);
        }
    }
}
#nullable disable