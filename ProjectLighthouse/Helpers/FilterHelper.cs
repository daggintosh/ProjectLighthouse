using System;
using System.Linq;
using LBPUnion.ProjectLighthouse.Extensions;
using LBPUnion.ProjectLighthouse.Levels;
using LBPUnion.ProjectLighthouse.PlayerData;
using Microsoft.EntityFrameworkCore;

namespace LBPUnion.ProjectLighthouse.Helpers.FilterHelper;

public enum FilterableEndpoint // used to collapse code that would otherwise be two separate functions
{
    Queue,
    FavouriteSlots,
    Genres,
    Categories,
    Generic
}

#nullable enable
public static class FilterHelper
{
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

    public static IQueryable<Slot> filterLevelsByEndpoint
    (
        Database database,
        string? gameFilterType,
        string? dateFilterType,
        string? textFilter,
        GameVersion version,
        string? username,
        FilterableEndpoint endpoint
    )
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
        switch (endpoint)
        {
            case FilterableEndpoint.Queue:
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
                if (textFilter != null) whereQueuedLevels = whereQueuedLevels.Where(s => s.Slot.Name.Contains(textFilter ?? ""));
                return whereQueuedLevels.OrderByDescending(q => q.QueuedLevelId).Include(q => q.Slot.Creator).Include(q => q.Slot.Location).Select(q => q.Slot).ByGameVersion(gameVersion, false, false, true);
            case FilterableEndpoint.FavouriteSlots:
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
                if (textFilter != null) whereHeartedLevels = whereHeartedLevels.Where(s => s.Slot.Name.Contains(textFilter ?? ""));
                return whereHeartedLevels.OrderByDescending(h => h.HeartedLevelId).Include(h => h.Slot.Creator).Include(h => h.Slot.Location).Select(h => h.Slot).ByGameVersion(gameVersion, false, false, true);
            default:
                IQueryable<Slot> slots;
                switch (gameFilterType)
                {
                    case "lbp3":
                    case "lbp2":
                        slots = database.Slots.ByGameVersion(gameVersion, false, true)
                        .Where(s => s.Type == SlotType.User && s.GameVersion == gameVersion);
                        break;
                    default:
                        slots = database.Slots.ByGameVersion(gameVersion, false, true)
                        .Where(s => s.Type == SlotType.User && s.GameVersion <= gameVersion);
                        break;
                }
                if (textFilter != null) slots = slots.Where(s => s.Name.Contains(textFilter ?? ""));
                return slots;
        }
    }
}
#nullable disable