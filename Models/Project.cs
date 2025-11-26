using System.ComponentModel.DataAnnotations;

namespace SustainabilityCanvas.Api.Models;

public class Project
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProfileId { get; set; }  // FK to Profile

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set;  } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<ProjectCollaborator> ProjectCollaborators { get; set; } = new List<ProjectCollaborator>();
}