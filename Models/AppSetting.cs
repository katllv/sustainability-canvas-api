using System.ComponentModel.DataAnnotations;

namespace SustainabilityCanvas.Api.Models;

public class AppSetting
{
    [Key]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}