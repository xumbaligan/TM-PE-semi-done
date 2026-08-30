using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TM_PE.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_notifications",
                columns: table => new
                {
                    NotificationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeID = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_notifications", x => x.NotificationID);
                    table.ForeignKey(
                        name: "FK_tbl_notifications_tbl_employees_EmployeeID",
                        column: x => x.EmployeeID,
                        principalTable: "tbl_employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_notifications_EmployeeID_IsRead",
                table: "tbl_notifications",
                columns: new[] { "EmployeeID", "IsRead" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_notifications");
        }
    }
}
