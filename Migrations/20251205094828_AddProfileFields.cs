using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sustainability_canvas_api.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "Profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Organization",
                table: "Profiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Department",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Organization",
                table: "Profiles");
        }
    }
}
