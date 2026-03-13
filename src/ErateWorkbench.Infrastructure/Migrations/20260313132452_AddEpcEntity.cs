using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErateWorkbench.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEpcEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EpcEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    EntityType = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ParentEntityNumber = table.Column<string>(type: "TEXT", nullable: true),
                    ParentEntityName = table.Column<string>(type: "TEXT", nullable: true),
                    PhysicalAddress = table.Column<string>(type: "TEXT", nullable: true),
                    PhysicalCity = table.Column<string>(type: "TEXT", nullable: true),
                    PhysicalCounty = table.Column<string>(type: "TEXT", nullable: true),
                    PhysicalState = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    PhysicalZip = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Website = table.Column<string>(type: "TEXT", nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    UrbanRuralStatus = table.Column<string>(type: "TEXT", nullable: true),
                    CategoryOneDiscountRate = table.Column<decimal>(type: "TEXT", nullable: true),
                    CategoryTwoDiscountRate = table.Column<decimal>(type: "TEXT", nullable: true),
                    LocaleCode = table.Column<string>(type: "TEXT", nullable: true),
                    StudentCount = table.Column<int>(type: "INTEGER", nullable: true),
                    FccRegistrationNumber = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpcEntities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EpcEntities_EntityNumber",
                table: "EpcEntities",
                column: "EntityNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EpcEntities");
        }
    }
}
