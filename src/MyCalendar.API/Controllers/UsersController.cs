using Microsoft.AspNetCore.Mvc;
using MyCalendar.Application.DTOs;
using MyCalendar.Application.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace MyCalendar.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [SwaggerOperation(Summary = "Get all users")]
    [SwaggerResponse(200, "Users list returned successfully")]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "Get user by ID")]
    [SwaggerResponse(200, "User found")]
    [SwaggerResponse(404, "User not found")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound();
        
        return Ok(user);
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Create user")]
    [SwaggerResponse(201, "User created successfully")]
    [SwaggerResponse(400, "Invalid request")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var createdUser = await _userService.CreateUserAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = createdUser.Id }, createdUser);
    }

    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "Update user")]
    [SwaggerResponse(204, "User updated successfully")]
    [SwaggerResponse(400, "Invalid request")]
    [SwaggerResponse(404, "User not found")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var success = await _userService.UpdateUserAsync(id, request);
        if (!success)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Delete user")]
    [SwaggerResponse(204, "User deleted successfully")]
    [SwaggerResponse(404, "User not found")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _userService.DeleteUserAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }
}