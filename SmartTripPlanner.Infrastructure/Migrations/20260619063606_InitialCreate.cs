using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartTripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    CityName = table.Column<string>(type: "text", nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlaceAttributes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaceAttributes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Places",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CityId = table.Column<long>(type: "bigint", nullable: false),
                    Location_Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Location_Longitude = table.Column<double>(type: "double precision", nullable: false),
                    TypicalDurationMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    IsIndoor = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsFamilyFriendly = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsAutoUpdateEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Places", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Places_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    TripCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CityId = table.Column<long>(type: "bigint", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    HotelName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HotelLatitude = table.Column<double>(type: "double precision", nullable: true),
                    HotelLongitude = table.Column<double>(type: "double precision", nullable: true),
                    TravelersAdults = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    TravelersChildren = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TravelersInfants = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PrefCarAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PrefMaxWalkingMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    PrefWeatherAwareEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Preferences_Interests = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "ARRAY[]::text[]"),
                    DefaultStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trips_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaceOpeningHours",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    OpenMinutes = table.Column<int>(type: "integer", nullable: false),
                    CloseMinutes = table.Column<int>(type: "integer", nullable: false),
                    PlaceId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaceOpeningHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaceOpeningHours_Places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "Places",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlacePlaceAttributes",
                columns: table => new
                {
                    PlaceId = table.Column<long>(type: "bigint", nullable: false),
                    PlaceAttributeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlacePlaceAttributes", x => new { x.PlaceId, x.PlaceAttributeId });
                    table.ForeignKey(
                        name: "FK_PlacePlaceAttributes_PlaceAttributes_PlaceAttributeId",
                        column: x => x.PlaceAttributeId,
                        principalTable: "PlaceAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlacePlaceAttributes_Places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "Places",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    Morning_BlockType = table.Column<int>(type: "integer", nullable: false),
                    Morning_Id = table.Column<long>(type: "bigint", nullable: false),
                    Afternoon_BlockType = table.Column<int>(type: "integer", nullable: false),
                    Afternoon_Id = table.Column<long>(type: "bigint", nullable: false),
                    Evening_BlockType = table.Column<int>(type: "integer", nullable: false),
                    Evening_Id = table.Column<long>(type: "bigint", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
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
                name: "TripMustSees",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlaceId = table.Column<long>(type: "bigint", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    PinnedDayIndex = table.Column<int>(type: "integer", nullable: true),
                    PinnedBlock = table.Column<string>(type: "text", nullable: true),
                    TripId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripMustSees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripMustSees_Trips_TripId",
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
                    PlaceId = table.Column<long>(type: "bigint", nullable: false),
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
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
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
                    PlaceId = table.Column<long>(type: "bigint", nullable: false),
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
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
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
                    PlaceId = table.Column<long>(type: "bigint", nullable: false),
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
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
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

            migrationBuilder.InsertData(
                table: "Cities",
                columns: new[] { "Id", "CityCode", "CityName", "IsAllowed" },
                values: new object[] { 1L, "madrid", "Madrid", true });

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

            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""IX_PlaceAttributes_Provider_Key_Value_CI"" ON ""PlaceAttributes"" (LOWER(""Provider""), LOWER(""Key""), LOWER(""Value""));");

            migrationBuilder.CreateIndex(
                name: "IX_PlaceOpeningHours_PlaceId",
                table: "PlaceOpeningHours",
                column: "PlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_PlacePlaceAttributes_PlaceAttributeId",
                table: "PlacePlaceAttributes",
                column: "PlaceAttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_Places_CityId",
                table: "Places",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Places_ProviderReferenceId",
                table: "Places",
                column: "ProviderReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripMustSees_TripId",
                table: "TripMustSees",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_CityId",
                table: "Trips",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TripCode",
                table: "Trips",
                column: "TripCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TripId",
                table: "Trips",
                column: "TripId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AfternoonActivities");

            migrationBuilder.DropTable(
                name: "EveningActivities");

            migrationBuilder.DropTable(
                name: "MorningActivities");

            migrationBuilder.DropTable(
                name: "PlaceOpeningHours");

            migrationBuilder.DropTable(
                name: "PlacePlaceAttributes");

            migrationBuilder.DropTable(
                name: "TripMustSees");

            migrationBuilder.DropTable(
                name: "DayPlan");

            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ""IX_PlaceAttributes_Provider_Key_Value_CI"";");

            migrationBuilder.DropTable(
                name: "PlaceAttributes");

            migrationBuilder.DropTable(
                name: "Places");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropTable(
                name: "Cities");
        }
    }
}
