using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Removes the OnlinePayment feature, which was sellable on a plan and gated
    /// nothing — online payment is out of scope until a provider is chosen (plan
    /// §2.3). Data only: the feature is a string in a row, not a column.
    /// </summary>
    public partial class DropOnlinePaymentFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Orphan rows, not entitlements: with the constant gone nothing reads
            // them, and a plan that still listed the module would show a blank
            // label on the plan screen.
            migrationBuilder.Sql("DELETE FROM [PlanFeatures] WHERE [Feature] = 'OnlinePayment';");
            migrationBuilder.Sql("DELETE FROM [AgencyFeatures] WHERE [Feature] = 'OnlinePayment';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty: which plans sold the module is not recoverable
            // from here, and re-adding it to every plan would be a worse guess
            // than leaving it off.
        }
    }
}
