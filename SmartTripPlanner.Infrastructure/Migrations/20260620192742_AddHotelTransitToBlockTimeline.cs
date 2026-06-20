using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHotelTransitToBlockTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlaceAttributes_Provider_Key_Value_CI",
                table: "PlaceAttributes");

            migrationBuilder.AddColumn<int>(
                name: "Afternoon_TransitFromHotel_BufferMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Afternoon_TransitFromHotel_DurationMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Afternoon_TransitFromHotel_FrictionAlert",
                table: "DayPlan",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Afternoon_TransitFromHotel_TransportMode",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Afternoon_TransitToHotel_BufferMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Afternoon_TransitToHotel_DurationMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Afternoon_TransitToHotel_FrictionAlert",
                table: "DayPlan",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Afternoon_TransitToHotel_TransportMode",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Evening_TransitFromHotel_BufferMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Evening_TransitFromHotel_DurationMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Evening_TransitFromHotel_FrictionAlert",
                table: "DayPlan",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Evening_TransitFromHotel_TransportMode",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Evening_TransitToHotel_BufferMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Evening_TransitToHotel_DurationMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Evening_TransitToHotel_FrictionAlert",
                table: "DayPlan",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Evening_TransitToHotel_TransportMode",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Morning_TransitFromHotel_BufferMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Morning_TransitFromHotel_DurationMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Morning_TransitFromHotel_FrictionAlert",
                table: "DayPlan",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Morning_TransitFromHotel_TransportMode",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Morning_TransitToHotel_BufferMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Morning_TransitToHotel_DurationMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Morning_TransitToHotel_FrictionAlert",
                table: "DayPlan",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Morning_TransitToHotel_TransportMode",
                table: "DayPlan",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Afternoon_TransitFromHotel_BufferMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Afternoon_TransitFromHotel_DurationMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Afternoon_TransitFromHotel_FrictionAlert",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Afternoon_TransitFromHotel_TransportMode",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Afternoon_TransitToHotel_BufferMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Afternoon_TransitToHotel_DurationMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Afternoon_TransitToHotel_FrictionAlert",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Afternoon_TransitToHotel_TransportMode",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_TransitFromHotel_BufferMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_TransitFromHotel_DurationMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_TransitFromHotel_FrictionAlert",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_TransitFromHotel_TransportMode",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_TransitToHotel_BufferMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_TransitToHotel_DurationMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_TransitToHotel_FrictionAlert",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_TransitToHotel_TransportMode",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Morning_TransitFromHotel_BufferMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Morning_TransitFromHotel_DurationMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Morning_TransitFromHotel_FrictionAlert",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Morning_TransitFromHotel_TransportMode",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Morning_TransitToHotel_BufferMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Morning_TransitToHotel_DurationMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Morning_TransitToHotel_FrictionAlert",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Morning_TransitToHotel_TransportMode",
                table: "DayPlan");

            migrationBuilder.CreateIndex(
                name: "IX_PlaceAttributes_Provider_Key_Value_CI",
                table: "PlaceAttributes",
                columns: new[] { "Provider", "Key", "Value" },
                unique: true);
        }
    }
}
