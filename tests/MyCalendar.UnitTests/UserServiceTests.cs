using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MyCalendar.Application.DTOs;
using MyCalendar.Application.Services;
using MyCalendar.Domain.Entities;
using MyCalendar.Infrastructure.Data;
using MyCalendar.Infrastructure.Repositories;

namespace MyCalendar.UnitTests;

/// <summary>
/// Unit tests for UserService
/// </summary>
public class UserServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserRepository _userRepository;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _userRepository = new UserRepository(_context);
        _userService = new UserService(_userRepository);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateUser_ValidData_ShouldSucceed()
    {
        // Arrange
        var request = new CreateUserRequestDto
        {
            Name = "João Silva",
            TimeZone = "UTC-3:00"
        };

        // Act
        var result = await _userService.CreateUserAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("João Silva");
        result.TimeZone.Should().Be("UTC-3:00");
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllUsers_WithExistingUsers_ShouldReturnAll()
    {
        // Arrange
        await _userService.CreateUserAsync(new CreateUserRequestDto { Name = "User 1", TimeZone = "UTC" });
        await _userService.CreateUserAsync(new CreateUserRequestDto { Name = "User 2", TimeZone = "UTC+1:00" });

        // Act
        var result = await _userService.GetAllUsersAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(u => u.Name == "User 1");
        result.Should().Contain(u => u.Name == "User 2");
    }

    [Fact]
    public async Task GetUserById_ExistingUser_ShouldReturnUser()
    {
        // Arrange
        var createdUser = await _userService.CreateUserAsync(new CreateUserRequestDto 
        { 
            Name = "Test User", 
            TimeZone = "UTC+5:30" 
        });

        // Act
        var result = await _userService.GetUserByIdAsync(createdUser.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(createdUser.Id);
        result.Name.Should().Be("Test User");
        result.TimeZone.Should().Be("UTC+5:30");
    }

    [Fact]
    public async Task GetUserById_NonExistentUser_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _userService.GetUserByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateUser_ExistingUser_ShouldSucceed()
    {
        // Arrange
        var createdUser = await _userService.CreateUserAsync(new CreateUserRequestDto 
        { 
            Name = "Original Name", 
            TimeZone = "UTC" 
        });

        var updateRequest = new UpdateUserRequestDto
        {
            Name = "Updated Name",
            TimeZone = "UTC-5:00"
        };

        // Act
        var result = await _userService.UpdateUserAsync(createdUser.Id, updateRequest);

        // Assert
        result.Should().BeTrue();

        var updatedUser = await _userService.GetUserByIdAsync(createdUser.Id);
        updatedUser!.Name.Should().Be("Updated Name");
        updatedUser.TimeZone.Should().Be("UTC-5:00");
    }

    [Fact]
    public async Task UpdateUser_NonExistentUser_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new UpdateUserRequestDto
        {
            Name = "Updated Name",
            TimeZone = "UTC-5:00"
        };

        // Act
        var result = await _userService.UpdateUserAsync(nonExistentId, updateRequest);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUser_ExistingUser_ShouldSucceed()
    {
        // Arrange
        var createdUser = await _userService.CreateUserAsync(new CreateUserRequestDto 
        { 
            Name = "User to Delete", 
            TimeZone = "UTC" 
        });

        // Act
        var result = await _userService.DeleteUserAsync(createdUser.Id);

        // Assert
        result.Should().BeTrue();

        var deletedUser = await _userService.GetUserByIdAsync(createdUser.Id);
        deletedUser.Should().BeNull();
    }

    [Fact]
    public async Task DeleteUser_NonExistentUser_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _userService.DeleteUserAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();
    }
} 