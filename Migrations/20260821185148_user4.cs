using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TM_PE.Migrations
{
    /// <inheritdoc />
    public partial class user4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_evaluationrecommendation");

            migrationBuilder.AddColumn<string>(
                name: "Recommendation",
                table: "tbl_performanceevaluation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "StarRating",
                table: "tbl_evaluationresult",
                type: "decimal(2,1)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "tbl_recommendation",
                columns: table => new
                {
                    RecommendationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecommendationName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_recommendation", x => x.RecommendationID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_recommendation_RecommendationName",
                table: "tbl_recommendation",
                column: "RecommendationName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_recommendation");

            migrationBuilder.DropColumn(
                name: "Recommendation",
                table: "tbl_performanceevaluation");

            migrationBuilder.AlterColumn<int>(
                name: "StarRating",
                table: "tbl_evaluationresult",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(2,1)");

            migrationBuilder.CreateTable(
                name: "tbl_evaluationrecommendation",
                columns: table => new
                {
                    EvaluationRecommendationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluationID = table.Column<int>(type: "int", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Recommendation = table.Column<string>(type: "nvarchar(50)", nullable: false)
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
    }
}
