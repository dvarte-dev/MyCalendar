using MyCalendar.Application.DTOs;
using MyCalendar.Application.Interfaces;
using MyCalendar.Domain.Entities;
using MyCalendar.Domain.Interfaces;

namespace MyCalendar.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        return users.Select(MapToUserDto);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user != null ? MapToUserDto(user) : null;
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequestDto request)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            TimeZone = request.TimeZone
        };
        
        var createdUser = await _userRepository.AddAsync(user);
        return MapToUserDto(createdUser);
    }

    public async Task<bool> UpdateUserAsync(Guid id, UpdateUserRequestDto request)
    {
        var existingUser = await _userRepository.GetByIdAsync(id);
        if (existingUser == null)
            return false;

        existingUser.Name = request.Name;
        existingUser.TimeZone = request.TimeZone;

        await _userRepository.UpdateAsync(existingUser);
        return true;
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
            return false;

        await _userRepository.DeleteAsync(id);
        return true;
    }

    private UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            TimeZone = user.TimeZone
        };
    }
}