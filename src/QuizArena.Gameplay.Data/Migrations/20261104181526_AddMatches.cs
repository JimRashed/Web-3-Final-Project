using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizArena.Gameplay.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    MatchId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    LobbyId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    PlayerAId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    PlayerBId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PlayerAScore = table.Column<int>(type: "int", nullable: false),
                    PlayerBScore = table.Column<int>(type: "int", nullable: false),
                    CurrentQuestionIndex = table.Column<int>(type: "int", nullable: false),
                    WinnerUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, collation: "Latin1_General_100_BIN2"),
                    Draw = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QuestionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnswersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.MatchId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_PlayerA_CreatedAt",
                table: "Matches",
                columns: new[] { "PlayerAId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_PlayerB_CreatedAt",
                table: "Matches",
                columns: new[] { "PlayerBId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_Matches_LobbyId",
                table: "Matches",
                column: "LobbyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Matches");
        }
    }
}
