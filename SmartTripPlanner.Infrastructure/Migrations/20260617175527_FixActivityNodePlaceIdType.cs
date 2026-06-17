using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixActivityNodePlaceIdType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Morning_BlockTotalDurationMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_BlockTotalDurationMinutes",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Afternoon_BlockTotalDurationMinutes",
                table: "DayPlan");

            migrationBuilder.AddColumn<int>(
                name: "Morning_BlockType",
                table: "DayPlan",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Evening_BlockType",
                table: "DayPlan",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "Afternoon_BlockType",
                table: "DayPlan",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<long>(
                name: "PlaceId",
                table: "MorningActivities",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<long>(
                name: "PlaceId",
                table: "EveningActivities",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<long>(
                name: "PlaceId",
                table: "AfternoonActivities",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Morning_BlockType",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Evening_BlockType",
                table: "DayPlan");

            migrationBuilder.DropColumn(
                name: "Afternoon_BlockType",
                table: "DayPlan");

            migrationBuilder.AddColumn<int>(
                name: "Morning_BlockTotalDurationMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Evening_BlockTotalDurationMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Afternoon_BlockTotalDurationMinutes",
                table: "DayPlan",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "PlaceId",
                table: "MorningActivities",
                type: "text",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "PlaceId",
                table: "EveningActivities",
                type: "text",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "PlaceId",
                table: "AfternoonActivities",
                type: "text",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
