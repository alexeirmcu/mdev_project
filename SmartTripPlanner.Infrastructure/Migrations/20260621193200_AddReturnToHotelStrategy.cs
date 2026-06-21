using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnToHotelStrategy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrefReturnToHotelStrategy",
                table: "Trips",
                type: "text",
                nullable: false,
                defaultValue: "Always");

            migrationBuilder.AddColumn<int>(
                name: "Afternoon_InterBlockTransit_BufferMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Afternoon_InterBlockTransit_DurationMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Afternoon_InterBlockTransit_FrictionAlert",
                table: "DayPlan",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Afternoon_InterBlockTransit_TransportMode",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Evening_InterBlockTransit_BufferMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Evening_InterBlockTransit_DurationMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Evening_InterBlockTransit_FrictionAlert",
                table: "DayPlan",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Evening_InterBlockTransit_TransportMode",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Morning_InterBlockTransit_BufferMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Morning_InterBlockTransit_DurationMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Morning_InterBlockTransit_FrictionAlert",
                table: "DayPlan",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Morning_InterBlockTransit_TransportMode",
                table: "DayPlan",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrefReturnToHotelStrategy",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "Afternoon_InterBlockTransit_BufferMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Afternoon_InterBlockTransit_DurationMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Afternoon_InterBlockTransit_FrictionAlert",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Afternoon_InterBlockTransit_TransportMode",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_InterBlockTransit_BufferMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_InterBlockTransit_DurationMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_InterBlockTransit_FrictionAlert",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_InterBlockTransit_TransportMode",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Morning_InterBlockTransit_BufferMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Morning_InterBlockTransit_DurationMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Morning_InterBlockTransit_FrictionAlert",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Morning_InterBlockTransit_TransportMode",
                table: "DayPlan");
        }
    }
}
