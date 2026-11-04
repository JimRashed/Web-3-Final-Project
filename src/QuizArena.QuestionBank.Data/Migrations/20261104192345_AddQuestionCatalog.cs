using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QuizArena.QuestionBank.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectAnswer = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Questions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionOptions",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOptions", x => new { x.QuestionId, x.Position });
                    table.CheckConstraint("CK_QuestionOptions_Position", "[Position] >= 0 AND [Position] < 4");
                    table.ForeignKey(
                        name: "FK_QuestionOptions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name" },
                values: new object[] { new Guid("39000000-0000-0000-0000-000000000001"), "General knowledge" });

            migrationBuilder.InsertData(
                table: "Questions",
                columns: new[] { "Id", "CategoryId", "CorrectAnswer", "Prompt" },
                values: new object[,]
                {
                    { new Guid("39000000-0000-0000-0001-000000000001"), new Guid("39000000-0000-0000-0000-000000000001"), "4", "What is 2 + 2?" },
                    { new Guid("39000000-0000-0000-0001-000000000002"), new Guid("39000000-0000-0000-0000-000000000001"), "Mars", "Which planet is known as the red planet?" },
                    { new Guid("39000000-0000-0000-0001-000000000003"), new Guid("39000000-0000-0000-0000-000000000001"), "Paris", "What is the capital of France?" },
                    { new Guid("39000000-0000-0000-0001-000000000004"), new Guid("39000000-0000-0000-0000-000000000001"), "3", "How many sides does a triangle have?" },
                    { new Guid("39000000-0000-0000-0001-000000000005"), new Guid("39000000-0000-0000-0000-000000000001"), "Pacific", "Which ocean is largest?" }
                });

            migrationBuilder.InsertData(
                table: "QuestionOptions",
                columns: new[] { "Position", "QuestionId", "Value" },
                values: new object[,]
                {
                    { 0, new Guid("39000000-0000-0000-0001-000000000001"), "4" },
                    { 1, new Guid("39000000-0000-0000-0001-000000000001"), "3" },
                    { 2, new Guid("39000000-0000-0000-0001-000000000001"), "5" },
                    { 3, new Guid("39000000-0000-0000-0001-000000000001"), "6" },
                    { 0, new Guid("39000000-0000-0000-0001-000000000002"), "Mars" },
                    { 1, new Guid("39000000-0000-0000-0001-000000000002"), "Venus" },
                    { 2, new Guid("39000000-0000-0000-0001-000000000002"), "Earth" },
                    { 3, new Guid("39000000-0000-0000-0001-000000000002"), "Jupiter" },
                    { 0, new Guid("39000000-0000-0000-0001-000000000003"), "Paris" },
                    { 1, new Guid("39000000-0000-0000-0001-000000000003"), "Rome" },
                    { 2, new Guid("39000000-0000-0000-0001-000000000003"), "Berlin" },
                    { 3, new Guid("39000000-0000-0000-0001-000000000003"), "Madrid" },
                    { 0, new Guid("39000000-0000-0000-0001-000000000004"), "3" },
                    { 1, new Guid("39000000-0000-0000-0001-000000000004"), "2" },
                    { 2, new Guid("39000000-0000-0000-0001-000000000004"), "4" },
                    { 3, new Guid("39000000-0000-0000-0001-000000000004"), "5" },
                    { 0, new Guid("39000000-0000-0000-0001-000000000005"), "Pacific" },
                    { 1, new Guid("39000000-0000-0000-0001-000000000005"), "Atlantic" },
                    { 2, new Guid("39000000-0000-0000-0001-000000000005"), "Indian" },
                    { 3, new Guid("39000000-0000-0000-0001-000000000005"), "Arctic" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);


            migrationBuilder.CreateIndex(
                name: "IX_Questions_CategoryId",
                table: "Questions",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionOptions");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
