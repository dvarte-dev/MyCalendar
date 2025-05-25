using Microsoft.EntityFrameworkCore;
using MyCalendar.Domain.Entities;
using MyCalendar.Domain.Interfaces;
using MyCalendar.Infrastructure.Data;

namespace MyCalendar.Infrastructure.Repositories;

public class MeetingRepository : IMeetingRepository
{
    private readonly ApplicationDbContext _context;

    public MeetingRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Meeting?> GetByIdAsync(Guid id)
    {
        return await _context.Meetings
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IEnumerable<Meeting>> GetAllAsync()
    {
        return await _context.Meetings
            .Include(m => m.Participants)
            .ToListAsync();
    }

    public async Task<Meeting> AddAsync(Meeting meeting)
    {
        await _context.Meetings.AddAsync(meeting);
        await _context.SaveChangesAsync();
        return meeting;
    }

    public async Task UpdateAsync(Meeting meeting)
    {
        _context.Meetings.Update(meeting);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var meeting = await GetByIdAsync(id);
        if (meeting != null)
        {
            _context.Meetings.Remove(meeting);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Meeting>> GetMeetingsByUserIdAsync(Guid userId)
    {
        return await _context.Meetings
            .Include(m => m.Participants)
            .Where(m => m.Participants.Any(p => p.Id == userId))
            .ToListAsync();
    }

    public async Task<IEnumerable<Meeting>> GetMeetingsByTimeRangeAsync(DateTime start, DateTime end)
    {
        return await _context.Meetings
            .Include(m => m.Participants)
            .Where(m => m.StartTime < end && m.EndTime > start) 
            .ToListAsync();
    }

    public async Task<IEnumerable<Meeting>> GetOverlappingMeetingsAsync(DateTime start, DateTime end, List<Guid> participantIds)
    {
        return await _context.Meetings
            .Include(m => m.Participants)
            .Where(m => 
                m.Participants.Any(p => participantIds.Contains(p.Id)) &&
                m.StartTime < end && m.EndTime > start) 
            .ToListAsync();
    }
}