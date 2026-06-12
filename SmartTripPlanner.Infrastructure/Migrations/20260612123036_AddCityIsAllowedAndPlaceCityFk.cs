using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCityIsAllowedAndPlaceCityFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAllowed",
                table: "Cities",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Cities_CityCode",
                table: "Cities",
                column: "CityCode");

            migrationBuilder.CreateIndex(
                name: "IX_Places_CityId",
                table: "Places",
                column: "CityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Places_Cities_CityId",
                table: "Places",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "CityCode",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Places_Cities_CityId",
                table: "Places");

            migrationBuilder.DropIndex(
                name: "IX_Places_CityId",
                table: "Places");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Cities_CityCode",
                table: "Cities");

            migrationBuilder.DropColumn(
                name: "IsAllowed",
                table: "Cities");
        }
    }
}
