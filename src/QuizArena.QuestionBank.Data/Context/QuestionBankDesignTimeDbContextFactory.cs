using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuizArena.QuestionBank.Data.Context;

/// <summary>
/// Used only by <c>dotnet ef</c>. Targets local development LocalDB unless
/// <see cref="ConnectionEnvironmentVariable"/> supplies another connection string.
/// </summary>
public sealed class QuestionBankDesignTimeDbContextFactory : IDesignTimeDbContextFactory<QuestionBankDbContext>
{
    public const string ConnectionEnvironmentVariable = "QUIZARENA_QUESTION_BANK_CONNECTION";
    public const string DevelopmentConnection = "Server=(localdb)\\MSSQLLocalDB;Database=QuizArenaQuestionBankDb;Trusted_Connection=True;TrustServerCertificate=True";

    public QuestionBankDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        var options = new DbContextOptionsBuilder<QuestionBankDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connection) ? DevelopmentConnection : connection)
            .Options;
        return new QuestionBankDbContext(options);
    }
}
