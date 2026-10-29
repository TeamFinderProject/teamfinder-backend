using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TeamFinder.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_types",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "profiles",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    GitHubUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PortfolioUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_profiles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    ProjectType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExternalContactUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                    table.CheckConstraint("CK_projects_project_type", "\"ProjectType\" IN ('PetProject','Hackathon','StudyProject')");
                    table.CheckConstraint("CK_projects_status", "\"Status\" IN ('Draft','Recruiting','InProgress','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_projects_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "telegram_link_codes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_link_codes", x => x.Code);
                    table.ForeignKey(
                        name: "FK_telegram_link_codes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "telegram_links",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    LinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_links", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_telegram_links_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_role_interests",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleTypeId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_role_interests", x => new { x.UserId, x.RoleTypeId });
                    table.ForeignKey(
                        name: "FK_user_role_interests_role_types_RoleTypeId",
                        column: x => x.RoleTypeId,
                        principalTable: "role_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_role_interests_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_skills",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SkillId = table.Column<long>(type: "bigint", nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_skills", x => new { x.UserId, x.SkillId });
                    table.CheckConstraint("CK_user_skills_level", "\"Level\" IN ('Beginner','Intermediate','Advanced')");
                    table.ForeignKey(
                        name: "FK_user_skills_skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_skills_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProjectId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TelegramSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TelegramAttempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_roles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    RoleTypeId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_roles", x => x.Id);
                    table.UniqueConstraint("AK_project_roles_Id_ProjectId", x => new { x.Id, x.ProjectId });
                    table.CheckConstraint("CK_project_roles_capacity", "\"Capacity\" BETWEEN 1 AND 50");
                    table.ForeignKey(
                        name: "FK_project_roles_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_roles_role_types_RoleTypeId",
                        column: x => x.RoleTypeId,
                        principalTable: "role_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    ProjectRoleId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClosureReason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.Id);
                    table.CheckConstraint("CK_applications_closed_has_reason", "(\"Status\" = 'Closed') = (\"ClosureReason\" IS NOT NULL)");
                    table.CheckConstraint("CK_applications_closure_reason", "\"ClosureReason\" IS NULL OR \"ClosureReason\" IN ('AcceptedAnotherRole','RoleFilled','RoleClosed','RecruitmentClosed','ProjectCancelled')");
                    table.CheckConstraint("CK_applications_status", "\"Status\" IN ('Pending','Accepted','Rejected','Withdrawn','Closed')");
                    table.ForeignKey(
                        name: "FK_applications_project_roles_ProjectRoleId_ProjectId",
                        columns: x => new { x.ProjectRoleId, x.ProjectId },
                        principalTable: "project_roles",
                        principalColumns: new[] { "Id", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_applications_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_applications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    ProjectRoleId = table.Column<long>(type: "bigint", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memberships", x => x.Id);
                    table.CheckConstraint("CK_memberships_end_reason", "\"EndReason\" IS NULL OR \"EndReason\" IN ('Left','Removed')");
                    table.CheckConstraint("CK_memberships_end_state", "(\"EndedAt\" IS NULL) = (\"EndReason\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_memberships_project_roles_ProjectRoleId_ProjectId",
                        columns: x => new { x.ProjectRoleId, x.ProjectId },
                        principalTable: "project_roles",
                        principalColumns: new[] { "Id", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_memberships_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_memberships_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_skill_requirements",
                columns: table => new
                {
                    ProjectRoleId = table.Column<long>(type: "bigint", nullable: false),
                    SkillId = table.Column<long>(type: "bigint", nullable: false),
                    RequirementType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MinimumLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_skill_requirements", x => new { x.ProjectRoleId, x.SkillId });
                    table.CheckConstraint("CK_role_skill_requirements_minimum_level", "\"MinimumLevel\" IS NULL OR \"MinimumLevel\" IN ('Beginner','Intermediate','Advanced')");
                    table.CheckConstraint("CK_role_skill_requirements_type", "\"RequirementType\" IN ('Required','NiceToHave')");
                    table.ForeignKey(
                        name: "FK_role_skill_requirements_project_roles_ProjectRoleId",
                        column: x => x.ProjectRoleId,
                        principalTable: "project_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_skill_requirements_skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "role_types",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1L, "Разработчик серверной части", "Backend Developer" },
                    { 2L, "Разработчик пользовательского интерфейса", "Frontend Developer" },
                    { 3L, "Разработчик серверной и клиентской частей", "Fullstack Developer" },
                    { 4L, "Дизайнер", "Designer" },
                    { 5L, "Инженер инфраструктуры и развёртывания", "DevOps Engineer" },
                    { 6L, "Инженер по тестированию", "QA Engineer" },
                    { 7L, "Менеджер продукта", "Product Manager" },
                    { 8L, "Аналитик данных", "Data Analyst" }
                });

            migrationBuilder.InsertData(
                table: "skills",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1L, "Язык программирования", "C#" },
                    { 2L, "Платформа серверных приложений", "ASP.NET Core" },
                    { 3L, "Реляционная база данных", "PostgreSQL" },
                    { 4L, "ORM для .NET", "Entity Framework Core" },
                    { 5L, "Язык программирования", "JavaScript" },
                    { 6L, "Типизированный JavaScript", "TypeScript" },
                    { 7L, "Библиотека пользовательских интерфейсов", "React" },
                    { 8L, "Разметка и стилизация веб-страниц", "HTML/CSS" },
                    { 9L, "Контейнеризация приложений", "Docker" },
                    { 10L, "Контроль версий", "Git" },
                    { 11L, "Язык программирования", "Go" },
                    { 12L, "Хранилище данных в памяти", "Redis" },
                    { 13L, "Проектирование интерфейсов", "Figma" },
                    { 14L, "Язык программирования", "Python" },
                    { 15L, "Проектирование и разработка API", "REST API" },
                    { 16L, "Язык запросов к реляционным БД", "SQL" },
                    { 17L, "Проверка качества программ", "Тестирование ПО" },
                    { 18L, "Проектирование пользовательского опыта", "UI/UX" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_applications_ProjectId_Status",
                table: "applications",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_applications_ProjectRoleId_ProjectId",
                table: "applications",
                columns: new[] { "ProjectRoleId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_applications_UserId_ProjectRoleId",
                table: "applications",
                columns: new[] { "UserId", "ProjectRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_memberships_ProjectId",
                table: "memberships",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_ProjectRoleId_ProjectId",
                table: "memberships",
                columns: new[] { "ProjectRoleId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_memberships_UserId_ProjectId",
                table: "memberships",
                columns: new[] { "UserId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_ProjectId",
                table: "notifications",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_TelegramSentAt",
                table: "notifications",
                column: "TelegramSentAt");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_ReadAt",
                table: "notifications",
                columns: new[] { "UserId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_project_roles_ProjectId",
                table: "project_roles",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_roles_RoleTypeId",
                table: "project_roles",
                column: "RoleTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_OwnerId",
                table: "projects",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_Status",
                table: "projects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_FamilyId",
                table: "refresh_tokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_role_skill_requirements_SkillId",
                table: "role_skill_requirements",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_role_types_Name",
                table: "role_types",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_skills_Name",
                table: "skills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_link_codes_UserId",
                table: "telegram_link_codes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_telegram_links_ChatId",
                table: "telegram_links",
                column: "ChatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_role_interests_RoleTypeId",
                table: "user_role_interests",
                column: "RoleTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_user_skills_SkillId",
                table: "user_skills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "memberships");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "profiles");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "role_skill_requirements");

            migrationBuilder.DropTable(
                name: "telegram_link_codes");

            migrationBuilder.DropTable(
                name: "telegram_links");

            migrationBuilder.DropTable(
                name: "user_role_interests");

            migrationBuilder.DropTable(
                name: "user_skills");

            migrationBuilder.DropTable(
                name: "project_roles");

            migrationBuilder.DropTable(
                name: "skills");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "role_types");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
