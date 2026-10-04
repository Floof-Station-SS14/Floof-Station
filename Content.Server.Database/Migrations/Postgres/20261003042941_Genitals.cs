using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class Genitals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "genitals",
                columns: table => new
                {
                    profile_id = table.Column<int>(type: "integer", nullable: false),
                    penis = table.Column<bool>(type: "boolean", nullable: false),
                    vagina = table.Column<bool>(type: "boolean", nullable: false),
                    breasts = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genitals", x => x.profile_id);
                    table.ForeignKey(
                        name: "FK_genitals_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "genitals");
        }
    }
}
