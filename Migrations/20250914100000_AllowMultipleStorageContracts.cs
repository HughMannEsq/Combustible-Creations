using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutumnRidgeUSA.Migrations
{
    public partial class AllowMultipleStorageContracts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StorageContracts_UserId",
                table: "StorageContracts");

            migrationBuilder.CreateIndex(
                name: "IX_StorageContracts_UserId",
                table: "StorageContracts",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StorageContracts_UserId",
                table: "StorageContracts");

            migrationBuilder.CreateIndex(
                name: "IX_StorageContracts_UserId",
                table: "StorageContracts",
                column: "UserId",
                unique: true);
        }
    }
}
