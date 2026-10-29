using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QatarLearningBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Already applied by earlier runs of this migration on this database:
            //  - TeacherProfiles.Qualifications and TeacherProfiles.Languages columns
            //  - TeacherAvailabilities table and its composite index
            //
            // Remaining: make the existing non-unique FK index on TeacherProfiles.UserId unique.
            // Done in ONE statement so the foreign key always has an index available.
            migrationBuilder.Sql(
                "ALTER TABLE `TeacherProfiles` " +
                "DROP INDEX `IX_TeacherProfiles_UserId`, " +
                "ADD UNIQUE INDEX `IX_TeacherProfiles_UserId` (`UserId`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE `TeacherProfiles` " +
                "DROP INDEX `IX_TeacherProfiles_UserId`, " +
                "ADD INDEX `IX_TeacherProfiles_UserId` (`UserId`);");

            migrationBuilder.DropTable(name: "TeacherAvailabilities");

            migrationBuilder.DropColumn(name: "Qualifications", table: "TeacherProfiles");
            migrationBuilder.DropColumn(name: "Languages", table: "TeacherProfiles");
        }
    }
}