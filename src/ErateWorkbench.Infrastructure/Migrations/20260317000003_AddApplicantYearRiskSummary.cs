using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErateWorkbench.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicantYearRiskSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicantYearRiskSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FundingYear = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicantEntityNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ApplicantEntityName = table.Column<string>(type: "TEXT", nullable: true),
                    TotalEligibleAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalCommittedAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    CommitmentRowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DistinctCommitmentFrnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalRequestedDisbursementAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalApprovedDisbursementAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    DisbursementRowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DistinctDisbursementFrnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DistinctInvoiceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    HasCommitmentData = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasDisbursementData = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReductionPct = table.Column<double>(type: "REAL", nullable: false),
                    DisbursementPct = table.Column<double>(type: "REAL", nullable: false),
                    RiskScore = table.Column<double>(type: "REAL", nullable: false),
                    RiskLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicantYearRiskSummaries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantYearRiskSummaries_ApplicantEntityNumber",
                table: "ApplicantYearRiskSummaries",
                column: "ApplicantEntityNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantYearRiskSummaries_FundingYear",
                table: "ApplicantYearRiskSummaries",
                column: "FundingYear");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantYearRiskSummaries_FundingYear_ApplicantEntityNumber",
                table: "ApplicantYearRiskSummaries",
                columns: new[] { "FundingYear", "ApplicantEntityNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantYearRiskSummaries_FundingYear_RiskLevel",
                table: "ApplicantYearRiskSummaries",
                columns: new[] { "FundingYear", "RiskLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantYearRiskSummaries_RiskLevel",
                table: "ApplicantYearRiskSummaries",
                column: "RiskLevel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicantYearRiskSummaries");
        }
    }
}
