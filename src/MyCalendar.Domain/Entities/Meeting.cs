namespace MyCalendar.Domain.Entities;

public class Meeting
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<User> Participants { get; set; } = new List<User>();
} 