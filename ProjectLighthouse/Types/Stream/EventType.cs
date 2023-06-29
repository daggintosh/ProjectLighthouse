using System.Diagnostics;
using System.Xml.Serialization;

namespace LBPUnion.ProjectLighthouse.Types.Stream;

public enum EventType
{
    PublishLevel,
    PlayLevel,
    HeartLevel,
    CommentLevel,
    DpadRating,
    LBP1Rate,
    Review,
    Score,
    CommentUser,
    HeartUser,
}

public static class EventTypeHelper
{
    public static EventType StringToEvent(string evString)
    {
        return evString switch
        {
            _ => EventType.PlayLevel,
        };
    }

    public static string EventToString(EventType eventType)
    {
        return eventType switch
        {
            _ => ""
        };
    }     
}