using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyCalendar.Infrastructure.Data;
using Swashbuckle.AspNetCore.Annotations;

namespace MyCalendar.API.Controllers;

/// <summary>
/// Controller for API functionality testing
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TestController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public TestController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Check if the API is working
    /// </summary>
    /// <returns>Confirmation message</returns>
    /// <response code="200">API is running</response>
    [HttpGet]
    [SwaggerOperation(Summary = "Check API status", Description = "Basic endpoint to verify if the API is online")]
    [SwaggerResponse(200, "API is running")]
    public ActionResult<string> Get()
    {
        return Ok("API is working!");
    }

    /// <summary>
    /// Test database connection
    /// </summary>
    /// <returns>Database connection status</returns>
    /// <response code="200">Database connection established</response>
    /// <response code="500">Error connecting to database</response>
    [HttpGet("db-connection")]
    [SwaggerOperation(Summary = "Test database connection", Description = "Verifies if the API can connect to the PostgreSQL database")]
    [SwaggerResponse(200, "Database connection established successfully")]
    [SwaggerResponse(500, "Error connecting to database")]
    public async Task<ActionResult<string>> TestDatabaseConnection()
    {
        try
        {
            bool canConnect = await _dbContext.Database.CanConnectAsync();
            
            if (canConnect)
            {
                return Ok("Database connection established successfully!");
            }
            else
            {
                return StatusCode(500, "Could not connect to database.");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error connecting to database: {ex.Message}");
        }
    }
}