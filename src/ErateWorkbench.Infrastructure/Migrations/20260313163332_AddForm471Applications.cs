using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErateWorkbench.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForm471Applications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Form471Applications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FundingYear = table.Column<int>(type: "INTEGER", nullable: false),
                    RawSourceKey = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ApplicantEntityNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ApplicantName = table.Column<string>(type: "TEXT", nullable: true),
                    ApplicantState = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    CategoryOfService = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ServiceType = table.Column<string>(type: "TEXT", nullable: true),
                    RequestedAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    ApplicationStatus = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Form471Applications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Form471Applications_RawSourceKey",
                table: "Form471Applications",
                column: "RawSourceKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Form471Applications");
        }
    }
}
