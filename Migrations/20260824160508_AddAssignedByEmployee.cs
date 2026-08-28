using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TM_PE.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedByEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedByEmployeeID",
                table: "tbl_officetask",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedByEmployeeID",
                table: "tbl_jobticket",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_officetask_AssignedByEmployeeID",
                table: "tbl_officetask",
                column: "AssignedByEmployeeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_jobticket_AssignedByEmployeeID",
                table: "tbl_jobticket",
                column: "AssignedByEmployeeID");

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_jobticket_tbl_employees_AssignedByEmployeeID",
                table: "tbl_jobticket",
                column: "AssignedByEmployeeID",
                principalTable: "tbl_employees",
                principalColumn: "EmployeeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_officetask_tbl_employees_AssignedByEmployeeID",
                table: "tbl_officetask",
                column: "AssignedByEmployeeID",
                principalTable: "tbl_employees",
                principalColumn: "EmployeeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbl_jobticket_tbl_employees_AssignedByEmployeeID",
                table: "tbl_jobticket");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_officetask_tbl_employees_AssignedByEmployeeID",
                table: "tbl_officetask");

            migrationBuilder.DropIndex(
                name: "IX_tbl_officetask_AssignedByEmployeeID",
                table: "tbl_officetask");

            migrationBuilder.DropIndex(
                name: "IX_tbl_jobticket_AssignedByEmployeeID",
                table: "tbl_jobticket");

            migrationBuilder.DropColumn(
                name: "AssignedByEmployeeID",
                table: "tbl_officetask");

            migrationBuilder.DropColumn(
                name: "AssignedByEmployeeID",
                table: "tbl_jobticket");
        }
    }
}
