using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TeamFinder.Api.Data;

#nullable disable

namespace TeamFinder.Api.Migrations;

[DbContext(typeof(TeamFinderDbContext))]
[Migration("20261014160000_AddProjectTypeAndMembershipHistoryAndSkillLevels")]
public sealed class AddProjectTypeAndMembershipHistoryAndSkillLevels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Add a required project type for pre-existing rows.
            -- Existing projects are provisionally classified as PetProject; confirm this mapping during review.
            ALTER TABLE projects
                ADD COLUMN "ProjectType" varchar(30) NOT NULL DEFAULT 'PetProject';
            ALTER TABLE projects
                ALTER COLUMN "ProjectType" DROP DEFAULT;

            -- Preserve membership records and add optional end-of-membership metadata.
            ALTER TABLE memberships
                ADD COLUMN "EndedAt" timestamptz NULL,
                ADD COLUMN "EndReason" varchar(20) NULL;

            ALTER TABLE memberships
                ADD CONSTRAINT "CK_memberships_end_reason"
                    CHECK ("EndReason" IS NULL OR "EndReason" IN ('Left','Removed')),
                ADD CONSTRAINT "CK_memberships_end_state"
                    CHECK (("EndedAt" IS NULL AND "EndReason" IS NULL)
                        OR ("EndedAt" IS NOT NULL AND "EndReason" IS NOT NULL));

            -- v1 used four levels. Map the former top level to the highest remaining v1 level,
            -- then replace the existing checks with the agreed three-level scale.
            UPDATE user_skills SET "Level" = 'Advanced' WHERE "Level" = 'Expert';
            UPDATE role_skill_requirements SET "MinimumLevel" = 'Advanced' WHERE "MinimumLevel" = 'Expert';

            DO $$
            DECLARE constraint_record record;
            BEGIN
                FOR constraint_record IN
                    SELECT conname
                    FROM pg_constraint
                    WHERE conrelid = 'user_skills'::regclass
                      AND contype = 'c'
                      AND pg_get_constraintdef(oid) LIKE '%Expert%'
                LOOP
                    EXECUTE format('ALTER TABLE user_skills DROP CONSTRAINT %I', constraint_record.conname);
                END LOOP;

                FOR constraint_record IN
                    SELECT conname
                    FROM pg_constraint
                    WHERE conrelid = 'role_skill_requirements'::regclass
                      AND contype = 'c'
                      AND pg_get_constraintdef(oid) LIKE '%Expert%'
                LOOP
                    EXECUTE format('ALTER TABLE role_skill_requirements DROP CONSTRAINT %I', constraint_record.conname);
                END LOOP;
            END $$;

            ALTER TABLE user_skills
                ADD CONSTRAINT "CK_user_skills_level"
                CHECK ("Level" IN ('Beginner','Intermediate','Advanced'));

            ALTER TABLE role_skill_requirements
                ADD CONSTRAINT "CK_role_skill_requirements_minimum_level"
                CHECK ("MinimumLevel" IS NULL OR "MinimumLevel" IN ('Beginner','Intermediate','Advanced'));

            ALTER TABLE projects
                ADD CONSTRAINT "CK_projects_project_type"
                CHECK ("ProjectType" IN ('PetProject','Hackathon','Educational'));
            """);
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.12")
            .HasAnnotation("Relational:MaxIdentifierLength", 63)
            .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

        TeamFinderModelV2.ConfigureModel(modelBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE projects DROP CONSTRAINT IF EXISTS "CK_projects_project_type";
            ALTER TABLE memberships DROP CONSTRAINT IF EXISTS "CK_memberships_end_state";
            ALTER TABLE memberships DROP CONSTRAINT IF EXISTS "CK_memberships_end_reason";

            ALTER TABLE user_skills DROP CONSTRAINT IF EXISTS "CK_user_skills_level";
            ALTER TABLE role_skill_requirements DROP CONSTRAINT IF EXISTS "CK_role_skill_requirements_minimum_level";

            -- Restore the former four-level checks. Values converted from Expert to Advanced stay Advanced.
            ALTER TABLE user_skills
                ADD CONSTRAINT "CK_user_skills_level"
                CHECK ("Level" IN ('Beginner','Intermediate','Advanced','Expert'));
            ALTER TABLE role_skill_requirements
                ADD CONSTRAINT "CK_role_skill_requirements_minimum_level"
                CHECK ("MinimumLevel" IS NULL OR "MinimumLevel" IN ('Beginner','Intermediate','Advanced','Expert'));

            ALTER TABLE memberships DROP COLUMN IF EXISTS "EndReason";
            ALTER TABLE memberships DROP COLUMN IF EXISTS "EndedAt";
            ALTER TABLE projects DROP COLUMN IF EXISTS "ProjectType";
            """);
    }
}
