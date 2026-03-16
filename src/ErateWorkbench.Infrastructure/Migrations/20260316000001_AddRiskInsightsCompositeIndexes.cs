using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErateWorkbench.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskInsightsCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FundingCommitments(FundingYear, ApplicantEntityNumber)
            // Speeds up the year-filtered GROUP BY ApplicantEntityNumber pattern used by
            // GetTopRiskApplicantsAsync, GetTopCommitmentDisbursementGapsAsync, and
            // GetTopReductionRatesAsync. With only single-column indexes, SQLite must
            // use FundingYear to locate rows then do a full-group-scan for ApplicantEntityNumber.
            // The composite lets it satisfy both in one contiguous index range per year.
            migrationBuilder.CreateIndex(
                name: "IX_FundingCommitments_FundingYear_ApplicantEntityNumber",
                table: "FundingCommitments",
                columns: new[] { "FundingYear", "ApplicantEntityNumber" });

            // Disbursements(FundingYear, ApplicantEntityNumber)
            // Mirrors the FundingCommitments pattern above for the disbursement-side
            // aggregation in the same repository methods. Also supports the snapshot
            // query WHERE FundingYear=? when year filter is active.
            migrationBuilder.CreateIndex(
                name: "IX_Disbursements_FundingYear_ApplicantEntityNumber",
                table: "Disbursements",
                columns: new[] { "FundingYear", "ApplicantEntityNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FundingCommitments_FundingYear_ApplicantEntityNumber",
                table: "FundingCommitments");

            migrationBuilder.DropIndex(
                name: "IX_Disbursements_FundingYear_ApplicantEntityNumber",
                table: "Disbursements");
        }
    }
}
