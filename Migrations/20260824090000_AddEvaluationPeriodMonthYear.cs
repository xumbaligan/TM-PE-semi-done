using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TM_PE.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationPeriodMonthYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EvaluationPeriodMonth",
                table: "tbl_performanceevaluation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EvaluationPeriodYear",
                table: "tbl_performanceevaluation",
                type: "int",
                nullable: true);

            // Best-effort backfill from the old free-text period (e.g. "August
            // 2026") into the new structured columns. Anything that doesn't
            // parse as a month/year falls back to this evaluation's
            // EvaluationDate instead of being left blank.
            migrationBuilder.Sql(@"
                UPDATE tbl_performanceevaluation
                SET EvaluationPeriodMonth = COALESCE(MONTH(TRY_PARSE(EvaluationPeriod AS date USING 'en-US')), MONTH(EvaluationDate)),
                    EvaluationPeriodYear = COALESCE(YEAR(TRY_PARSE(EvaluationPeriod AS date USING 'en-US')), YEAR(EvaluationDate));
            ");

            migrationBuilder.AlterColumn<int>(
                name: "EvaluationPeriodMonth",
                table: "tbl_performanceevaluation",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EvaluationPeriodYear",
                table: "tbl_performanceevaluation",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "EvaluationPeriod",
                table: "tbl_performanceevaluation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvaluationPeriod",
                table: "tbl_performanceevaluation",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE tbl_performanceevaluation
                SET EvaluationPeriod = FORMAT(DATEFROMPARTS(EvaluationPeriodYear, EvaluationPeriodMonth, 1), 'MMMM yyyy', 'en-US');
            ");

            migrationBuilder.DropColumn(
                name: "EvaluationPeriodMonth",
                table: "tbl_performanceevaluation");

            migrationBuilder.DropColumn(
                name: "EvaluationPeriodYear",
                table: "tbl_performanceevaluation");
        }
    }
}
