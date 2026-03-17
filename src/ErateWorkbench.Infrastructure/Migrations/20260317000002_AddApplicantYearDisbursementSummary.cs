using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErateWorkbench.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicantYearDisbursementSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicantYearDisbursementSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FundingYear = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicantEntityNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ApplicantEntityName = table.Column<string>(type: "TEXT", nullable: true),
                    TotalRequestedAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalApprovedAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    DisbursementRowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DistinctFrnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DistinctInvoiceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicantYearDisbursementSummaries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantYearDisbursementSummaries_FundingYear",
                table: "ApplicantYearDisbursementSummaries",
                column: "FundingYear");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantYearDisbursementSummaries_ApplicantEntityNumber",
                table: "ApplicantYearDisbursementSummaries",
                column: "ApplicantEntityNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantYearDisbursementSummaries_FundingYear_ApplicantEntityNumber",
                table: "ApplicantYearDisbursementSummaries",
                columns: new[] { "FundingYear", "ApplicantEntityNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicantYearDisbursementSummaries");
        }
    }
}
