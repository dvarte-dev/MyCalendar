using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyCalendar.Application.DTOs;
using MyCalendar.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;

namespace MyCalendar.UnitTests;

/// <summary>
/// Integration tests for scheduling API endpoints
/// </summary>
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly ApplicationDbContext _context;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            
            builder.ConfigureServices(services =>
            {
                var infrastructureServices = services.Where(d => 
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(ApplicationDbContext) ||
                    (d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)))
                    .ToArray();

                foreach (var service in infrastructureServices)
                {
                    services.Remove(service);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb_Shared"));
            });
        });

        _client = _factory.CreateClient();
        _scope = _factory.Services.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _client.Dispose();
    }

    [Fact]
    public async Task ScheduleMeeting_ThroughAPI_CompleteFlow_ShouldWork()
    {
        var user1Request = new CreateUserRequestDto
        {
            Name = "João Silva",
            TimeZone = "UTC-3:00"
        };

        var user2Request = new CreateUserRequestDto
        {
            Name = "Maria Santos", 
            TimeZone = "UTC-5:00"
        };

        var user1Response = await _client.PostAsJsonAsync("/api/Users", user1Request);
        var user2Response = await _client.PostAsJsonAsync("/api/Users", user2Request);

        user1Response.StatusCode.Should().Be(HttpStatusCode.Created);
        user2Response.StatusCode.Should().Be(HttpStatusCode.Created);

        var user1 = await user1Response.Content.ReadFromJsonAsync<UserDto>();
        var user2 = await user2Response.Content.ReadFromJsonAsync<UserDto>();

        user1.Should().NotBeNull();
        user2.Should().NotBeNull();
        user1!.Name.Should().Be("João Silva");
        user2!.Name.Should().Be("Maria Santos");

        var meetingRequest = new ScheduleMeetingRequestDto
        {
            Title = "Integration Meeting",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(13),
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
            ParticipantIds = new List<Guid> { user1.Id, user2.Id }
        };

        var meetingResponse = await _client.PostAsJsonAsync("/api/Meetings/schedule", meetingRequest);
        meetingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meetingResult = await meetingResponse.Content.ReadFromJsonAsync<ScheduleMeetingResponseDto>();
        meetingResult.Should().NotBeNull();
        meetingResult!.IsSuccess.Should().BeTrue();
        meetingResult.ScheduledMeeting.Should().NotBeNull();
        meetingResult.ScheduledMeeting!.Title.Should().Be("Integration Meeting");
        meetingResult.ScheduledMeeting.Participants.Should().HaveCount(2);

        var conflictingMeetingRequest = new ScheduleMeetingRequestDto
        {
            Title = "Conflicting Meeting",
            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(13),
            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
            ParticipantIds = new List<Guid> { user1.Id }
        };

        var conflictResponse = await _client.PostAsJsonAsync("/api/Meetings/schedule", conflictingMeetingRequest);
        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var conflictResult = await conflictResponse.Content.ReadFromJsonAsync<ScheduleMeetingResponseDto>();
        conflictResult.Should().NotBeNull();
        conflictResult!.IsSuccess.Should().BeFalse();
        conflictResult.Message.Should().Contain("Time conflict detected");
        conflictResult.SuggestedTimeSlots.Should().NotBeEmpty();

        var allMeetingsResponse = await _client.GetAsync("/api/Meetings");
        allMeetingsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var allMeetings = await allMeetingsResponse.Content.ReadFromJsonAsync<List<MeetingDto>>();
        allMeetings.Should().NotBeNull();
        allMeetings!.Should().HaveCount(1);
        allMeetings.First().Title.Should().Be("Integration Meeting");

        var conflictAnalysisRequest = new ConflictAnalysisRequestDto
        {
            ParticipantIds = new List<Guid> { user1.Id, user2.Id },
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(7),
            DurationMinutes = 60
        };

        var analysisResponse = await _client.PostAsJsonAsync("/api/Meetings/analyze-conflicts", conflictAnalysisRequest);
        analysisResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var analysisResult = await analysisResponse.Content.ReadFromJsonAsync<ConflictAnalysisResponseDto>();
        analysisResult.Should().NotBeNull();
        analysisResult!.Participants.Should().HaveCount(2);
        analysisResult.WorkingHoursOverlap.Should().NotBeNull();
        analysisResult.Summary.Should().NotBeEmpty();

        var availableSlotsUrl = $"/api/Meetings/available-slots?participantIds={user1.Id}&participantIds={user2.Id}&startDate={DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}&endDate={DateTime.UtcNow.AddDays(7):yyyy-MM-ddTHH:mm:ss.fffZ}&durationMinutes=60";
        
        var slotsResponse = await _client.GetAsync(availableSlotsUrl);
        slotsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var availableSlots = await slotsResponse.Content.ReadFromJsonAsync<List<AvailableTimeSlotDto>>();
        availableSlots.Should().NotBeNull();
        availableSlots!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ScheduleMeeting_NonExistentUser_ThroughAPI_ShouldReturnError()
    {
        var invalidRequest = new ScheduleMeetingRequestDto
        {
            Title = "Meeting with Ghost User",
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
            ParticipantIds = new List<Guid> { Guid.NewGuid() }
        };

        var response = await _client.PostAsJsonAsync("/api/Meetings/schedule", invalidRequest);
        
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<ScheduleMeetingResponseDto>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }
    }

    private async Task<UserDto> CreateUserViaAPI(string name, string timeZone)
    {
        var request = new CreateUserRequestDto
        {
            Name = name,
            TimeZone = timeZone
        };

        var response = await _client.PostAsJsonAsync("/api/Users", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        user.Should().NotBeNull();
        return user!;
    }
} 