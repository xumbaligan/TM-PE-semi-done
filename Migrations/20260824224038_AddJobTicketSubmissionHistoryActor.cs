using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TM_PE.Migrations
{
    /// <inheritdoc />
    public partial class AddJobTicketSubmissionHistoryActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActorEmployeeID",
                table: "tbl_jobticketsubmissionhistory",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_jobticketsubmissionhistory_ActorEmployeeID",
                table: "tbl_jobticketsubmissionhistory",
                column: "ActorEmployeeID");

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_jobticketsubmissionhistory_tbl_employees_ActorEmployeeID",
                table: "tbl_jobticketsubmissionhistory",
                column: "ActorEmployeeID",
                principalTable: "tbl_employees",
                principalColumn: "EmployeeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbl_jobticketsubmissionhistory_tbl_employees_ActorEmployeeID",
                table: "tbl_jobticketsubmissionhistory");

            migrationBuilder.DropIndex(
                name: "IX_tbl_jobticketsubmissionhistory_ActorEmployeeID",
                table: "tbl_jobticketsubmissionhistory");

            migrationBuilder.DropColumn(
                name: "ActorEmployeeID",
                table: "tbl_jobticketsubmissionhistory");
        }
    }
}
