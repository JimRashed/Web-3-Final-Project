using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizArena.Lobby.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLobbyPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveLobbies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinCode = table.Column<string>(type: "varchar(6)", unicode: false, maxLength: 6, nullable: false, collation: "Latin1_General_100_BIN2"),
                    HostId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WriteRevision = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveLobbies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LobbyPlayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LobbyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LobbyPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LobbyPlayers_ActiveLobbies_LobbyId",
                        column: x => x.LobbyId,
                        principalTable: "ActiveLobbies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveLobbies_JoinCode",
                table: "ActiveLobbies",
                column: "JoinCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LobbyPlayers_LobbyId_PlayerId",
                table: "LobbyPlayers",
                columns: new[] { "LobbyId", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LobbyPlayers");

            migrationBuilder.DropTable(
                name: "ActiveLobbies");
        }
    }
}
