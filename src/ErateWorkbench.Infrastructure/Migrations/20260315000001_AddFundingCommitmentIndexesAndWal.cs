using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErateWorkbench.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFundingCommitmentIndexesAndWal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable WAL mode for better read/write concurrency. This setting is
            // persisted in the database file header, so it only needs to be applied once.
            migrationBuilder.Sql("PRAGMA journal_mode=WAL;");

            migrationBuilder.CreateIndex(
                name: "IX_FundingCommitments_FundingYear",
                table: "FundingCommitments",
                column: "FundingYear");

            migrationBuilder.CreateIndex(
                name: "IX_FundingCommitments_CategoryOfService",
                table: "FundingCommitments",
                column: "CategoryOfService");

            migrationBuilder.CreateIndex(
                name: "IX_FundingCommitments_ServiceProviderName",
                table: "FundingCommitments",
                column: "ServiceProviderName");

            migrationBuilder.CreateIndex(
                name: "IX_FundingCommitments_ApplicantEntityNumber",
                table: "FundingCommitments",
                column: "ApplicantEntityNumber");

            migrationBuilder.CreateIndex(
                name: "IX_FundingCommitments_CommitmentStatus",
                table: "FundingCommitments",
                column: "CommitmentStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FundingCommitments_FundingYear",
                table: "FundingCommitments");

            migrationBuilder.DropIndex(
                name: "IX_FundingCommitments_CategoryOfService",
                table: "FundingCommitments");

            migrationBuilder.DropIndex(
                name: "IX_FundingCommitments_ServiceProviderName",
                table: "FundingCommitments");

            migrationBuilder.DropIndex(
                name: "IX_FundingCommitments_ApplicantEntityNumber",
                table: "FundingCommitments");

            migrationBuilder.DropIndex(
                name: "IX_FundingCommitments_CommitmentStatus",
                table: "FundingCommitments");
        }
    }
}
