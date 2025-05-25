# MyCalendar - Event Scheduling and Conflict Resolution System

A backend service for collaborative calendar system that supports scheduling meetings between multiple participants, ensuring no time conflicts, handling timezone differences, and providing automatic conflict resolution suggestions.

## Architecture

The system implements Clean Architecture with clear separation of concerns:

```
├── API/             # REST Controllers and HTTP endpoints
├── Application/     # Business logic and services
├── Domain/          # Core entities (User, Meeting)
└── Infrastructure/  # Data repositories and database access
```

**Technologies**: .NET 8, Entity Framework Core, PostgreSQL, xUnit

## Core Features

- **Conflict Detection**: Automatically checks for scheduling conflicts across participants
- **Intelligent Suggestions**: Provides up to 3 alternative time slots when conflicts occur
- **Multi-Timezone Support**: Handles participants in different time zones with working hours validation
- **Efficient Data Structures**: Uses optimized queries for handling thousands of meetings per user
- **Working Hours Overlap Analysis**: Calculates common available time windows between participants

## Data Model

### User
- Unique ID, Name, TimeZone
- Working hours: 08:00-18:00 local time

### Meeting
- Unique ID, Title, StartTime (UTC), EndTime (UTC), List of Participants

## Setup and Execution

### Prerequisites
- .NET 8 SDK
- PostgreSQL

### Configuration

**Database Setup**: The project comes with a pre-configured AWS RDS PostgreSQL database that is already set up and ready to use. No additional database configuration is required.

The connection string in `appsettings.json` is already configured:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=database-1.c4pg4geema71.us-east-1.rds.amazonaws.com;Database=MyCalendar;Username=postgres;Password=postgres123"
  }
}
```

**Optional - Local Database Setup**: If you prefer to use your own local PostgreSQL database:

1. Install PostgreSQL locally
2. Update the connection string in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=MyCalendar;Username=postgres;Password=your-password"
  }
}
```
3. Apply database migrations:
```bash
dotnet ef database update
```

### Running the Application

```bash
cd src/MyCalendar.API
dotnet run
```

The application will be available at:
- **API Documentation**: `https://localhost:5089/swagger`
- **Interactive Demo**: `https://localhost:5089/demo`

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/users` | Create user with timezone |
| `GET` | `/api/users` | List all users |
| `POST` | `/api/meetings/schedule` | Schedule meeting with conflict detection |
| `GET` | `/api/meetings` | List all meetings |
| `POST` | `/api/meetings/analyze-conflicts` | Analyze working hours overlap |
| `GET` | `/api/meetings/available-slots` | Find available time slots |

## Usage Examples

### Creating Users
```bash
POST /api/users
{
  "name": "João Silva",
  "timeZone": "UTC-3:00"
}
```

### Scheduling Meeting
```bash
POST /api/meetings/schedule
{
  "title": "Planning Meeting",
  "startTime": "2024-01-15T14:00:00Z",
  "endTime": "2024-01-15T15:00:00Z",
  "participantIds": ["user1-id", "user2-id"]
}
```

### Response with Conflict
```json
{
  "isSuccess": false,
  "message": "Conflito de horário detectado",
  "suggestedTimeSlots": [
    {
      "startTime": "2024-01-15T15:00:00Z",
      "endTime": "2024-01-15T16:00:00Z"
    }
  ]
}
```

## Timezone Handling

The system correctly handles complex timezone scenarios:

### Standard Timezones
- UTC-3:00 (Brazil): 08:00-18:00 local = 11:00-21:00 UTC
- UTC+1:00 (Europe): 08:00-18:00 local = 07:00-17:00 UTC

### Midnight-Crossing Timezones
- UTC+9:00 (Japan): 08:00-18:00 local = 23:00-09:00 UTC (crosses midnight)
- UTC+10:00 (Australia): 08:00-18:00 local = 22:00-08:00 UTC (crosses midnight)

### Working Hours Overlap Calculation
The system calculates common working hours between participants. When participants are in time zones with no overlapping business hours, the system will indicate that no common working time exists. In such cases, meetings would require participants to work outside their normal business hours or arrange overtime.

## Testing

### Running Tests
```bash
dotnet test
```

### Test Coverage
- Unit tests for all core scheduling logic
- Integration tests for API endpoints
- Repository tests for data access
- Performance tests for conflict detection efficiency

### Test Categories
- Scheduling without conflicts
- Simple conflict detection
- Multi-timezone scenarios
- Midnight-crossing timezones
- Intelligent suggestion algorithms
- Working hours overlap analysis
- Input validation and error handling

## Performance Considerations

- Optimized database queries for conflict detection
- Efficient time range calculations
- Indexed database fields for fast lookups
- Tested with thousands of meetings per user
