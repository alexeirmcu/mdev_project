using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityNodeLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Trips");

            migrationBuilder.AddColumn<List<string>>(
                name: "Preferences_Interests",
                table: "Trips",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "MorningActivities",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "MorningActivities",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "EveningActivities",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "EveningActivities",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "AfternoonActivities",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "AfternoonActivities",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Preferences_Interests",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "MorningActivities");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "MorningActivities");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "EveningActivities");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "EveningActivities");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "AfternoonActivities");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "AfternoonActivities");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Trips",
                type: "text",
                nullable: false,
                defaultValue: "CREATED");
        }
    }
}
