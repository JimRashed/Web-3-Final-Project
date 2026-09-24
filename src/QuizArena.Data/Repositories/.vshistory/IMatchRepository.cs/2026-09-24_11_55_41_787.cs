using QuizArena.Domain.Gameplay.Entities;

namespace QuizArena.Data.Repositories;

public interface IMatchRepository
{
    Task<ActiveMatch> CreateAsync(ActiveMatch match);
    Task<ActiveMatch?> GetAsync(string matchId);
    Task<ActiveMatch> UpdateAsync(ActiveMatch match);
}