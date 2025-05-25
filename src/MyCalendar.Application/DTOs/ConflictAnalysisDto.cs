using System.ComponentModel.DataAnnotations;

namespace MyCalendar.Application.DTOs;

public class ConflictAnalysisRequestDto
{
    [Required]
    public List<Guid> ParticipantIds { get; set; } = new List<Guid>();
    
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int DurationMinutes { get; set; } = 60;
    
    public DateTime? MeetingStartTime { get; set; }
    public DateTime? MeetingEndTime { get; set; }
}

public class ConflictAnalysisResponseDto
{
    public bool HasConflicts { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<ParticipantAnalysisDto> Participants { get; set; } = new List<ParticipantAnalysisDto>();
    public WorkingHoursOverlapDto WorkingHoursOverlap { get; set; } = new WorkingHoursOverlapDto();
    public List<ConflictingMeetingDto> ConflictingMeetings { get; set; } = new List<ConflictingMeetingDto>();
    public List<SuggestedTimeSlotDto> SuggestedTimeSlots { get; set; } = new List<SuggestedTimeSlotDto>();
}

public class ParticipantAnalysisDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string LocalWorkingHours { get; set; } = string.Empty; 
    public string UtcWorkingHours { get; set; } = string.Empty;
    public int TotalMeetings { get; set; }
}

public class WorkingHoursOverlapDto
{
    public bool HasOverlap { get; set; }
    public string OverlapPeriod { get; set; } = string.Empty; 
    public string OverlapDuration { get; set; } = string.Empty; 
    public List<string> ParticipantLocalTimes { get; set; } = new List<string>();
}

public class ConflictingMeetingDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<string> ConflictingParticipants { get; set; } = new List<string>();
}

public class SuggestedTimeSlotDto
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public string UtcTimeRange { get; set; } = string.Empty;
    public List<ParticipantLocalTimeDto> ParticipantLocalTimes { get; set; } = new List<ParticipantLocalTimeDto>();
    public string Recommendation { get; set; } = string.Empty; 
}

public class ParticipantLocalTimeDto
{
    public string Name { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string LocalTimeRange { get; set; } = string.Empty;
} 