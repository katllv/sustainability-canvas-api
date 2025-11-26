using System.ComponentModel.DataAnnotations;

namespace SustainabilityCanvas.Api.Models;

public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; } = UserRole.User;

    // to get associated Profile
    public Profile Profile { get; set; } = null!;
}

public enum UserRole
{
    User = 0, // regular
    Admin = 1, // can delete projects, manage users
}