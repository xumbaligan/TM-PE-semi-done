using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TM_PE.Migrations
{
    /// <inheritdoc />
    public partial class user3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.RenameColumn(
                name: "GeneralRemarks",
                table: "tbl_performanceevaluation",
                newName: "GeneralFeedback");

            migrationBuilder.RenameColumn(
                name: "Remarks",
                table: "tbl_evaluationresult",
                newName: "Feedback");

            migrationBuilder.AddColumn<int>(
                name: "StarRating",
                table: "tbl_evaluationresult",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "tbl_evaluationrecommendation",
                columns: table => new
                {
                    EvaluationRecommendationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluationID = table.Column<int>(type: "int", nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_evaluationrecommendation", x => x.EvaluationRecommendationID);
                    table.ForeignKey(
                        name: "FK_tbl_evaluationrecommendation_tbl_performanceevaluation_EvaluationID",
                        column: x => x.EvaluationID,
                        principalTable: "tbl_performanceevaluation",
                        principalColumn: "EvaluationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_evaluationrecommendation_EvaluationID",
                table: "tbl_evaluationrecommendation",
                column: "EvaluationID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_evaluationrecommendation");

            migrationBuilder.DropColumn(
                name: "StarRating",
                table: "tbl_evaluationresult");

            migrationBuilder.RenameColumn(
                name: "GeneralFeedback",
                table: "tbl_performanceevaluation",
                newName: "GeneralRemarks");

            migrationBuilder.RenameColumn(
                name: "Feedback",
                table: "tbl_evaluationresult",
                newName: "Remarks");

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
    }
}
