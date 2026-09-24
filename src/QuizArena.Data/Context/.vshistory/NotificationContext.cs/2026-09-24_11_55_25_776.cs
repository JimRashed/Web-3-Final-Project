using Microsoft.EntityFrameworkCore;
using QuizArena.Domain.Notification.Entities;

namespace QuizArena.Data.Context;

public class NotificationContext : DbContext
{
    public NotificationContext(DbContextOptions<NotificationContext> options) : base(options) { }

    public DbSet<EventLog> EventLogs => Set<EventLog>();
    public DbSet<UserPref> UserPrefs => Set<UserPref>();
}