using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutumnRidgeUSA.Migrations
{
    public partial class AddDivisionSystem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Divisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Divisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDivisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    DivisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContractedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDivisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDivisions_Divisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserDivisions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Divisions",
                columns: new[] { "Id", "Description", "IsActive", "Name" },
                values: new object[] { 1, "Storage facility services", true, "Storage" });

            migrationBuilder.InsertData(
                table: "Divisions",
                columns: new[] { "Id", "Description", "IsActive", "Name" },
                values: new object[] { 2, "Construction and renovation services", true, "Contracting" });

            migrationBuilder.InsertData(
                table: "Divisions",
                columns: new[] { "Id", "Description", "IsActive", "Name" },
                values: new object[] { 3, "Property management and sales", true, "Real Estate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDivisions_DivisionId",
                table: "UserDivisions",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDivisions_UserId_DivisionId",
                table: "UserDivisions",
                columns: new[] { "UserId", "DivisionId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserDivisions");

            migrationBuilder.DropTable(
                name: "Divisions");
        }
    }
}
