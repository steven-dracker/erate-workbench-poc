using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErateWorkbench.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFundingCommitments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FundingCommitments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FundingRequestNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FrnLineItemNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    RawSourceKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ApplicantEntityNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ApplicantName = table.Column<string>(type: "TEXT", nullable: true),
                    ApplicationNumber = table.Column<string>(type: "TEXT", nullable: true),
                    FundingYear = table.Column<int>(type: "INTEGER", nullable: false),
                    ServiceProviderName = table.Column<string>(type: "TEXT", nullable: true),
                    ServiceProviderSpin = table.Column<string>(type: "TEXT", nullable: true),
                    CategoryOfService = table.Column<string>(type: "TEXT", nullable: true),
                    TypeOfService = table.Column<string>(type: "TEXT", nullable: true),
                    CommitmentStatus = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CommittedAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    TotalEligibleAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundingCommitments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FundingCommitments_RawSourceKey",
                table: "FundingCommitments",
                column: "RawSourceKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FundingCommitments");
        }
    }
}
