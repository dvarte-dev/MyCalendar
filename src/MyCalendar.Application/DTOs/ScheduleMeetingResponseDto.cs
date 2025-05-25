namespace MyCalendar.Application.DTOs;

public class ScheduleMeetingResponseDto
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public MeetingDto? ScheduledMeeting { get; set; }
    public List<AvailableTimeSlotDto> SuggestedTimeSlots { get; set; } = new List<AvailableTimeSlotDto>();
}

public class AvailableTimeSlotDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
} 