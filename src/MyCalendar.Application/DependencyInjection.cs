using Microsoft.Extensions.DependencyInjection;
using MyCalendar.Application.Interfaces;
using MyCalendar.Application.Services;

namespace MyCalendar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISchedulingService, SchedulingService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
} 