using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace starfold_mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class LogosId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "logos_id",
                table: "ezra_notes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ezra_notes_logos_id",
                table: "ezra_notes",
                column: "logos_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ezra_notes_logos_id",
                table: "ezra_notes");

            migrationBuilder.DropColumn(
                name: "logos_id",
                table: "ezra_notes");
        }
    }
}
