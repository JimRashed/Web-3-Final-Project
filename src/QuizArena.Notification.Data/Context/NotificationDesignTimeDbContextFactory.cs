using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuizArena.Notification.Data.Context;

/// <summary>
/// Used only by <c>dotnet ef</c>. Targets local development LocalDB unless
/// <see cref="ConnectionEnvironmentVariable"/> supplies another connection string.
/// </summary>
public sealed class NotificationDesignTimeDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public const string ConnectionEnvironmentVariable = "QUIZARENA_NOTIFICATION_CONNECTION";
    public const string DevelopmentConnection = "Server=(localdb)\\MSSQLLocalDB;Database=QuizArenaNotificationDb;Trusted_Connection=True;TrustServerCertificate=True";

    public NotificationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connection) ? DevelopmentConnection : connection)
            .Options;
        return new NotificationDbContext(options);
    }
}
