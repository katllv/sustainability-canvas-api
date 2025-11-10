using System.ComponentModel.DataAnnotations;

namespace SustainabilityCanvas.Api.Models;

public class Sdg
{
    [Key]
    public int Id { get; set; } // 1-17
    
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
}