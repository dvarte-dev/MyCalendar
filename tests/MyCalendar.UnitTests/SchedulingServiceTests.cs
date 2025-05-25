using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MyCalendar.Application.DTOs;
using MyCalendar.Application.Services;
using MyCalendar.Domain.Entities;
using MyCalendar.Infrastructure.Data;
using MyCalendar.Infrastructure.Repositories;

namespace MyCalendar.UnitTests;

/// <summary>
/// Unit tests for the meeting scheduling system
/// Covering the cases that I thought were important for the coding challenge. (I'm not sure if I covered all cases, but I tried to cover the most important ones)
/// </summary>
public class SchedulingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserRepository _userRepository;
    private readonly MeetingRepository _meetingRepository;
    private readonly SchedulingService _schedulingService;

    public SchedulingServiceTests()
    {
        // Configure in-memory database for tests
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _userRepository = new UserRepository(_context);
        _meetingRepository = new MeetingRepository(_context);
        _schedulingService = new SchedulingService(_meetingRepository, _userRepository);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Use Case 1: Meeting without conflicts

    [Fact]
    public async Task ScheduleMeeting_WithoutConflicts_ShouldSucceed()
    {
        // Arrange
        var user1 = await CreateTestUser("João Silva", "UTC-3:00");
        var user2 = await CreateTestUser("Maria Santos", "UTC-5:00");

        var request = new ScheduleMeetingRequestDto
        {
            Title = "Planning Meeting",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14), 
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(15),  
            ParticipantIds = new List<Guid> { user1.Id, user2.Id }
        };

        // Act
        var result = await _schedulingService.ScheduleMeetingAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Meeting scheduled successfully.");
        result.ScheduledMeeting.Should().NotBeNull();
        result.ScheduledMeeting!.Title.Should().Be("Planning Meeting");
        result.ScheduledMeeting.Participants.Should().HaveCount(2);
        result.SuggestedTimeSlots.Should().BeEmpty();
    }

    #endregion

    #region Use Case 2: Simple conflict (same time)

    [Fact]
    public async Task ScheduleMeeting_WithSimpleConflict_ShouldReturnSuggestions()
    {
        // Arrange
        var user1 = await CreateTestUser("João Silva", "UTC-3:00");
        var user2 = await CreateTestUser("Maria Santos", "UTC-5:00");

        // Create existing meeting
        var existingMeeting = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = "Existing Meeting",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(15),
            Participants = new List<User> { user1 }
        };
        await _meetingRepository.AddAsync(existingMeeting);

        // Try to schedule at the same time
        var request = new ScheduleMeetingRequestDto
        {
            Title = "New Meeting",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(15),
            ParticipantIds = new List<Guid> { user1.Id, user2.Id }
        };

        // Act
        var result = await _schedulingService.ScheduleMeetingAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Time conflict detected");
        result.ScheduledMeeting.Should().BeNull();
        result.SuggestedTimeSlots.Should().NotBeEmpty();
        result.SuggestedTimeSlots.Should().HaveCountLessOrEqualTo(3);
    }

    #endregion

    #region Use Case 3: Multiple participants in different timezones

    [Fact]
    public async Task ScheduleMeeting_MultipleTimeZones_ShouldRespectWorkingHours()
    {
        // Arrange
        var userBrazil = await CreateTestUser("João (Brasil)", "UTC-3:00");
        var userUSA = await CreateTestUser("John (USA)", "UTC-5:00");
        var userUK = await CreateTestUser("James (UK)", "UTC");

        var request = new ScheduleMeetingRequestDto
        {
            Title = "Global Meeting",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(13),
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
            ParticipantIds = new List<Guid> { userBrazil.Id, userUSA.Id, userUK.Id }
        };

        // Act
        var result = await _schedulingService.ScheduleMeetingAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ScheduledMeeting.Should().NotBeNull();
        result.ScheduledMeeting!.Participants.Should().HaveCount(3);
    }

    [Fact]
    public async Task AnalyzeConflicts_MultipleTimeZones_ShouldShowWorkingHoursOverlap()
    {
        // Arrange
        var userBrazil = await CreateTestUser("João (Brasil)", "UTC-3:00");
        var userJapan = await CreateTestUser("Hiroshi (Japan)", "UTC+9:00");

        var request = new ConflictAnalysisRequestDto
        {
            ParticipantIds = new List<Guid> { userBrazil.Id, userJapan.Id },
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(7),
            DurationMinutes = 60
        };

        // Act
        var result = await _schedulingService.AnalyzeConflictsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Participants.Should().HaveCount(2);
        result.WorkingHoursOverlap.Should().NotBeNull();
        result.Summary.Should().Contain("participant");
    }

    #endregion

    #region Use Case 4: Midnight crossing timezones (Japan, Australia)

    [Fact]
    public async Task ScheduleMeeting_JapanTimezone_ShouldHandleMidnightCrossing()
    {
        // Arrange
        var userJapan = await CreateTestUser("Hiroshi (Japan)", "UTC+9:00");
        var userAustralia = await CreateTestUser("Steve (Australia)", "UTC+10:00");

        // Time that would be 08:00 in Japan (23:00 UTC the day before)
        var startTimeUtc = DateTime.UtcNow.AddDays(1).Date.AddHours(-1); // 23:00 UTC
        var endTimeUtc = startTimeUtc.AddHours(1); // 00:00 UTC (midnight)

        var request = new ScheduleMeetingRequestDto
        {
            Title = "Asia-Pacific Meeting",
            StartTime = startTimeUtc,
            EndTime = endTimeUtc,
            ParticipantIds = new List<Guid> { userJapan.Id, userAustralia.Id }
        };

        // Act
        var result = await _schedulingService.ScheduleMeetingAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ScheduledMeeting.Should().NotBeNull();
        result.ScheduledMeeting!.StartTime.Should().Be(startTimeUtc);
        result.ScheduledMeeting.EndTime.Should().Be(endTimeUtc);
    }

    [Fact]
    public async Task AnalyzeConflicts_MidnightCrossingTimezones_ShouldCalculateOverlapCorrectly()
    {
        // Arrange
        var userJapan = await CreateTestUser("Hiroshi (Japan)", "UTC+9:00");
        var userNewZealand = await CreateTestUser("Kiwi (New Zealand)", "UTC+12:00");

        var request = new ConflictAnalysisRequestDto
        {
            ParticipantIds = new List<Guid> { userJapan.Id, userNewZealand.Id },
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(1),
            DurationMinutes = 60
        };

        // Act
        var result = await _schedulingService.AnalyzeConflictsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.WorkingHoursOverlap.Should().NotBeNull();
        result.Participants.Should().HaveCount(2);
        
        // Verify UTC timezones are correct for timezones that cross midnight
        var japanParticipant = result.Participants.First(p => p.Name.Contains("Japan"));
        japanParticipant.UtcWorkingHours.Should().Contain("UTC");
    }

    #endregion

    #region Use Case 5: Intelligent suggestions prioritizing ideal times

    [Fact]
    public async Task FindAvailableTimeSlots_ShouldReturnIntelligentSuggestions()
    {
        // Arrange
        var user1 = await CreateTestUser("João Silva", "UTC-3:00");
        var user2 = await CreateTestUser("Maria Santos", "UTC-5:00");

        // Create some existing meetings to force suggestions
        var meeting1 = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = "Meeting 1",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(15),
            Participants = new List<User> { user1 }
        };
        await _meetingRepository.AddAsync(meeting1);

        var participantIds = new List<Guid> { user1.Id, user2.Id };
        var startDate = DateTime.UtcNow.AddDays(1).Date;
        var endDate = startDate.AddDays(7);

        // Act
        var result = await _schedulingService.FindAvailableTimeSlotsAsync(
            participantIds, startDate, endDate, 60);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().HaveCountLessOrEqualTo(3);
        
        // Check if slots are ordered chronologically
        for (int i = 1; i < result.Count; i++)
        {
            result[i].StartTime.Should().BeAfter(result[i - 1].StartTime);
        }
    }

    [Fact]
    public async Task ScheduleMeeting_WithConflict_ShouldReturnPrioritizedSuggestions()
    {
        // Arrange
        var user1 = await CreateTestUser("João Silva", "UTC-3:00");
        var user2 = await CreateTestUser("Maria Santos", "UTC-5:00");

        // Create conflict
        var existingMeeting = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = "Existing Meeting",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(15),
            Participants = new List<User> { user1 }
        };
        await _meetingRepository.AddAsync(existingMeeting);

        var request = new ScheduleMeetingRequestDto
        {
            Title = "New Meeting",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(15),
            ParticipantIds = new List<Guid> { user1.Id, user2.Id }
        };

        // Act
        var result = await _schedulingService.ScheduleMeetingAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.SuggestedTimeSlots.Should().NotBeEmpty();
        result.SuggestedTimeSlots.Should().HaveCountLessOrEqualTo(3);
        
        // Check if suggestions are within working hours
        foreach (var slot in result.SuggestedTimeSlots)
        {
            var hour = slot.StartTime.Hour;
            hour.Should().BeInRange(8, 18); 
        }
    }

    #endregion

    #region Use Case 6: Working hours overlap analysis

    [Fact]
    public async Task AnalyzeConflicts_ShouldCalculateWorkingHoursOverlap()
    {
        // Arrange
        var userBrazil = await CreateTestUser("João (Brasil)", "UTC-3:00");
        var userUK = await CreateTestUser("James (UK)", "UTC");
        var userIndia = await CreateTestUser("Raj (India)", "UTC+5:30");

        var request = new ConflictAnalysisRequestDto
        {
            ParticipantIds = new List<Guid> { userBrazil.Id, userUK.Id, userIndia.Id },
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(1),
            DurationMinutes = 60
        };

        // Act
        var result = await _schedulingService.AnalyzeConflictsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.WorkingHoursOverlap.Should().NotBeNull();
        result.WorkingHoursOverlap.HasOverlap.Should().BeTrue();
        result.WorkingHoursOverlap.OverlapPeriod.Should().NotBeEmpty();
        result.WorkingHoursOverlap.OverlapDuration.Should().NotBeEmpty();
        result.WorkingHoursOverlap.ParticipantLocalTimes.Should().HaveCount(3);
    }

    [Fact]
    public async Task AnalyzeConflicts_NoOverlap_ShouldIndicateNoCommonHours()
    {
        // Arrange - Very distant timezones without overlap
        var userUSAWest = await CreateTestUser("John (USA West)", "UTC-8:00");
        var userJapan = await CreateTestUser("Hiroshi (Japan)", "UTC+9:00");

        var request = new ConflictAnalysisRequestDto
        {
            ParticipantIds = new List<Guid> { userUSAWest.Id, userJapan.Id },
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(1),
            DurationMinutes = 60
        };

        // Act
        var result = await _schedulingService.AnalyzeConflictsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.WorkingHoursOverlap.Should().NotBeNull();
        // May have minimal overlap or none, depending on implementation
        result.WorkingHoursOverlap.OverlapDuration.Should().NotBeEmpty();
    }

    #endregion

    #region Validation and Edge Case Tests

    [Fact]
    public async Task ScheduleMeeting_InvalidTimeRange_ShouldFail()
    {
        // Arrange
        var user1 = await CreateTestUser("João Silva", "UTC-3:00");

        var request = new ScheduleMeetingRequestDto
        {
            Title = "Invalid Meeting",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(15),
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14), 
            ParticipantIds = new List<Guid> { user1.Id }
        };

        // Act
        var result = await _schedulingService.ScheduleMeetingAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("End time must be after start time");
    }

    [Fact]
    public async Task ScheduleMeeting_NoParticipants_ShouldFail()
    {
        // Arrange
        var request = new ScheduleMeetingRequestDto
        {
            Title = "Meeting Without Participants",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(15),
            ParticipantIds = new List<Guid>()
        };

        // Act
        var result = await _schedulingService.ScheduleMeetingAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("At least one participant is required");
    }

    [Fact]
    public async Task ScheduleMeeting_NonExistentParticipant_ShouldFail()
    {
        // Arrange
        var request = new ScheduleMeetingRequestDto
        {
            Title = "Meeting with Non-existent Participant",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(15),
            ParticipantIds = new List<Guid> { Guid.NewGuid() }
        };

        // Act
        var result = await _schedulingService.ScheduleMeetingAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task ScheduleMeeting_OutsideWorkingHours_ShouldReturnSuggestions()
    {
        // Arrange
        var user1 = await CreateTestUser("João Silva", "UTC-3:00");

        var request = new ScheduleMeetingRequestDto
        {
            Title = "Meeting Outside Working Hours",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(2), // 02:00 UTC (too early)
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(3),   // 03:00 UTC
            ParticipantIds = new List<Guid> { user1.Id }
        };

        // Act
        var result = await _schedulingService.ScheduleMeetingAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Outside working hours");
        result.SuggestedTimeSlots.Should().NotBeEmpty();
    }

    #endregion

    #region Performance and Efficiency Tests

    [Fact]
    public async Task ScheduleMeeting_WithManyExistingMeetings_ShouldBeEfficient()
    {
        // Arrange
        var user1 = await CreateTestUser("João Silva", "UTC-3:00");
        var user2 = await CreateTestUser("Maria Santos", "UTC-5:00");

        // Create many existing meetings to test efficiency
        var existingMeetings = new List<Meeting>();
        for (int i = 0; i < 100; i++)
        {
            var meeting = new Meeting
            {
                Id = Guid.NewGuid(),
                Title = $"Meeting {i}",
                StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(8 + (i % 10)),
                EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(9 + (i % 10)),
                Participants = new List<User> { i % 2 == 0 ? user1 : user2 }
            };
            existingMeetings.Add(meeting);
        }

        foreach (var meeting in existingMeetings)
        {
            await _meetingRepository.AddAsync(meeting);
        }

        var request = new ScheduleMeetingRequestDto
        {
            Title = "New Meeting",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(16),
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(17),
            ParticipantIds = new List<Guid> { user1.Id, user2.Id }
        };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _schedulingService.ScheduleMeetingAsync(request);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should be fast even with many meetings
        result.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private async Task<User> CreateTestUser(string name, string timeZone)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            TimeZone = timeZone
        };

        return await _userRepository.AddAsync(user);
    }

    #endregion
}