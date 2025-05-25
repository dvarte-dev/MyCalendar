using MyCalendar.Application.DTOs;
using MyCalendar.Application.Interfaces;
using MyCalendar.Domain.Entities;
using MyCalendar.Domain.Interfaces;

namespace MyCalendar.Application.Services;

public class SchedulingService : ISchedulingService
{
    private readonly IMeetingRepository _meetingRepository;
    private readonly IUserRepository _userRepository;

    public SchedulingService(
        IMeetingRepository meetingRepository,
        IUserRepository userRepository)
    {
        _meetingRepository = meetingRepository;
        _userRepository = userRepository;
    }

    public async Task<ScheduleMeetingResponseDto> ScheduleMeetingAsync(ScheduleMeetingRequestDto request)
    {
        var response = new ScheduleMeetingResponseDto();

        if (request.EndTime <= request.StartTime)
        {
            response.IsSuccess = false;
            response.Message = "End time must be after start time.";
            return response;
        }

        if (!request.ParticipantIds.Any())
        {
            response.IsSuccess = false;
            response.Message = "At least one participant is required.";
            return response;
        }

        var participants = new List<User>();
        foreach (var participantId in request.ParticipantIds)
        {
            var user = await _userRepository.GetByIdAsync(participantId);
            if (user == null)
            {
                response.IsSuccess = false;
                response.Message = $"Participant with ID {participantId} not found.";
                return response;
            }
            participants.Add(user);
        }

        var startTimeUtc = DateTime.SpecifyKind(request.StartTime, DateTimeKind.Utc);
        var endTimeUtc = DateTime.SpecifyKind(request.EndTime, DateTimeKind.Utc);

        var workingHoursValidation = ValidateWorkingHours(participants, startTimeUtc, endTimeUtc);
        if (!workingHoursValidation.IsValid)
        {
            response.IsSuccess = false;
            response.Message = workingHoursValidation.Message;
            
            var suggestedSlots = await FindAvailableTimeSlotsAsync(
                request.ParticipantIds, 
                startTimeUtc, 
                startTimeUtc.AddDays(7),
                (int)(endTimeUtc - startTimeUtc).TotalMinutes);
            
            response.SuggestedTimeSlots = suggestedSlots.Take(3).ToList();
            return response;
        }

        var conflictingMeetings = await _meetingRepository.GetOverlappingMeetingsAsync(
            startTimeUtc,
            endTimeUtc,
            request.ParticipantIds);

        if (conflictingMeetings.Any())
        {
            response.IsSuccess = false;
            response.Message = "Time conflict detected for one or more participants.";
            
            var suggestedSlots = await FindAvailableTimeSlotsAsync(
                request.ParticipantIds, 
                startTimeUtc, 
                startTimeUtc.AddDays(7),
                (int)(endTimeUtc - startTimeUtc).TotalMinutes);
            
            response.SuggestedTimeSlots = suggestedSlots.Take(3).ToList();
            return response;
        }

        var meeting = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = request.Title ?? "Untitled Meeting",
            StartTime = startTimeUtc,
            EndTime = endTimeUtc,
            Participants = participants
        };

        await _meetingRepository.AddAsync(meeting);

        response.IsSuccess = true;
        response.Message = "Meeting scheduled successfully.";
        response.ScheduledMeeting = MapToMeetingDto(meeting);

        return response;
    }

    public async Task<List<AvailableTimeSlotDto>> FindAvailableTimeSlotsAsync(
        List<Guid> participantIds, 
        DateTime startDate, 
        DateTime endDate, 
        int durationMinutes)
    {
        if (!participantIds.Any())
            return new List<AvailableTimeSlotDto>();

        var participants = new List<User>();
        foreach (var participantId in participantIds)
        {
            var user = await _userRepository.GetByIdAsync(participantId);
            if (user != null)
                participants.Add(user);
        }

        if (!participants.Any())
            return new List<AvailableTimeSlotDto>();

        var startDateUtc = startDate.ToUniversalTime();
        var endDateUtc = endDate.ToUniversalTime();

        var allMeetings = await _meetingRepository.GetOverlappingMeetingsAsync(
            startDateUtc, 
            endDateUtc, 
            participantIds);
        
        var orderedMeetings = allMeetings
            .Where(m => m.Participants.Any(p => participantIds.Contains(p.Id)))
            .OrderBy(m => m.StartTime)
            .ToList();
        
        var availableSlots = new List<AvailableTimeSlotDto>();
        var meetingDuration = TimeSpan.FromMinutes(durationMinutes);
        
        for (var day = startDateUtc.Date; day <= endDateUtc.Date && availableSlots.Count < 3; day = day.AddDays(1))
        {
            var dailySlots = FindAvailableSlotsForDay(participants, day, orderedMeetings, meetingDuration, startDateUtc, endDateUtc);
            
            foreach (var slot in dailySlots)
            {
                availableSlots.Add(slot);
                if (availableSlots.Count >= 3)
                    break;
            }
        }
        
        return availableSlots;
    }

    private List<AvailableTimeSlotDto> FindAvailableSlotsForDay(
        List<User> participants, 
        DateTime day, 
        List<Meeting> orderedMeetings, 
        TimeSpan meetingDuration,
        DateTime startDateUtc,
        DateTime endDateUtc)
    {
        var dailySlots = new List<AvailableTimeSlotDto>();
        
        var commonWorkingHours = GetCommonWorkingHours(participants, day);
        
        if (commonWorkingHours.start >= commonWorkingHours.end)
            return dailySlots;

        var dayStart = day.Add(commonWorkingHours.start);
        var dayEnd = day.Add(commonWorkingHours.end);
        
        if (day == startDateUtc.Date && startDateUtc > dayStart)
            dayStart = startDateUtc;
        
        if (day == endDateUtc.Date && endDateUtc < dayEnd)
            dayEnd = endDateUtc;
        
        if (dayEnd - dayStart < meetingDuration)
            return dailySlots;
        
        var dayMeetings = orderedMeetings
            .Where(m => m.StartTime.Date == day.Date && m.StartTime < dayEnd && m.EndTime > dayStart)
            .OrderBy(m => m.StartTime)
            .ToList();
        
        var currentTime = dayStart;
        
        foreach (var meeting in dayMeetings)
        {
            var meetingStart = meeting.StartTime < dayStart ? dayStart : meeting.StartTime;
            
            while (currentTime + meetingDuration <= meetingStart)
            {
                dailySlots.Add(new AvailableTimeSlotDto
                {
                    StartTime = currentTime,
                    EndTime = currentTime.Add(meetingDuration)
                });
                
                currentTime = currentTime.AddHours(1);
            }
            
            currentTime = meeting.EndTime > currentTime ? meeting.EndTime : currentTime;
        }
        
        while (currentTime + meetingDuration <= dayEnd)
        {
            dailySlots.Add(new AvailableTimeSlotDto
            {
                StartTime = currentTime,
                EndTime = currentTime.Add(meetingDuration)
            });
            
            currentTime = currentTime.AddHours(1);
        }
        
        return dailySlots;
    }

    public async Task<IEnumerable<MeetingDto>> GetAllMeetingsAsync()
    {
        var meetings = await _meetingRepository.GetAllAsync();
        return meetings.Select(MapToMeetingDto);
    }

    public async Task<bool> DeleteMeetingAsync(Guid meetingId)
    {
        var meeting = await _meetingRepository.GetByIdAsync(meetingId);
        if (meeting == null)
            return false;

        await _meetingRepository.DeleteAsync(meetingId);
        return true;
    }

    public async Task<ConflictAnalysisResponseDto> AnalyzeConflictsAsync(ConflictAnalysisRequestDto request)
    {
        var response = new ConflictAnalysisResponseDto();

        if (!request.ParticipantIds.Any())
        {
            response.Summary = "No participants selected for analysis.";
            return response;
        }

        var participants = new List<User>();
        foreach (var participantId in request.ParticipantIds)
        {
            var user = await _userRepository.GetByIdAsync(participantId);
            if (user != null)
                participants.Add(user);
        }

        if (!participants.Any())
        {
            response.Summary = "No valid participants found.";
            return response;
        }

        var startDate = request.StartDate?.ToUniversalTime() ?? DateTime.UtcNow;
        var endDate = request.EndDate?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(7);

        response.Participants = await AnalyzeParticipants(participants, startDate, endDate);
        response.WorkingHoursOverlap = AnalyzeWorkingHoursOverlap(participants);

        if (request.MeetingStartTime.HasValue && request.MeetingEndTime.HasValue)
        {
            var meetingStartUtc = request.MeetingStartTime.Value.ToUniversalTime();
            var meetingEndUtc = request.MeetingEndTime.Value.ToUniversalTime();
            
            // Check for existing meeting conflicts
            var conflictingMeetings = await _meetingRepository.GetOverlappingMeetingsAsync(
                meetingStartUtc, 
                meetingEndUtc, 
                request.ParticipantIds);
            
            response.ConflictingMeetings = conflictingMeetings.Select(m => new ConflictingMeetingDto
            {
                Id = m.Id,
                Title = m.Title,
                StartTime = m.StartTime,
                EndTime = m.EndTime,
                ConflictingParticipants = m.Participants
                    .Where(p => request.ParticipantIds.Contains(p.Id))
                    .Select(p => $"{p.Name} ({p.TimeZone})")
                    .ToList()
            }).ToList();
            
            // Check for working hours conflicts
            var workingHoursValidation = ValidateWorkingHours(participants, meetingStartUtc, meetingEndUtc);
            if (!workingHoursValidation.IsValid)
            {
                // Add a virtual "conflict" for working hours violation
                var meetingDate = meetingStartUtc.ToString("dd/MM/yyyy");
                var meetingTimeRange = $"{meetingStartUtc:HH:mm} - {meetingEndUtc:HH:mm}";
                
                response.ConflictingMeetings.Add(new ConflictingMeetingDto
                {
                    Id = Guid.Empty,
                    Title = $"Working Hours Conflict for {meetingTimeRange} on {meetingDate}",
                    StartTime = meetingStartUtc,
                    EndTime = meetingEndUtc,
                    ConflictingParticipants = new List<string> { workingHoursValidation.Message }
                });
            }
        }
        else
        {
            response.ConflictingMeetings = await FindConflictingMeetings(participants, startDate, endDate);
        }

        response.HasConflicts = response.ConflictingMeetings.Any();
        response.SuggestedTimeSlots = await GenerateIntelligentSuggestions(participants, startDate, endDate, request.DurationMinutes);
        response.Summary = GenerateAnalysisSummary(response, participants);

        return response;
    }

    private async Task<List<ParticipantAnalysisDto>> AnalyzeParticipants(List<User> participants, DateTime startDate, DateTime endDate)
    {
        var result = new List<ParticipantAnalysisDto>();

        foreach (var participant in participants)
        {
            var workingHours = GetWorkingHoursInUtc(participant, startDate.Date);
            var meetings = await _meetingRepository.GetMeetingsByUserIdAsync(participant.Id);
            var meetingsInPeriod = meetings.Where(m => m.StartTime >= startDate && m.EndTime <= endDate).Count();

            var localStartTime = ConvertUtcToUserLocalTime(startDate.Date.Add(new TimeSpan(8, 0, 0)), participant.TimeZone);
            var localEndTime = ConvertUtcToUserLocalTime(startDate.Date.Add(new TimeSpan(18, 0, 0)), participant.TimeZone);

            string utcWorkingHoursDisplay;
            if (workingHours.crossesMidnight)
            {
                utcWorkingHoursDisplay = $"{workingHours.start:hh\\:mm} - 00:00 + 00:00 - {workingHours.end:hh\\:mm} UTC (crosses midnight)";
            }
            else
            {
                utcWorkingHoursDisplay = $"{workingHours.start:hh\\:mm} - {workingHours.end:hh\\:mm} UTC";
            }

            result.Add(new ParticipantAnalysisDto
            {
                Id = participant.Id,
                Name = participant.Name,
                TimeZone = participant.TimeZone,
                LocalWorkingHours = "08:00 - 18:00",
                UtcWorkingHours = utcWorkingHoursDisplay,
                TotalMeetings = meetingsInPeriod
            });
        }

        return result;
    }

    private WorkingHoursOverlapDto AnalyzeWorkingHoursOverlap(List<User> participants)
    {
        var result = new WorkingHoursOverlapDto();
        var today = DateTime.UtcNow.Date;

        if (participants.Count < 2)
        {
            result.HasOverlap = true;
            result.OverlapPeriod = "08:00 - 18:00 UTC";
            result.OverlapDuration = "10 hours";
            return result;
        }

        var commonHours = GetCommonWorkingHours(participants, today);
        
        if (commonHours.start >= commonHours.end)
        {
            result.HasOverlap = false;
            result.OverlapPeriod = "No overlap";
            result.OverlapDuration = "0 hours";
            result.ParticipantLocalTimes.Add("❌ No common working hours between participants");
        }
        else
        {
            result.HasOverlap = true;
            result.OverlapPeriod = $"{commonHours.start:hh\\:mm} - {commonHours.end:hh\\:mm} UTC";
            
            var duration = commonHours.end - commonHours.start;
            if (duration.Minutes > 0)
            {
                result.OverlapDuration = $"{duration.Hours}:{duration.Minutes:D1} hours";
            }
            else
            {
                result.OverlapDuration = $"{duration.TotalHours:F0} hours";
            }

            foreach (var participant in participants)
            {
                var localStart = ConvertUtcToUserLocalTime(today.Add(commonHours.start), participant.TimeZone);
                var localEnd = ConvertUtcToUserLocalTime(today.Add(commonHours.end), participant.TimeZone);
                
                result.ParticipantLocalTimes.Add(
                    $"{participant.Name} ({participant.TimeZone}): {localStart:HH:mm} - {localEnd:HH:mm}"
                );
            }
        }

        return result;
    }

    private async Task<List<ConflictingMeetingDto>> FindConflictingMeetings(List<User> participants, DateTime startDate, DateTime endDate)
    {
        var result = new List<ConflictingMeetingDto>();
        var participantIds = participants.Select(p => p.Id).ToList();

        var allMeetings = await _meetingRepository.GetOverlappingMeetingsAsync(startDate, endDate, participantIds);

        var conflictGroups = new List<List<Meeting>>();

        foreach (var meeting in allMeetings.OrderBy(m => m.StartTime))
        {
            var overlappingMeetings = allMeetings
                .Where(m => m.Id != meeting.Id && 
                           m.StartTime < meeting.EndTime && 
                           m.EndTime > meeting.StartTime &&
                           m.Participants.Any(p => meeting.Participants.Any(mp => mp.Id == p.Id)))
                .ToList();

            if (overlappingMeetings.Any())
            {
                var conflictGroup = new List<Meeting> { meeting };
                conflictGroup.AddRange(overlappingMeetings);
                
                if (!conflictGroups.Any(cg => cg.Any(m => conflictGroup.Any(cm => cm.Id == m.Id))))
                {
                    conflictGroups.Add(conflictGroup);
                }
            }
        }

        foreach (var group in conflictGroups)
        {
            foreach (var meeting in group)
            {
                var conflictingParticipants = meeting.Participants
                    .Where(p => participantIds.Contains(p.Id))
                    .Select(p => $"{p.Name} ({p.TimeZone})")
                    .ToList();

                if (!result.Any(r => r.Id == meeting.Id))
                {
                    result.Add(new ConflictingMeetingDto
                    {
                        Id = meeting.Id,
                        Title = meeting.Title,
                        StartTime = meeting.StartTime,
                        EndTime = meeting.EndTime,
                        ConflictingParticipants = conflictingParticipants
                    });
                }
            }
        }

        return result;
    }

    private async Task<List<SuggestedTimeSlotDto>> GenerateIntelligentSuggestions(List<User> participants, DateTime startDate, DateTime endDate, int durationMinutes)
    {
        var suggestions = new List<SuggestedTimeSlotDto>();
        var participantIds = participants.Select(p => p.Id).ToList();

        var availableSlots = await FindAvailableTimeSlotsAsync(participantIds, startDate, endDate, durationMinutes);

        foreach (var slot in availableSlots)
        {
            var suggestion = new SuggestedTimeSlotDto
            {
                StartTimeUtc = slot.StartTime,
                EndTimeUtc = slot.EndTime,
                UtcTimeRange = $"{slot.StartTime:HH:mm} - {slot.EndTime:HH:mm} UTC - {slot.StartTime:dd/MM/yyyy}"
            };

            foreach (var participant in participants)
            {
                var localStart = ConvertUtcToUserLocalTime(slot.StartTime, participant.TimeZone);
                var localEnd = ConvertUtcToUserLocalTime(slot.EndTime, participant.TimeZone);

                suggestion.ParticipantLocalTimes.Add(new ParticipantLocalTimeDto
                {
                    Name = participant.Name,
                    TimeZone = participant.TimeZone,
                    LocalTimeRange = $"{localStart:HH:mm} - {localEnd:HH:mm}"
                });
            }

            suggestion.Recommendation = DetermineRecommendation(slot.StartTime, participants);
            suggestions.Add(suggestion);
        }

        var orderedSuggestions = suggestions
            .OrderByDescending(s => GetRecommendationScore(s.Recommendation))
            .ThenBy(s => s.StartTimeUtc)
            .Take(3)
            .ToList();

        return orderedSuggestions;
    }

    private int GetRecommendationScore(string recommendation)
    {
        return recommendation switch
        {
            "🎯 Ideal" => 4,
            "✅ Good" => 3,
            "⚠️ Acceptable" => 2,
            "❌ Not recommended" => 1,
            _ => 0
        };
    }

    private string DetermineRecommendation(DateTime startTimeUtc, List<User> participants)
    {
        var scores = new List<int>();

        foreach (var participant in participants)
        {
            var localTime = ConvertUtcToUserLocalTime(startTimeUtc, participant.TimeZone);
            var hour = localTime.Hour;
            var minute = localTime.Minute;
            var totalMinutes = hour * 60 + minute;

            int score = 0;
            
            if ((hour >= 10 && hour <= 11) || (hour >= 14 && hour <= 15))
            {
                score = 4;
            }
            else if ((hour >= 9 && hour <= 9) || (hour >= 12 && hour <= 13) || (hour >= 16 && hour <= 16))
            {
                score = 3;
            }
            else if ((hour >= 8 && hour <= 8) || (hour >= 17 && hour <= 17))
            {
                score = 2;
            }
            else if (hour >= 8 && hour <= 18)
            {
                score = 1;
            }
            else
            {
                score = 0;
            }
            
            scores.Add(score);
        }

        var averageScore = scores.Average();

        if (averageScore >= 3.5) return "🎯 Ideal";
        else if (averageScore >= 2.5) return "✅ Good";
        else if (averageScore >= 1.5) return "⚠️ Acceptable";
        else return "❌ Not recommended";
    }

    private string GenerateAnalysisSummary(ConflictAnalysisResponseDto response, List<User> participants)
    {
        var summary = new List<string>();

        summary.Add($"📊 Analysis for {participants.Count} participant(s):");
        foreach (var p in response.Participants)
        {
            summary.Add($"   • {p.Name} ({p.TimeZone}): {p.UtcWorkingHours}");
        }

        if (response.WorkingHoursOverlap.HasOverlap)
        {
            summary.Add($"\n✅ Overlap window: {response.WorkingHoursOverlap.OverlapPeriod} ({response.WorkingHoursOverlap.OverlapDuration})");
            foreach (var localTime in response.WorkingHoursOverlap.ParticipantLocalTimes)
            {
                summary.Add($"   • {localTime}");
            }
        }
        else
        {
            summary.Add("\n❌ No working hours overlap");
        }

        if (response.HasConflicts)
        {
            var workingHoursConflicts = response.ConflictingMeetings.Where(m => m.Title.Contains("Working Hours Conflict")).ToList();
            var meetingConflicts = response.ConflictingMeetings.Where(m => !m.Title.Contains("Working Hours Conflict")).ToList();
            
            if (workingHoursConflicts.Any() && meetingConflicts.Any())
            {
                summary.Add($"\n⚠️ {workingHoursConflicts.Count} working hours conflict(s) and {meetingConflicts.Count} meeting conflict(s) found");
            }
            else if (workingHoursConflicts.Any())
            {
                var conflict = workingHoursConflicts.First();
                var meetingDate = conflict.StartTime.ToString("dd/MM/yyyy");
                var meetingTimeRange = $"{conflict.StartTime:HH:mm} - {conflict.EndTime:HH:mm}";
                summary.Add($"\n⚠️ Working hours conflict for {meetingTimeRange} on {meetingDate}");
            }
            else
            {
                summary.Add($"\n⚠️ {meetingConflicts.Count} meeting conflict(s) found");
            }
        }
        else
        {
            summary.Add("\n✅ No conflicts detected");
        }

        if (response.SuggestedTimeSlots.Any())
        {
            summary.Add($"\n🎯 {response.SuggestedTimeSlots.Count} suggested time slot(s):");
            foreach (var suggestion in response.SuggestedTimeSlots)
            {
                summary.Add($"   • {suggestion.UtcTimeRange} - {suggestion.Recommendation}");
            }
        }

        return string.Join("\n", summary);
    }

    private (bool IsValid, string Message) ValidateWorkingHours(List<User> participants, DateTime startTimeUtc, DateTime endTimeUtc)
    {
        var invalidParticipants = new List<string>();

        foreach (var participant in participants)
        {
            var participantWorkingHours = GetWorkingHoursInUtc(participant, startTimeUtc.Date);
            
            var meetingStartTime = startTimeUtc.TimeOfDay;
            var meetingEndTime = endTimeUtc.TimeOfDay;
            
            if (endTimeUtc.Date > startTimeUtc.Date)
                meetingEndTime = new TimeSpan(23, 59, 59);

            bool isValidTime;
            if (participantWorkingHours.crossesMidnight)
            {
                isValidTime = (meetingStartTime >= participantWorkingHours.start) || 
                             (meetingEndTime <= participantWorkingHours.end);
            }
            else
            {
                isValidTime = meetingStartTime >= participantWorkingHours.start && 
                             meetingEndTime <= participantWorkingHours.end;
            }

            if (!isValidTime)
            {
                var localStartTime = ConvertUtcToUserLocalTime(startTimeUtc, participant.TimeZone);
                var localEndTime = ConvertUtcToUserLocalTime(endTimeUtc, participant.TimeZone);
                
                invalidParticipants.Add($"{participant.Name} ({participant.TimeZone}): meeting would be from {localStartTime:HH:mm} to {localEndTime:HH:mm} (outside working hours 08:00-18:00)");
            }
        }

        if (invalidParticipants.Any())
        {
            return (false, $"Outside working hours for: {string.Join("; ", invalidParticipants)}");
        }

        return (true, string.Empty);
    }

    private (TimeSpan start, TimeSpan end, bool crossesMidnight) GetWorkingHoursInUtc(User user, DateTime date)
    {
        try
        {
            var localWorkStart = date.Date.AddHours(8);
            var localWorkEnd = date.Date.AddHours(18);

            var utcWorkStart = ConvertUserLocalTimeToUtc(localWorkStart, user.TimeZone);
            var utcWorkEnd = ConvertUserLocalTimeToUtc(localWorkEnd, user.TimeZone);

            bool crossesMidnight = utcWorkEnd.Date > utcWorkStart.Date;

            return (utcWorkStart.TimeOfDay, utcWorkEnd.TimeOfDay, crossesMidnight);
        }
        catch
        {
            return (new TimeSpan(8, 0, 0), new TimeSpan(18, 0, 0), false);
        }
    }

    private (TimeSpan start, TimeSpan end) GetCommonWorkingHours(List<User> participants, DateTime date)
    {
        if (!participants.Any())
            return (new TimeSpan(8, 0, 0), new TimeSpan(18, 0, 0));

        var participantHours = new List<(TimeSpan start, TimeSpan end, bool crossesMidnight, string name)>();
        
        foreach (var participant in participants)
        {
            var workingHours = GetWorkingHoursInUtc(participant, date);
            participantHours.Add((workingHours.start, workingHours.end, workingHours.crossesMidnight, participant.Name));
        }

        var normalHours = participantHours.Where(h => !h.crossesMidnight).ToList();
        var midnightCrossers = participantHours.Where(h => h.crossesMidnight).ToList();

        if (!midnightCrossers.Any())
        {
            var latestStart = normalHours.Max(h => h.start);
            var earliestEnd = normalHours.Min(h => h.end);
            
            if (latestStart >= earliestEnd)
                return (new TimeSpan(23, 0, 0), new TimeSpan(23, 0, 0));
                
            return (latestStart, earliestEnd);
        }
        else if (!normalHours.Any())
        {
            return FindOverlapForMidnightCrossers(midnightCrossers);
        }
        else
        {
            return FindMixedOverlap(normalHours, midnightCrossers);
        }
    }

    private (TimeSpan start, TimeSpan end) FindOverlapForMidnightCrossers(List<(TimeSpan start, TimeSpan end, bool crossesMidnight, string name)> midnightCrossers)
    {
        var latestStart = midnightCrossers.Max(h => h.start);
        var earliestEnd = midnightCrossers.Min(h => h.end);
        
        return (new TimeSpan(0, 0, 0), earliestEnd);
    }

    private (TimeSpan start, TimeSpan end) FindMixedOverlap(
        List<(TimeSpan start, TimeSpan end, bool crossesMidnight, string name)> normalHours,
        List<(TimeSpan start, TimeSpan end, bool crossesMidnight, string name)> midnightCrossers)
    {
        var normalStart = normalHours.Max(h => h.start);
        var normalEnd = normalHours.Min(h => h.end);
        
        if (normalStart >= normalEnd)
            return (new TimeSpan(23, 0, 0), new TimeSpan(23, 0, 0));
        
        foreach (var crosser in midnightCrossers)
        {
            if (crosser.start <= normalEnd)
            {
                normalStart = TimeSpan.FromTicks(Math.Max(normalStart.Ticks, crosser.start.Ticks));
                normalEnd = TimeSpan.FromTicks(Math.Min(normalEnd.Ticks, new TimeSpan(23, 59, 59).Ticks));
            }
            else
            {
                if (normalStart <= crosser.end)
                {
                    normalEnd = TimeSpan.FromTicks(Math.Min(normalEnd.Ticks, crosser.end.Ticks));
                }
                else
                {
                    return (new TimeSpan(23, 0, 0), new TimeSpan(23, 0, 0));
                }
            }
        }
        
        return normalStart >= normalEnd ? 
            (new TimeSpan(23, 0, 0), new TimeSpan(23, 0, 0)) : 
            (normalStart, normalEnd);
    }

    private DateTime ConvertUtcToUserLocalTime(DateTime utcTime, string userTimeZone)
    {
        var utcDateTime = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
        
        try
        {
            var offset = ParseTimezoneOffset(userTimeZone);
            return utcDateTime.Add(offset);
        }
        catch
        {
            return utcDateTime;
        }
    }

    private DateTime ConvertUserLocalTimeToUtc(DateTime localTime, string userTimeZone)
    {
        try
        {
            var localDateTime = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
            var offset = ParseTimezoneOffset(userTimeZone);
            
            return localDateTime.Subtract(offset);
        }
        catch
        {
            return DateTime.SpecifyKind(localTime, DateTimeKind.Utc);
        }
    }

    private TimeSpan ParseTimezoneOffset(string timeZone)
    {
        if (string.IsNullOrEmpty(timeZone) || timeZone == "UTC")
            return TimeSpan.Zero;

        var offsetString = timeZone.Replace("UTC", "").Trim();
        
        if (string.IsNullOrEmpty(offsetString))
            return TimeSpan.Zero;

        bool isPositive = true;
        if (offsetString.StartsWith("+"))
        {
            isPositive = true;
            offsetString = offsetString.Substring(1);
        }
        else if (offsetString.StartsWith("-"))
        {
            isPositive = false;
            offsetString = offsetString.Substring(1);
        }

        if (TimeSpan.TryParse(offsetString, out var offset))
        {
            return isPositive ? offset : offset.Negate();
        }

        return TimeSpan.Zero;
    }

    private MeetingDto MapToMeetingDto(Meeting meeting)
    {
        return new MeetingDto
        {
            Id = meeting.Id,
            Title = meeting.Title,
            StartTime = meeting.StartTime,
            EndTime = meeting.EndTime,
            Participants = meeting.Participants.Select(p => new UserDto
            {
                Id = p.Id,
                Name = p.Name,
                TimeZone = p.TimeZone
            }).ToList()
        };
    }
}