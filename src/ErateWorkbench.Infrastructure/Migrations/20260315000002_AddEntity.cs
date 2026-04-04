using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErateWorkbench.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Entities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    UrbanRuralStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    FullTimeStudentCount = table.Column<int>(type: "INTEGER", nullable: true),
                    PartTimeStudentCount = table.Column<int>(type: "INTEGER", nullable: true),
                    NslpStudentCount = table.Column<int>(type: "INTEGER", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Entities_EntityNumber",
                table: "Entities",
                column: "EntityNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entities_State",
                table: "Entities",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_Entities_EntityType",
                table: "Entities",
                column: "EntityType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Entities");
        }
    }
}
