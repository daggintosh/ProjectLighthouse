#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using LBPUnion.ProjectLighthouse.PlayerData;
using LBPUnion.ProjectLighthouse.Serialization;

namespace LBPUnion.ProjectLighthouse.Levels;

public class CustomRewards
{
    [Key]
    [XmlAttribute("SlotId")]
    public int SlotId {get; set;}

    [ForeignKey(nameof(SlotId))]
    [JsonIgnore]
    public Slot? Slot {get; set;}


}
#nullable disable