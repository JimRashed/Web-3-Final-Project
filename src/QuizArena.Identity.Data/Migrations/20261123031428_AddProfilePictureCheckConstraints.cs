using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizArena.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfilePictureCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_ProfilePictures_ContentLength",
                table: "ProfilePictures",
                sql: "DATALENGTH([Content]) BETWEEN 1 AND 2097152");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProfilePictures_ContentType",
                table: "ProfilePictures",
                sql: "[ContentType] COLLATE Latin1_General_BIN2 IN (N'image/jpeg', N'image/png', N'image/webp')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProfilePictures_Version",
                table: "ProfilePictures",
                sql: "[Version] >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProfilePictures_ContentLength",
                table: "ProfilePictures");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProfilePictures_ContentType",
                table: "ProfilePictures");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProfilePictures_Version",
                table: "ProfilePictures");
        }
    }
}
