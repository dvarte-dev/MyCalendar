using System.ComponentModel.DataAnnotations;

namespace MyCalendar.Application.DTOs;

public class CreateUserRequestDto
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(100, ErrorMessage = "TimeZone cannot exceed 100 characters")]
    public string TimeZone { get; set; } = "UTC";
}