using Microsoft.EntityFrameworkCore;
using QuizArena.Api.Services;
using QuizArena.Data.Context;
using QuizArena.Data.Repositories;
using QuizArena.Domain.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<QuizArenaContext>(options =>
    options.UseInMemoryDatabase("QuizArena"));
builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();
builder.Services.AddScoped<ILobbyRepository, LobbyRepository>();
builder.Services.AddScoped<IQuestionService, QuestionService>();
builder.Services.AddScoped<ILobbyService, LobbyService>();
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
