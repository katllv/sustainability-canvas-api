using System.ComponentModel.DataAnnotations;

namespace SustainabilityCanvas.Api.Models;

public enum SectionType
{
    KS,
    KA,
    WM,
    KTR,
    UVP, 
    CO,
    RE,
    CS,
    CR,
    CH,
    GO
}

public enum SustainabilityDimension
{
    Environmental,
    Social,
    Economic
}

public enum RelationType
{
    Direct,
    Indirect, 
    Hidden
}

public class Impact
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProjectId { get; set; }  // FK to Project
    
    [Required]
    public SectionType Type { get; set; }

    [Required]
    [Range(1, 10)]
    public int Score { get; set; }

    [Required]
    public SustainabilityDimension Dimension { get; set; }

    [Required]
    public RelationType Relation { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // to get associated SDGs
    public ICollection<ImpactSdg> ImpactSdgs { get; set; } = new List<ImpactSdg>();
}