using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTripOwnerUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Trips",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_OwnerUserId",
                table: "Trips",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_OwnerUserId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Trips");
        }
    }
}
