using Microsoft.EntityFrameworkCore;
using QuizArena.Api.Services;
using QuizArena.Data.Context;
using QuizArena.Data.Repositories;
using QuizArena.Domain.Services;
using QuizArena.Statistics.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Lobby + Question Bank
builder.Services.AddDbContext<QuizArenaContext>(options =>
    options.UseInMemoryDatabase("QuizArena"));
builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();
builder.Services.AddScoped<ILobbyRepository, LobbyRepository>();
builder.Services.AddScoped<IQuestionService, QuestionService>();
builder.Services.AddScoped<ILobbyService, LobbyService>();

// Identity
builder.Services.AddDbContext<IdentityContext>(options =>
    options.UseInMemoryDatabase("Identity"));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Statistics
builder.Services.AddDbContext<StatisticsContext>(options =>
    options.UseInMemoryDatabase("Statistics"));
builder.Services.AddScoped<IStatisticsRepository, StatisticsRepository>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();