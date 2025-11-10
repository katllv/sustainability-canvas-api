using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SustainabilityCanvas.Api.Models;

// Composite key: ImpactId + SdgId
public class ImpactSdg
{
    [Key, Column(Order = 0)]
    public int ImpactId { get; set; }  // FK to Impact

    [Key, Column(Order = 1)]
    public int SdgId { get; set; }     // FK to Sdg

    // to get associated Impact and Sdg
    public Impact Impact { get; set; } = null!;
    public Sdg Sdg { get; set; } = null!;
}