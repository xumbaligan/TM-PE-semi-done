using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TM_PE.Migrations
{
    /// <inheritdoc />
    public partial class user1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_criteria",
                columns: table => new
                {
                    CriteriaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CriteriaName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RoleType = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_criteria", x => x.CriteriaId);
                });

            migrationBuilder.CreateTable(
                name: "tbl_departments",
                columns: table => new
                {
                    DepartmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_departments", x => x.DepartmentId);
                });

            migrationBuilder.CreateTable(
                name: "tbl_fiberplan",
                columns: table => new
                {
                    FiberPlanID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_fiberplan", x => x.FiberPlanID);
                });

            migrationBuilder.CreateTable(
                name: "tbl_jobticket",
                columns: table => new
                {
                    JobTicketID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    JobType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClientFullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrimaryNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SecondaryNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FiberPlan = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ServiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateOfCompletion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LocationAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    NearestLandmark = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_jobticket", x => x.JobTicketID);
                });

            migrationBuilder.CreateTable(
                name: "tbl_officetask",
                columns: table => new
                {
                    OfficeTaskID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TaskName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Progress = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_officetask", x => x.OfficeTaskID);
                });

            migrationBuilder.CreateTable(
                name: "tbl_employees",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RoleType = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    HiredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_employees", x => x.EmployeeId);
                    table.ForeignKey(
                        name: "FK_tbl_employees_tbl_departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "tbl_departments",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_jobticketreschedulehistory",
                columns: table => new
                {
                    JobTicketRescheduleHistoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobTicketID = table.Column<int>(type: "int", nullable: false),
                    OldServiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NewServiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PreviousRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DateChanged = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_jobticketreschedulehistory", x => x.JobTicketRescheduleHistoryID);
                    table.ForeignKey(
                        name: "FK_tbl_jobticketreschedulehistory_tbl_jobticket_JobTicketID",
                        column: x => x.JobTicketID,
                        principalTable: "tbl_jobticket",
                        principalColumn: "JobTicketID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_jobticketsubmissionhistory",
                columns: table => new
                {
                    JobTicketSubmissionHistoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobTicketID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DateChanged = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_jobticketsubmissionhistory", x => x.JobTicketSubmissionHistoryID);
                    table.ForeignKey(
                        name: "FK_tbl_jobticketsubmissionhistory_tbl_jobticket_JobTicketID",
                        column: x => x.JobTicketID,
                        principalTable: "tbl_jobticket",
                        principalColumn: "JobTicketID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_jobticketassignment",
                columns: table => new
                {
                    JobTicketAssignmentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobTicketID = table.Column<int>(type: "int", nullable: false),
                    EmployeeID = table.Column<int>(type: "int", nullable: false),
                    IsLeader = table.Column<bool>(type: "bit", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_jobticketassignment", x => x.JobTicketAssignmentID);
                    table.ForeignKey(
                        name: "FK_tbl_jobticketassignment_tbl_employees_EmployeeID",
                        column: x => x.EmployeeID,
                        principalTable: "tbl_employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_jobticketassignment_tbl_jobticket_JobTicketID",
                        column: x => x.JobTicketID,
                        principalTable: "tbl_jobticket",
                        principalColumn: "JobTicketID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_performanceevaluation",
                columns: table => new
                {
                    EvaluationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeID = table.Column<int>(type: "int", nullable: false),
                    EvaluatorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EvaluationPeriod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EvaluationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OverallScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    OverallRating = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GeneralRemarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EvaluationStatus = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_performanceevaluation", x => x.EvaluationID);
                    table.ForeignKey(
                        name: "FK_tbl_performanceevaluation_tbl_employees_EmployeeID",
                        column: x => x.EmployeeID,
                        principalTable: "tbl_employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_taskactivity",
                columns: table => new
                {
                    ActivityID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FeedBack = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedEmployeeID = table.Column<int>(type: "int", nullable: true),
                    OfficeTaskID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_taskactivity", x => x.ActivityID);
                    table.ForeignKey(
                        name: "FK_tbl_taskactivity_tbl_employees_AssignedEmployeeID",
                        column: x => x.AssignedEmployeeID,
                        principalTable: "tbl_employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_tbl_taskactivity_tbl_officetask_OfficeTaskID",
                        column: x => x.OfficeTaskID,
                        principalTable: "tbl_officetask",
                        principalColumn: "OfficeTaskID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_taskassignment",
                columns: table => new
                {
                    TaskAssignmentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfficeTaskID = table.Column<int>(type: "int", nullable: false),
                    EmployeeID = table.Column<int>(type: "int", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_taskassignment", x => x.TaskAssignmentID);
                    table.ForeignKey(
                        name: "FK_tbl_taskassignment_tbl_employees_EmployeeID",
                        column: x => x.EmployeeID,
                        principalTable: "tbl_employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_taskassignment_tbl_officetask_OfficeTaskID",
                        column: x => x.OfficeTaskID,
                        principalTable: "tbl_officetask",
                        principalColumn: "OfficeTaskID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_useraccount",
                columns: table => new
                {
                    UserAccountID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeID = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordSalt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_useraccount", x => x.UserAccountID);
                    table.ForeignKey(
                        name: "FK_tbl_useraccount_tbl_employees_EmployeeID",
                        column: x => x.EmployeeID,
                        principalTable: "tbl_employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_jobticketsubmission",
                columns: table => new
                {
                    JobTicketSubmissionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobTicketID = table.Column<int>(type: "int", nullable: false),
                    EmployeeID = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateSubmitted = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RescheduleHistoryID = table.Column<int>(type: "int", nullable: true),
                    SubmissionHistoryID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_jobticketsubmission", x => x.JobTicketSubmissionID);
                    table.ForeignKey(
                        name: "FK_tbl_jobticketsubmission_tbl_employees_EmployeeID",
                        column: x => x.EmployeeID,
                        principalTable: "tbl_employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_jobticketsubmission_tbl_jobticket_JobTicketID",
                        column: x => x.JobTicketID,
                        principalTable: "tbl_jobticket",
                        principalColumn: "JobTicketID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_jobticketsubmission_tbl_jobticketreschedulehistory_RescheduleHistoryID",
                        column: x => x.RescheduleHistoryID,
                        principalTable: "tbl_jobticketreschedulehistory",
                        principalColumn: "JobTicketRescheduleHistoryID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_jobticketsubmission_tbl_jobticketsubmissionhistory_SubmissionHistoryID",
                        column: x => x.SubmissionHistoryID,
                        principalTable: "tbl_jobticketsubmissionhistory",
                        principalColumn: "JobTicketSubmissionHistoryID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_appraisal",
                columns: table => new
                {
                    AppraisalID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeID = table.Column<int>(type: "int", nullable: false),
                    EvaluationID = table.Column<int>(type: "int", nullable: false),
                    AppraisalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OverallRating = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    SalaryAdjustmentRecommendation = table.Column<bool>(type: "bit", nullable: false),
                    PromotionRecommendation = table.Column<bool>(type: "bit", nullable: false),
                    TrainingRecommendation = table.Column<bool>(type: "bit", nullable: false),
                    ManagerRemarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AppraisalStatus = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "tbl_evaluationresult",
                columns: table => new
                {
                    EvaluationResultID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluationID = table.Column<int>(type: "int", nullable: false),
                    CriteriaID = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_evaluationresult", x => x.EvaluationResultID);
                    table.ForeignKey(
                        name: "FK_tbl_evaluationresult_tbl_criteria_CriteriaID",
                        column: x => x.CriteriaID,
                        principalTable: "tbl_criteria",
                        principalColumn: "CriteriaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_evaluationresult_tbl_performanceevaluation_EvaluationID",
                        column: x => x.EvaluationID,
                        principalTable: "tbl_performanceevaluation",
                        principalColumn: "EvaluationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_feedback",
                columns: table => new
                {
                    FeedbackID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeID = table.Column<int>(type: "int", nullable: false),
                    SubmittedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EvaluationID = table.Column<int>(type: "int", nullable: true),
                    FeedbackType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_feedback", x => x.FeedbackID);
                    table.ForeignKey(
                        name: "FK_tbl_feedback_tbl_employees_EmployeeID",
                        column: x => x.EmployeeID,
                        principalTable: "tbl_employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_feedback_tbl_performanceevaluation_EvaluationID",
                        column: x => x.EvaluationID,
                        principalTable: "tbl_performanceevaluation",
                        principalColumn: "EvaluationID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tbl_activitysubmission",
                columns: table => new
                {
                    SubmissionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityID = table.Column<int>(type: "int", nullable: false),
                    EmployeeID = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateSubmitted = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_activitysubmission", x => x.SubmissionID);
                    table.ForeignKey(
                        name: "FK_tbl_activitysubmission_tbl_employees_EmployeeID",
                        column: x => x.EmployeeID,
                        principalTable: "tbl_employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_activitysubmission_tbl_taskactivity_ActivityID",
                        column: x => x.ActivityID,
                        principalTable: "tbl_taskactivity",
                        principalColumn: "ActivityID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_activitysubmission_ActivityID",
                table: "tbl_activitysubmission",
                column: "ActivityID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_activitysubmission_EmployeeID",
                table: "tbl_activitysubmission",
                column: "EmployeeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_appraisal_EmployeeID",
                table: "tbl_appraisal",
                column: "EmployeeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_appraisal_EvaluationID",
                table: "tbl_appraisal",
                column: "EvaluationID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_employees_DepartmentId",
                table: "tbl_employees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_employees_Email",
                table: "tbl_employees",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_evaluationresult_CriteriaID",
                table: "tbl_evaluationresult",
                column: "CriteriaID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_evaluationresult_EvaluationID",
                table: "tbl_evaluationresult",
                column: "EvaluationID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_feedback_EmployeeID",
                table: "tbl_feedback",
                column: "EmployeeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_feedback_EvaluationID",
                table: "tbl_feedback",
                column: "EvaluationID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_fiberplan_PlanName",
                table: "tbl_fiberplan",
                column: "PlanName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_jobticketassignment_EmployeeID",
                table: "tbl_jobticketassignment",
                column: "EmployeeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_jobticketassignment_JobTicketID",
                table: "tbl_jobticketassignment",
                column: "JobTicketID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_jobticketreschedulehistory_JobTicketID",
                table: "tbl_jobticketreschedulehistory",
                column: "JobTicketID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_jobticketsubmission_EmployeeID",
                table: "tbl_jobticketsubmission",
                column: "EmployeeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_jobticketsubmission_JobTicketID",
                table: "tbl_jobticketsubmission",
                column: "JobTicketID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_jobticketsubmission_RescheduleHistoryID",
                table: "tbl_jobticketsubmission",
                column: "RescheduleHistoryID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_jobticketsubmission_SubmissionHistoryID",
                table: "tbl_jobticketsubmission",
                column: "SubmissionHistoryID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_jobticketsubmissionhistory_JobTicketID",
                table: "tbl_jobticketsubmissionhistory",
                column: "JobTicketID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_performanceevaluation_EmployeeID",
                table: "tbl_performanceevaluation",
                column: "EmployeeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_taskactivity_AssignedEmployeeID",
                table: "tbl_taskactivity",
                column: "AssignedEmployeeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_taskactivity_OfficeTaskID",
                table: "tbl_taskactivity",
                column: "OfficeTaskID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_taskassignment_EmployeeID",
                table: "tbl_taskassignment",
                column: "EmployeeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_taskassignment_OfficeTaskID",
                table: "tbl_taskassignment",
                column: "OfficeTaskID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_useraccount_EmployeeID",
                table: "tbl_useraccount",
                column: "EmployeeID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_useraccount_Username",
                table: "tbl_useraccount",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_activitysubmission");

            migrationBuilder.DropTable(
                name: "tbl_appraisal");

            migrationBuilder.DropTable(
                name: "tbl_evaluationresult");

            migrationBuilder.DropTable(
                name: "tbl_feedback");

            migrationBuilder.DropTable(
                name: "tbl_fiberplan");

            migrationBuilder.DropTable(
                name: "tbl_jobticketassignment");

            migrationBuilder.DropTable(
                name: "tbl_jobticketsubmission");

            migrationBuilder.DropTable(
                name: "tbl_taskassignment");

            migrationBuilder.DropTable(
                name: "tbl_useraccount");

            migrationBuilder.DropTable(
                name: "tbl_taskactivity");

            migrationBuilder.DropTable(
                name: "tbl_criteria");

            migrationBuilder.DropTable(
                name: "tbl_performanceevaluation");

            migrationBuilder.DropTable(
                name: "tbl_jobticketreschedulehistory");

            migrationBuilder.DropTable(
                name: "tbl_jobticketsubmissionhistory");

            migrationBuilder.DropTable(
                name: "tbl_officetask");

            migrationBuilder.DropTable(
                name: "tbl_employees");

            migrationBuilder.DropTable(
                name: "tbl_jobticket");

            migrationBuilder.DropTable(
                name: "tbl_departments");
        }
    }
}
