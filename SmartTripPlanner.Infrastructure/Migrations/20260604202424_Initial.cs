using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartTripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CityCode = table.Column<string>(type: "text", nullable: false),
                    CityName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CityId = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    HotelName = table.Column<string>(type: "text", nullable: false),
                    HotelLatitude = table.Column<double>(type: "double precision", nullable: false),
                    HotelLongitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DayPlan",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayIndex = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    WeatherSummary = table.Column<int>(type: "integer", nullable: false),
                    Morning_BlockTotalDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Morning_Id = table.Column<long>(type: "bigint", nullable: false),
                    Afternoon_BlockTotalDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Afternoon_Id = table.Column<long>(type: "bigint", nullable: false),
                    Evening_BlockTotalDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Evening_Id = table.Column<long>(type: "bigint", nullable: false),
                    TripId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DayPlan_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MustSeeInput",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlaceId = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    PinnedDayIndex = table.Column<int>(type: "integer", nullable: true),
                    PinnedBlock = table.Column<int>(type: "integer", nullable: true),
                    TripId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MustSeeInput", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MustSeeInput_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AfternoonActivities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    PlaceId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    EstimatedArrival = table.Column<int>(type: "integer", nullable: false),
                    EstimatedDeparture = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsIndoor = table.Column<bool>(type: "boolean", nullable: false),
                    TransitToNext_TransportMode = table.Column<int>(type: "integer", nullable: true),
                    TransitToNext_DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    TransitToNext_BufferMinutes = table.Column<int>(type: "integer", nullable: true),
                    TransitToNext_FrictionAlert = table.Column<bool>(type: "boolean", nullable: true),
                    DayPlanId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfternoonActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfternoonActivities_DayPlan_DayPlanId",
                        column: x => x.DayPlanId,
                        principalTable: "DayPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EveningActivities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    PlaceId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    EstimatedArrival = table.Column<int>(type: "integer", nullable: false),
                    EstimatedDeparture = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsIndoor = table.Column<bool>(type: "boolean", nullable: false),
                    TransitToNext_TransportMode = table.Column<int>(type: "integer", nullable: true),
                    TransitToNext_DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    TransitToNext_BufferMinutes = table.Column<int>(type: "integer", nullable: true),
                    TransitToNext_FrictionAlert = table.Column<bool>(type: "boolean", nullable: true),
                    DayPlanId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EveningActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EveningActivities_DayPlan_DayPlanId",
                        column: x => x.DayPlanId,
                        principalTable: "DayPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MorningActivities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    PlaceId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    EstimatedArrival = table.Column<int>(type: "integer", nullable: false),
                    EstimatedDeparture = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsIndoor = table.Column<bool>(type: "boolean", nullable: false),
                    TransitToNext_TransportMode = table.Column<int>(type: "integer", nullable: true),
                    TransitToNext_DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    TransitToNext_BufferMinutes = table.Column<int>(type: "integer", nullable: true),
                    TransitToNext_FrictionAlert = table.Column<bool>(type: "boolean", nullable: true),
                    DayPlanId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MorningActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MorningActivities_DayPlan_DayPlanId",
                        column: x => x.DayPlanId,
                        principalTable: "DayPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AfternoonActivities_DayPlanId",
                table: "AfternoonActivities",
                column: "DayPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_CityCode",
                table: "Cities",
                column: "CityCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DayPlan_TripId",
                table: "DayPlan",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_EveningActivities_DayPlanId",
                table: "EveningActivities",
                column: "DayPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MorningActivities_DayPlanId",
                table: "MorningActivities",
                column: "DayPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MustSeeInput_TripId",
                table: "MustSeeInput",
                column: "TripId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AfternoonActivities");

            migrationBuilder.DropTable(
                name: "Cities");

            migrationBuilder.DropTable(
                name: "EveningActivities");

            migrationBuilder.DropTable(
                name: "MorningActivities");

            migrationBuilder.DropTable(
                name: "MustSeeInput");

            migrationBuilder.DropTable(
                name: "DayPlan");

            migrationBuilder.DropTable(
                name: "Trips");
        }
    }
}
