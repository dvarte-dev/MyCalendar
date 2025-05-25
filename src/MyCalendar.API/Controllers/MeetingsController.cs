using Microsoft.AspNetCore.Mvc;
using MyCalendar.Application.DTOs;
using MyCalendar.Application.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace MyCalendar.API.Controllers;

/// <summary>
/// Controller for meeting management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MeetingsController : ControllerBase
{
    private readonly ISchedulingService _schedulingService;

    public MeetingsController(ISchedulingService schedulingService)
    {
        _schedulingService = schedulingService;
    }

    /// <summary>
    /// Get all scheduled meetings
    /// </summary>
    /// <returns>List of all meetings</returns>
    /// <response code="200">Meetings list returned successfully</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all meetings", 
        Description = "Returns all scheduled meetings in the system for calendar display."
    )]
    [SwaggerResponse(200, "Meetings list returned successfully", typeof(IEnumerable<MeetingDto>))]
    public async Task<IActionResult> GetMeetings()
    {
        var meetings = await _schedulingService.GetAllMeetingsAsync();
        return Ok(meetings);
    }

    /// <summary>
    /// Delete a specific meeting
    /// </summary>
    /// <param name="id">ID of the meeting to be deleted</param>
    /// <returns>Result of the deletion operation</returns>
    /// <response code="204">Meeting deleted successfully</response>
    /// <response code="404">Meeting not found</response>
    [HttpDelete("{id}")]
    [SwaggerOperation(
        Summary = "Delete meeting", 
        Description = "Removes a specific meeting from the system."
    )]
    [SwaggerResponse(204, "Meeting deleted successfully")]
    [SwaggerResponse(404, "Meeting not found")]
    public async Task<IActionResult> DeleteMeeting(Guid id)
    {
        var success = await _schedulingService.DeleteMeetingAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Schedule a new meeting with conflict detection
    /// </summary>
    /// <param name="request">Meeting data to be scheduled</param>
    /// <returns>Scheduling result, including suggestions in case of conflict</returns>
    /// <response code="200">Meeting scheduled successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="409">Time conflict detected (returns suggestions)</response>
    [HttpPost("schedule")]
    [SwaggerOperation(
        Summary = "Schedule meeting", 
        Description = "Schedules a new meeting with conflict detection. If there are conflicts, returns alternative time suggestions."
    )]
    [SwaggerResponse(200, "Meeting scheduled successfully", typeof(ScheduleMeetingResponseDto))]
    [SwaggerResponse(400, "Invalid request")]
    [SwaggerResponse(409, "Time conflict detected", typeof(ScheduleMeetingResponseDto))]
    public async Task<IActionResult> ScheduleMeeting([FromBody] ScheduleMeetingRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _schedulingService.ScheduleMeetingAsync(request);
        
        if (result.IsSuccess)
        {
            return Ok(result);
        }
        
        if (result.SuggestedTimeSlots.Any())
        {
            return StatusCode(409, result);
        }
        
        return BadRequest(result);
    }

    /// <summary>
    /// Find available time slots for a group of participants
    /// </summary>
    /// <param name="participantIds">Participant IDs</param>
    /// <param name="startDate">Search start date/time</param>
    /// <param name="endDate">Search end date/time</param>
    /// <param name="durationMinutes">Meeting duration in minutes</param>
    /// <returns>List of available slots</returns>
    /// <response code="200">Returns the list of available slots</response>
    /// <response code="400">If no participant is provided</response>
    [HttpGet("available-slots")]
    [SwaggerOperation(
        Summary = "Find available slots", 
        Description = "Searches for available time slots for a group of participants in a specific period"
    )]
    [SwaggerResponse(200, "List of available slots", typeof(List<AvailableTimeSlotDto>))]
    [SwaggerResponse(400, "Invalid request")]
    public async Task<IActionResult> FindAvailableSlots(
        [FromQuery] List<Guid> participantIds, 
        [FromQuery] DateTime startDate, 
        [FromQuery] DateTime endDate, 
        [FromQuery] int durationMinutes = 30)
    {
        if (!participantIds.Any())
        {
            return BadRequest("At least one participant is required.");
        }

        var availableSlots = await _schedulingService.FindAvailableTimeSlotsAsync(
            participantIds, 
            startDate, 
            endDate, 
            durationMinutes);
        
        return Ok(availableSlots);
    }

    /// <summary>
    /// Analyze conflicts and working hours overlap between participants
    /// </summary>
    /// <param name="request">Conflict analysis data</param>
    /// <returns>Detailed analysis including conflicts, working hours overlap and suggestions</returns>
    /// <response code="200">Conflict analysis returned successfully</response>
    /// <response code="400">Invalid request</response>
    [HttpPost("analyze-conflicts")]
    [SwaggerOperation(
        Summary = "Analyze conflicts and schedules", 
        Description = "Analyzes meeting conflicts and working hours overlap between participants, considering different time zones. Returns detailed analysis with ideal time suggestions."
    )]
    [SwaggerResponse(200, "Conflict analysis returned successfully", typeof(ConflictAnalysisResponseDto))]
    [SwaggerResponse(400, "Invalid request")]
    public async Task<IActionResult> AnalyzeConflicts([FromBody] ConflictAnalysisRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!request.ParticipantIds.Any())
        {
            return BadRequest("At least one participant is required for analysis.");
        }

        var result = await _schedulingService.AnalyzeConflictsAsync(request);
        return Ok(result);
    }
}