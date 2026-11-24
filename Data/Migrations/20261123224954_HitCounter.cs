using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace starfold_mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class HitCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ezra_hit_counter",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false),
                    count = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ezra_hit_counter", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "ezra_hit_counter",
                columns: new[] { "id", "count" },
                values: new object[] { 1, 0L });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ezra_hit_counter");
        }
    }
}
