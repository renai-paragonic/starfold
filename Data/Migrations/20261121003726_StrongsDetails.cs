using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace starfold_mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class StrongsDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "derivation",
                table: "ezra_strongs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kjv_def",
                table: "ezra_strongs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "derivation",
                table: "ezra_strongs");

            migrationBuilder.DropColumn(
                name: "kjv_def",
                table: "ezra_strongs");
        }
    }
}
