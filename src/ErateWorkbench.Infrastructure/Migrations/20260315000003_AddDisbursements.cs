using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErateWorkbench.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDisbursements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Disbursements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RawSourceKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    FundingRequestNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    InvoiceId = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    InvoiceLineNumber = table.Column<string>(type: "TEXT", nullable: true),
                    InvoiceType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    InvoiceLineStatus = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ApplicationNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ApplicantEntityNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ApplicantEntityName = table.Column<string>(type: "TEXT", nullable: true),
                    ServiceProviderSpin = table.Column<string>(type: "TEXT", maxLength: 15, nullable: true),
                    ServiceProviderName = table.Column<string>(type: "TEXT", nullable: true),
                    FundingYear = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryOfService = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RequestedAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    InvoiceReceivedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LineCompletionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disbursements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Disbursements_RawSourceKey",
                table: "Disbursements",
                column: "RawSourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disbursements_FundingRequestNumber",
                table: "Disbursements",
                column: "FundingRequestNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Disbursements_FundingYear",
                table: "Disbursements",
                column: "FundingYear");

            migrationBuilder.CreateIndex(
                name: "IX_Disbursements_ApplicantEntityNumber",
                table: "Disbursements",
                column: "ApplicantEntityNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Disbursements_ServiceProviderSpin",
                table: "Disbursements",
                column: "ServiceProviderSpin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Disbursements");
        }
    }
}
