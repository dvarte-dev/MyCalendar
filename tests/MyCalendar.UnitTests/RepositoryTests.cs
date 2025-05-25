using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MyCalendar.Domain.Entities;
using MyCalendar.Infrastructure.Data;
using MyCalendar.Infrastructure.Repositories;

namespace MyCalendar.UnitTests;

/// <summary>
/// Unit tests for repositories
/// </summary>
public class RepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserRepository _userRepository;
    private readonly MeetingRepository _meetingRepository;

    public RepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _userRepository = new UserRepository(_context);
        _meetingRepository = new MeetingRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region UserRepository Tests

    [Fact]
    public async Task UserRepository_AddUser_ShouldPersistUser()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            TimeZone = "UTC-3:00"
        };

        // Act
        var result = await _userRepository.AddAsync(user);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
        result.Name.Should().Be("Test User");
        result.TimeZone.Should().Be("UTC-3:00");

        // Check if it was persisted
        var retrievedUser = await _userRepository.GetByIdAsync(user.Id);
        retrievedUser.Should().NotBeNull();
        retrievedUser!.Name.Should().Be("Test User");
    }

    [Fact]
    public async Task UserRepository_GetAllUsers_ShouldReturnAllUsers()
    {
        // Arrange
        var user1 = new User { Id = Guid.NewGuid(), Name = "User 1", TimeZone = "UTC" };
        var user2 = new User { Id = Guid.NewGuid(), Name = "User 2", TimeZone = "UTC+1:00" };

        await _userRepository.AddAsync(user1);
        await _userRepository.AddAsync(user2);

        // Act
        var result = await _userRepository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(u => u.Name == "User 1");
        result.Should().Contain(u => u.Name == "User 2");
    }

    [Fact]
    public async Task UserRepository_UpdateUser_ShouldModifyUser()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            TimeZone = "UTC"
        };

        await _userRepository.AddAsync(user);

        // Act
        user.Name = "Updated Name";
        user.TimeZone = "UTC-5:00";
        await _userRepository.UpdateAsync(user);

        // Assert
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.Name.Should().Be("Updated Name");
        updatedUser.TimeZone.Should().Be("UTC-5:00");
    }

    [Fact]
    public async Task UserRepository_DeleteUser_ShouldRemoveUser()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "User to Delete",
            TimeZone = "UTC"
        };

        await _userRepository.AddAsync(user);

        // Act
        await _userRepository.DeleteAsync(user.Id);

        // Assert
        var deletedUser = await _userRepository.GetByIdAsync(user.Id);
        deletedUser.Should().BeNull();
    }

    #endregion

    #region MeetingRepository Tests

    [Fact]
    public async Task MeetingRepository_AddMeeting_ShouldPersistMeeting()
    {
        // Arrange
        var user1 = new User { Id = Guid.NewGuid(), Name = "User 1", TimeZone = "UTC" };
        var user2 = new User { Id = Guid.NewGuid(), Name = "User 2", TimeZone = "UTC+1:00" };

        await _userRepository.AddAsync(user1);
        await _userRepository.AddAsync(user2);

        var meeting = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = "Test Meeting",
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
            Participants = new List<User> { user1, user2 }
        };

        // Act
        var result = await _meetingRepository.AddAsync(meeting);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(meeting.Id);
        result.Title.Should().Be("Test Meeting");
        result.Participants.Should().HaveCount(2);

        // Check if it was persisted with participants
        var retrievedMeeting = await _meetingRepository.GetByIdAsync(meeting.Id);
        retrievedMeeting.Should().NotBeNull();
        retrievedMeeting!.Participants.Should().HaveCount(2);
    }

    [Fact]
    public async Task MeetingRepository_GetMeetingsByUserId_ShouldReturnUserMeetings()
    {
        // Arrange
        var user1 = new User { Id = Guid.NewGuid(), Name = "User 1", TimeZone = "UTC" };
        var user2 = new User { Id = Guid.NewGuid(), Name = "User 2", TimeZone = "UTC+1:00" };

        await _userRepository.AddAsync(user1);
        await _userRepository.AddAsync(user2);

        var meeting1 = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = "Meeting 1",
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
            Participants = new List<User> { user1 }
        };

        var meeting2 = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = "Meeting 2",
            StartTime = DateTime.UtcNow.AddDays(2),
            EndTime = DateTime.UtcNow.AddDays(2).AddHours(1),
            Participants = new List<User> { user1, user2 }
        };

        var meeting3 = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = "Meeting 3",
            StartTime = DateTime.UtcNow.AddDays(3),
            EndTime = DateTime.UtcNow.AddDays(3).AddHours(1),
            Participants = new List<User> { user2 }
        };

        await _meetingRepository.AddAsync(meeting1);
        await _meetingRepository.AddAsync(meeting2);
        await _meetingRepository.AddAsync(meeting3);

        // Act
        var user1Meetings = await _meetingRepository.GetMeetingsByUserIdAsync(user1.Id);

        // Assert
        user1Meetings.Should().HaveCount(2);
        user1Meetings.Should().Contain(m => m.Title == "Meeting 1");
        user1Meetings.Should().Contain(m => m.Title == "Meeting 2");
        user1Meetings.Should().NotContain(m => m.Title == "Meeting 3");
    }

    [Fact]
    public async Task MeetingRepository_GetOverlappingMeetings_ShouldReturnConflictingMeetings()
    {
        // Arrange
        var user1 = new User { Id = Guid.NewGuid(), Name = "User 1", TimeZone = "UTC" };
        var user2 = new User { Id = Guid.NewGuid(), Name = "User 2", TimeZone = "UTC+1:00" };

        await _userRepository.AddAsync(user1);
        await _userRepository.AddAsync(user2);

        var baseTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14);

        // Meeting that overlaps
        var overlappingMeeting = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = "Overlapping Meeting",
            StartTime = baseTime,
            EndTime = baseTime.AddHours(1),
            Participants = new List<User> { user1 }
        };

        // Meeting that does not overlap
        var nonOverlappingMeeting = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = "Non-overlapping Meeting",
            StartTime = baseTime.AddHours(2),
            EndTime = baseTime.AddHours(3),
            Participants = new List<User> { user1 }
        };

        await _meetingRepository.AddAsync(overlappingMeeting);
        await _meetingRepository.AddAsync(nonOverlappingMeeting);

        // Act
        var overlappingMeetings = await _meetingRepository.GetOverlappingMeetingsAsync(
            baseTime.AddMinutes(30),
            baseTime.AddMinutes(90),
            new List<Guid> { user1.Id }
        );

        // Assert
        overlappingMeetings.Should().HaveCount(1);
        overlappingMeetings.First().Title.Should().Be("Overlapping Meeting");
    }

    [Fact]
    public async Task MeetingRepository_GetMeetingsByTimeRange_ShouldReturnMeetingsInRange()
    {
        // Arrange
        var user1 = new User { Id = Guid.NewGuid(), Name = "User 1", TimeZone = "UTC" };
        await _userRepository.AddAsync(user1);

        var baseTime = DateTime.UtcNow.AddDays(1).Date;

        var meetingInRange = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = "Meeting In Range",
            StartTime = baseTime.AddHours(10),
            EndTime = baseTime.AddHours(11),
            Participants = new List<User> { user1 }
        };

        var meetingOutOfRange = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = "Meeting Out Of Range",
            StartTime = baseTime.AddDays(2),
            EndTime = baseTime.AddDays(2).AddHours(1),
            Participants = new List<User> { user1 }
        };

        await _meetingRepository.AddAsync(meetingInRange);
        await _meetingRepository.AddAsync(meetingOutOfRange);

        // Act
        var meetingsInRange = await _meetingRepository.GetMeetingsByTimeRangeAsync(
            baseTime.AddHours(9),
            baseTime.AddHours(12)
        );

        // Assert
        meetingsInRange.Should().HaveCount(1);
        meetingsInRange.First().Title.Should().Be("Meeting In Range");
    }

    [Fact]
    public async Task MeetingRepository_DeleteMeeting_ShouldRemoveMeeting()
    {
        // Arrange
        var user1 = new User { Id = Guid.NewGuid(), Name = "User 1", TimeZone = "UTC" };
        await _userRepository.AddAsync(user1);

        var meeting = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = "Meeting to Delete",
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
            Participants = new List<User> { user1 }
        };

        await _meetingRepository.AddAsync(meeting);

        // Act
        await _meetingRepository.DeleteAsync(meeting.Id);

        // Assert
        var deletedMeeting = await _meetingRepository.GetByIdAsync(meeting.Id);
        deletedMeeting.Should().BeNull();
    }

    #endregion

    #region Conflict Detection Efficiency Tests

    [Fact]
    public async Task MeetingRepository_GetOverlappingMeetings_WithManyMeetings_ShouldBeEfficient()
    {
        // Arrange
        var user1 = new User { Id = Guid.NewGuid(), Name = "User 1", TimeZone = "UTC" };
        await _userRepository.AddAsync(user1);

        var baseTime = DateTime.UtcNow.AddDays(1).Date;

        // Create many meetings to test efficiency
        var meetings = new List<Meeting>();
        for (int i = 0; i < 1000; i++)
        {
            var meeting = new Meeting
            {
                Id = Guid.NewGuid(),
                Title = $"Meeting {i}",
                StartTime = baseTime.AddHours(i % 24),
                EndTime = baseTime.AddHours((i % 24) + 1),
                Participants = new List<User> { user1 }
            };
            meetings.Add(meeting);
        }

        foreach (var meeting in meetings)
        {
            await _meetingRepository.AddAsync(meeting);
        }

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var overlappingMeetings = await _meetingRepository.GetOverlappingMeetingsAsync(
            baseTime.AddHours(10),
            baseTime.AddHours(12),
            new List<Guid> { user1.Id }
        );
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
        overlappingMeetings.Should().NotBeEmpty();
    }

    #endregion
} 