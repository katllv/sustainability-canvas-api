using System.ComponentModel.DataAnnotations;

namespace SustainabilityCanvas.Api.Models;

public class Profile
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? ProfileUrl { get; set; }
    
    public string? JobTitle { get; set; }
    
    public string? Department { get; set; }
    
    public string? Organization { get; set; }
    
    public string? Location { get; set; }

    // Navigation to User
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}