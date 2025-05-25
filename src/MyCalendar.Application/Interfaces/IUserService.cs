using MyCalendar.Application.DTOs;

namespace MyCalendar.Application.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<UserDto?> GetUserByIdAsync(Guid id);
    Task<UserDto> CreateUserAsync(CreateUserRequestDto request);
    Task<bool> UpdateUserAsync(Guid id, UpdateUserRequestDto request);
    Task<bool> DeleteUserAsync(Guid id);
}