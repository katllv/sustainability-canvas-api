using System.ComponentModel.DataAnnotations;

namespace SustainabilityCanvas.Api.Models;

public class ProjectCollaborator
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProfileId { get; set; }  // FK to Profile

    [Required]
    public int ProjectId { get; set; }  // FK to Project

    // to get associated Profile and Project
    public Profile Profile { get; set; } = null!;
    public Project Project { get; set; } = null!;
}