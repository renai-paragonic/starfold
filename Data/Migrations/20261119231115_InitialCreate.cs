using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace starfold_mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ezra_notes",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    slug = table.Column<string>(type: "TEXT", nullable: false),
                    body_md = table.Column<string>(type: "TEXT", nullable: false),
                    is_public = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ezra_notes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ezra_strongs",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    lemma = table.Column<string>(type: "TEXT", nullable: false),
                    transliteration = table.Column<string>(type: "TEXT", nullable: true),
                    definition = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ezra_strongs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ezra_tags",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ezra_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ezra_note_refs",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    note_id = table.Column<int>(type: "INTEGER", nullable: false),
                    book = table.Column<int>(type: "INTEGER", nullable: false),
                    chapter = table.Column<int>(type: "INTEGER", nullable: true),
                    verse_start = table.Column<int>(type: "INTEGER", nullable: true),
                    verse_end = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ezra_note_refs", x => x.id);
                    table.ForeignKey(
                        name: "FK_ezra_note_refs_ezra_notes_note_id",
                        column: x => x.note_id,
                        principalTable: "ezra_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ezra_note_strongs",
                columns: table => new
                {
                    note_id = table.Column<int>(type: "INTEGER", nullable: false),
                    strongs_id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ezra_note_strongs", x => new { x.note_id, x.strongs_id });
                    table.ForeignKey(
                        name: "FK_ezra_note_strongs_ezra_notes_note_id",
                        column: x => x.note_id,
                        principalTable: "ezra_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ezra_note_strongs_ezra_strongs_strongs_id",
                        column: x => x.strongs_id,
                        principalTable: "ezra_strongs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ezra_note_tags",
                columns: table => new
                {
                    note_id = table.Column<int>(type: "INTEGER", nullable: false),
                    tag_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ezra_note_tags", x => new { x.note_id, x.tag_id });
                    table.ForeignKey(
                        name: "FK_ezra_note_tags_ezra_notes_note_id",
                        column: x => x.note_id,
                        principalTable: "ezra_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ezra_note_tags_ezra_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "ezra_tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ezra_note_refs_book_chapter_verse_start",
                table: "ezra_note_refs",
                columns: new[] { "book", "chapter", "verse_start" });

            migrationBuilder.CreateIndex(
                name: "IX_ezra_note_refs_note_id",
                table: "ezra_note_refs",
                column: "note_id");

            migrationBuilder.CreateIndex(
                name: "IX_ezra_note_strongs_strongs_id",
                table: "ezra_note_strongs",
                column: "strongs_id");

            migrationBuilder.CreateIndex(
                name: "IX_ezra_note_tags_tag_id",
                table: "ezra_note_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "IX_ezra_tags_name",
                table: "ezra_tags",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ezra_note_refs");

            migrationBuilder.DropTable(
                name: "ezra_note_strongs");

            migrationBuilder.DropTable(
                name: "ezra_note_tags");

            migrationBuilder.DropTable(
                name: "ezra_strongs");

            migrationBuilder.DropTable(
                name: "ezra_notes");

            migrationBuilder.DropTable(
                name: "ezra_tags");
        }
    }
}
