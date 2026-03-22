using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErateWorkbench.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultantTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsultantApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RawSourceKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ApplicationNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FundingYear = table.Column<int>(type: "INTEGER", nullable: false),
                    FormVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsCertifiedInWindow = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ApplicantEpcOrganizationId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    OrganizationName = table.Column<string>(type: "TEXT", nullable: true),
                    ApplicantType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ApplicantState = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    ContactEmail = table.Column<string>(type: "TEXT", nullable: true),
                    ConsultantEpcOrganizationId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ConsultantName = table.Column<string>(type: "TEXT", nullable: true),
                    ConsultantCity = table.Column<string>(type: "TEXT", nullable: true),
                    ConsultantState = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    ConsultantZipCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    ConsultantPhone = table.Column<string>(type: "TEXT", nullable: true),
                    ConsultantEmail = table.Column<string>(type: "TEXT", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultantApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsultantFrnStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RawSourceKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    FundingRequestNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ApplicationNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FundingYear = table.Column<int>(type: "INTEGER", nullable: false),
                    FormVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsCertifiedInWindow = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Nickname = table.Column<string>(type: "TEXT", nullable: true),
                    ApplicantState = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    Ben = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    OrganizationName = table.Column<string>(type: "TEXT", nullable: true),
                    OrganizationEntityTypeName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "TEXT", nullable: true),
                    ConsultantEpcOrganizationId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ConsultantName = table.Column<string>(type: "TEXT", nullable: true),
                    ServiceTypeName = table.Column<string>(type: "TEXT", nullable: true),
                    ContractTypeName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SpinName = table.Column<string>(type: "TEXT", nullable: true),
                    FrnStatusName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PendingReason = table.Column<string>(type: "TEXT", nullable: true),
                    InvoicingMode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    DiscountPct = table.Column<decimal>(type: "TEXT", nullable: true),
                    TotalPreDiscountCosts = table.Column<decimal>(type: "TEXT", nullable: true),
                    FundingCommitmentRequest = table.Column<decimal>(type: "TEXT", nullable: true),
                    TotalAuthorizedDisbursement = table.Column<decimal>(type: "TEXT", nullable: true),
                    ServiceStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FcdlLetterDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultantFrnStatuses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantApplications_ApplicationNumber",
                table: "ConsultantApplications",
                column: "ApplicationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantApplications_ConsultantEpcOrganizationId",
                table: "ConsultantApplications",
                column: "ConsultantEpcOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantApplications_FundingYear_ConsultantEpcOrganizationId",
                table: "ConsultantApplications",
                columns: new[] { "FundingYear", "ConsultantEpcOrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantApplications_RawSourceKey",
                table: "ConsultantApplications",
                column: "RawSourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantFrnStatuses_ApplicationNumber",
                table: "ConsultantFrnStatuses",
                column: "ApplicationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantFrnStatuses_ConsultantEpcOrganizationId",
                table: "ConsultantFrnStatuses",
                column: "ConsultantEpcOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantFrnStatuses_FundingRequestNumber",
                table: "ConsultantFrnStatuses",
                column: "FundingRequestNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantFrnStatuses_FundingYear_ConsultantEpcOrganizationId",
                table: "ConsultantFrnStatuses",
                columns: new[] { "FundingYear", "ConsultantEpcOrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantFrnStatuses_RawSourceKey",
                table: "ConsultantFrnStatuses",
                column: "RawSourceKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsultantApplications");

            migrationBuilder.DropTable(
                name: "ConsultantFrnStatuses");
        }
    }
}
