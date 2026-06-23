using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMustSeeOvertimeSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PrefAllowMustSeeOvertime",
                table: "Trips",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OvertimeAlert",
                table: "MorningActivities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OvertimeAlert",
                table: "EveningActivities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OvertimeAlert",
                table: "AfternoonActivities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrefAllowMustSeeOvertime",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "OvertimeAlert",
                table: "MorningActivities");

            migrationBuilder.DropColumn(
                name: "OvertimeAlert",
                table: "EveningActivities");

            migrationBuilder.DropColumn(
                name: "OvertimeAlert",
                table: "AfternoonActivities");
        }
    }
}
