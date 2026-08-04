using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCarMileage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Mileage",
                table: "Cars",
                type: "int",
                nullable: true);

            // Seed the new odometer from what the fleet's hires already recorded:
            // the highest pickup/return reading on each car is the best the data
            // knows, and it is what the app itself would have arrived at (see
            // Car.RecordOdometer). Cancelled hires never went out, so their
            // readings do not count. A car with no reading on file stays NULL —
            // "nobody has told us" is not the same as zero.
            migrationBuilder.Sql(@"
                UPDATE Cars
                SET Mileage = (
                    SELECT MAX(v.Reading)
                    FROM Rentings r
                    CROSS APPLY (VALUES (r.StartMileage), (r.EndMileage)) AS v(Reading)
                    WHERE r.CarId = Cars.Id AND r.RentingState <> 3
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mileage",
                table: "Cars");
        }
    }
}
