using MyCalendar.Application.DTOs;

namespace MyCalendar.Application.Interfaces;

public interface ISchedulingService
{
    Task<ScheduleMeetingResponseDto> ScheduleMeetingAsync(ScheduleMeetingRequestDto request);
    Task<List<AvailableTimeSlotDto>> FindAvailableTimeSlotsAsync(List<Guid> participantIds, DateTime startDate, DateTime endDate, int durationMinutes);
    Task<IEnumerable<MeetingDto>> GetAllMeetingsAsync();
    Task<bool> DeleteMeetingAsync(Guid meetingId);
    Task<ConflictAnalysisResponseDto> AnalyzeConflictsAsync(ConflictAnalysisRequestDto request);
} 