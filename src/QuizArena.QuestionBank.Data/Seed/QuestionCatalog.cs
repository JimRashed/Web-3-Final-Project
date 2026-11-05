using Microsoft.EntityFrameworkCore;
using QuizArena.QuestionBank.Data.Entities;
using QuizArena.QuestionBank.Domain.Entities;

namespace QuizArena.QuestionBank.Data.Seed;

internal static class QuestionCatalog
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var categoryId = Guid.Parse("39000000-0000-0000-0000-000000000001");
        modelBuilder.Entity<CategoryRecord>().HasData(new CategoryRecord { Id = categoryId, Name = "General knowledge" });
        string[] prompts = ["What is 2 + 2?", "Which planet is known as the red planet?", "What is the capital of France?", "How many sides does a triangle have?", "Which ocean is largest?"];
        string[][] options = [["4", "3", "5", "6"], ["Mars", "Venus", "Earth", "Jupiter"], ["Paris", "Rome", "Berlin", "Madrid"], ["3", "2", "4", "5"], ["Pacific", "Atlantic", "Indian", "Arctic"]];
        for (var index = 0; index < prompts.Length; index++)
        {
            var id = Guid.Parse($"39000000-0000-0000-0001-{index + 1:000000000000}");
            var question = new Question(id, prompts[index], "General knowledge", options[index], options[index][0]);
            modelBuilder.Entity<QuestionRecord>().HasData(new { question.Id, question.Prompt, CategoryId = categoryId, question.CorrectAnswer });
            modelBuilder.Entity<QuestionOptionRecord>().HasData(question.Options.Select((value, position) => new QuestionOptionRecord { QuestionId = id, Position = position, Value = value }));
        }
    }
}
