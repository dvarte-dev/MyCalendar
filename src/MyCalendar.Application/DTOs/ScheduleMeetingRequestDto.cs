namespace MyCalendar.Application.DTOs;

public class ScheduleMeetingRequestDto
{
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<Guid> ParticipantIds { get; set; } = new List<Guid>();
} 