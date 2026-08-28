using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TM_PE.Migrations
{
    /// <inheritdoc />
    public partial class user2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_appraisal");

            migrationBuilder.AddColumn<bool>(
                name: "PromotionRecommendation",
                table: "tbl_performanceevaluation",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Recommendation",
                table: "tbl_performanceevaluation",
                type: "nvarchar(50)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "SalaryAdjustmentRecommendation",
                table: "tbl_performanceevaluation",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TrainingRecommendation",
                table: "tbl_performanceevaluation",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromotionRecommendation",
                table: "tbl_performanceevaluation");

            migrationBuilder.DropColumn(
                name: "Recommendation",
                table: "tbl_performanceevaluation");

            migrationBuilder.DropColumn(
                name: "SalaryAdjustmentRecommendation",
                table: "tbl_performanceevaluation");

            migrationBuilder.DropColumn(
                name: "TrainingRecommendation",
                table: "tbl_performanceevaluation");

            migrationBuilder.CreateTable(
                name: "tbl_appraisal",
                columns: table => new
                {
                    AppraisalID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeID = table.Column<int>(type: "int", nullable: false),
                    EvaluationID = table.Column<int>(type: "int", nullable: false),
                    AppraisalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppraisalStatus = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ManagerRemarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OverallRating = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PromotionRecommendation = table.Column<bool>(type: "bit", nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    SalaryAdjustmentRecommendation = table.Column<bool>(type: "bit", nullable: false),
                    TrainingRecommendation = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_appraisal", x => x.AppraisalID);
                    table.ForeignKey(
                        name: "FK_tbl_appraisal_tbl_employees_EmployeeID",
                        column: x => x.EmployeeID,
                        principalTable: "tbl_employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_appraisal_tbl_performanceevaluation_EvaluationID",
                        column: x => x.EvaluationID,
                        principalTable: "tbl_performanceevaluation",
                        principalColumn: "EvaluationID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_appraisal_EmployeeID",
                table: "tbl_appraisal",
                column: "EmployeeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_appraisal_EvaluationID",
                table: "tbl_appraisal",
                column: "EvaluationID");
        }
    }
}
