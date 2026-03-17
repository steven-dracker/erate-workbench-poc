using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErateWorkbench.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicantYearCommitmentSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicantYearCommitmentSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FundingYear = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicantEntityNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ApplicantEntityName = table.Column<string>(type: "TEXT", nullable: true),
                    TotalEligibleAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalCommittedAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    CommitmentRowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DistinctFrnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicantYearCommitmentSummaries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantYearCommitmentSummaries_FundingYear",
                table: "ApplicantYearCommitmentSummaries",
                column: "FundingYear");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantYearCommitmentSummaries_ApplicantEntityNumber",
                table: "ApplicantYearCommitmentSummaries",
                column: "ApplicantEntityNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantYearCommitmentSummaries_FundingYear_ApplicantEntityNumber",
                table: "ApplicantYearCommitmentSummaries",
                columns: new[] { "FundingYear", "ApplicantEntityNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicantYearCommitmentSummaries");
        }
    }
}
