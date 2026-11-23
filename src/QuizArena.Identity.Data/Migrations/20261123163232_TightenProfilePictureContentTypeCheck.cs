using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizArena.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class TightenProfilePictureContentTypeCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProfilePictures_ContentType",
                table: "ProfilePictures");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProfilePictures_ContentType",
                table: "ProfilePictures",
                sql: "([ContentType] + N'|') COLLATE Latin1_General_BIN2 IN (N'image/jpeg|', N'image/png|', N'image/webp|')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProfilePictures_ContentType",
                table: "ProfilePictures");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProfilePictures_ContentType",
                table: "ProfilePictures",
                sql: "[ContentType] COLLATE Latin1_General_BIN2 IN (N'image/jpeg', N'image/png', N'image/webp')");
        }
    }
}
