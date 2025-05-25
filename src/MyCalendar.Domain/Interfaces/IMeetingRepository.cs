using MyCalendar.Domain.Entities;
namespace MyCalendar.Domain.Interfaces;



public interface IMeetingRepository
{
    Task<Meeting?> GetByIdAsync(Guid id);
    Task<IEnumerable<Meeting>> GetAllAsync();
    Task<Meeting> AddAsync(Meeting meeting);
    Task UpdateAsync(Meeting meeting);
    Task DeleteAsync(Guid id);
    Task<IEnumerable<Meeting>> GetMeetingsByUserIdAsync(Guid userId);
    Task<IEnumerable<Meeting>> GetMeetingsByTimeRangeAsync(DateTime start, DateTime end);
    Task<IEnumerable<Meeting>> GetOverlappingMeetingsAsync(DateTime start, DateTime end, List<Guid> participantIds);
} 