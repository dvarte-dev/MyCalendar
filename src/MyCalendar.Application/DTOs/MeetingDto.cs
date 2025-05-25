namespace MyCalendar.Application.DTOs;

public class MeetingDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<UserDto> Participants { get; set; } = new List<UserDto>();
} 