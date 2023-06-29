using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using Discord.Rest;

namespace LBPUnion.ProjectLighthouse.Types.Serialization;

[XmlRoot("stream")]
public class RecentActivityResponse : ILbpSerializable
{
    [XmlElement("start_timestamp")]
    public required long StartTimestamp { get; set; }
    [XmlElement("end_timestamp")]
    public required long EndTimestamp { get; set; }
    [XmlElement("groups")]
    public required List<RAGroup> Groups { get; set; }
}

[XmlRoot("group")]
public class RAGroup : ILbpSerializable
{
    [XmlAttribute("type")]
    public required string GroupType { get; set; }
    [XmlElement("timestamp")]
    public required long Timestamp { get; set; }
    
    // Objects concerned by the event
    [XmlElement("user_id")]
    public int? UserId { get; set; }
    public bool ShouldSerializeUserId() => this.UserId.HasValue;
    [XmlElement("slot_id")]
    public int? SlotId { get; set; }
    public bool ShouldSerializeSlotId() => this.SlotId.HasValue;

    // TODO: News and Team Picks will bypass this and use an event at the top level,
    //       dont serialize this in the future if it doesn't have a value.
    [XmlElement("subgroups")]
    public List<RASubGroup>? SubGroups { get; set; }
    public bool ShouldSerializeSubGroups() => this.SubGroups != null;
}

[XmlRoot("group")]
public class RASubGroup : ILbpSerializable
{
    [XmlAttribute("type")]
    [DefaultValue("user")]
    public required string SubGroupType { get; set; } // I'm pretty sure this will only ever be User, we don't exactly have Slots rating Users

    [XmlElement("timestamp")]
    public required long Timestamp { get; set; }

    [XmlElement("user_id")]
    public required int UserId { get; set; }
    
    [XmlElement("events")]
    public List<RAEvent> Events { get; set; }
}

[XmlRoot("event")]
public class RAEvent : ILbpSerializable
{
    [XmlAttribute("type")]
    public required string EventType { get; set; } // Make sure this goes through EventToString

    [XmlElement("timestamp")]
    public required long Timestamp { get; set; }

    [XmlElement("actor")]
    public required int ActorId { get; set; }

    [XmlElement("object_slot_id")]
    public required int SlotId { get; set; }
}

//  I was just doing my impression of Mystery. Wee-snaw!
//  Well, keep working on it. That was terrible.