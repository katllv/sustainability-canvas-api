using System.ComponentModel.DataAnnotations;

namespace SustainabilityCanvas.Api.Models;

public class Profile
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? ProfileUrl { get; set; }

    // Navigation to User
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}