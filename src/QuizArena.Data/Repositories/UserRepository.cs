using Microsoft.EntityFrameworkCore;
using QuizArena.Data.Context;
using QuizArena.Domain.Identity.Entities;

namespace QuizArena.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly QuizArenaContext _context;

    public UserRepository(QuizArenaContext context)
    {
        _context = context;
    }

    public Task<User?> GetByIdAsync(Guid id) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id);

    public Task<User?> GetByUsernameAsync(string username) =>
        _context.Users.FirstOrDefaultAsync(u => u.Username == username);

    public async Task AddAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }
}