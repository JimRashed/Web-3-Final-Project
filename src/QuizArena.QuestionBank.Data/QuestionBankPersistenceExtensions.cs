using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.QuestionBank.Data.Context;
using QuizArena.QuestionBank.Data.Repositories;
using QuizArena.QuestionBank.Domain.Repositories;

namespace QuizArena.QuestionBank.Data;

public static class QuestionBankPersistenceExtensions
{
    /// <summary>Registers <see cref="QuestionBankDbContext"/> (scoped) against the service's own SQL Server database.</summary>
    public static IServiceCollection AddQuestionBankPersistence(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for the QuestionBank database.");
        }

        if (!IsWellFormedSqlServerConnectionString(connectionString))
        {
            // Name the key only: SqlClient's parse error can quote part of the value, so it is not chained.
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not a valid SQL Server connection string for the QuestionBank database.");
        }

        services.AddDbContext<QuestionBankDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        return services;
    }

    /// <summary>Parses the connection string without opening a connection.</summary>
    private static bool IsWellFormedSqlServerConnectionString(string connectionString)
    {
        try
        {
            _ = new SqlConnectionStringBuilder(connectionString);
            return true;
        }
        catch (Exception parseFailure) when (parseFailure is ArgumentException or FormatException or OverflowException)
        {
            return false;
        }
    }
}
