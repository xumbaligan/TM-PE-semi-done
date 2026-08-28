using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TM_PE.Migrations
{
    /// <inheritdoc />
    public partial class AddActivitySubmissionReviewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateReviewed",
                table: "tbl_activitysubmission",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Feedback",
                table: "tbl_activitysubmission",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByEmployeeID",
                table: "tbl_activitysubmission",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_activitysubmission_ReviewedByEmployeeID",
                table: "tbl_activitysubmission",
                column: "ReviewedByEmployeeID");

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_activitysubmission_tbl_employees_ReviewedByEmployeeID",
                table: "tbl_activitysubmission",
                column: "ReviewedByEmployeeID",
                principalTable: "tbl_employees",
                principalColumn: "EmployeeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbl_activitysubmission_tbl_employees_ReviewedByEmployeeID",
                table: "tbl_activitysubmission");

            migrationBuilder.DropIndex(
                name: "IX_tbl_activitysubmission_ReviewedByEmployeeID",
                table: "tbl_activitysubmission");

            migrationBuilder.DropColumn(
                name: "DateReviewed",
                table: "tbl_activitysubmission");

            migrationBuilder.DropColumn(
                name: "Feedback",
                table: "tbl_activitysubmission");

            migrationBuilder.DropColumn(
                name: "ReviewedByEmployeeID",
                table: "tbl_activitysubmission");
        }
    }
}
