using Microsoft.EntityFrameworkCore;
using MyCalendar.Domain.Entities;

namespace MyCalendar.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Meeting> Meetings { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Meeting>()
            .HasMany(m => m.Participants)
            .WithMany()
            .UsingEntity(j => j.ToTable("MeetingParticipants"));
    }
} 