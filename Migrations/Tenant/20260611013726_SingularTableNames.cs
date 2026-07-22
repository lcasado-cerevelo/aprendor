using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingPlatform.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class SingularTableNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserGroups",
                table: "UserGroups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserGroupMembers",
                table: "UserGroupMembers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrainingVersions",
                table: "TrainingVersions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrainingSets",
                table: "TrainingSets");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrainingSetExclusions",
                table: "TrainingSetExclusions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Trainings",
                table: "Trainings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrainingItems",
                table: "TrainingItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Sessions",
                table: "Sessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MediaAssets",
                table: "MediaAssets");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ItemResponses",
                table: "ItemResponses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Categories",
                table: "Categories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AuditLogs",
                table: "AuditLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Attempts",
                table: "Attempts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Assignments",
                table: "Assignments");

            migrationBuilder.RenameTable(
                name: "UserGroups",
                newName: "UserGroup");

            migrationBuilder.RenameTable(
                name: "UserGroupMembers",
                newName: "UserGroupMember");

            migrationBuilder.RenameTable(
                name: "TrainingVersions",
                newName: "TrainingVersion");

            migrationBuilder.RenameTable(
                name: "TrainingSets",
                newName: "TrainingSet");

            migrationBuilder.RenameTable(
                name: "TrainingSetExclusions",
                newName: "TrainingSetExclusion");

            migrationBuilder.RenameTable(
                name: "Trainings",
                newName: "Training");

            migrationBuilder.RenameTable(
                name: "TrainingItems",
                newName: "TrainingItem");

            migrationBuilder.RenameTable(
                name: "Sessions",
                newName: "Session");

            migrationBuilder.RenameTable(
                name: "MediaAssets",
                newName: "MediaAsset");

            migrationBuilder.RenameTable(
                name: "ItemResponses",
                newName: "ItemResponse");

            migrationBuilder.RenameTable(
                name: "Categories",
                newName: "Category");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                newName: "AuditLog");

            migrationBuilder.RenameTable(
                name: "Attempts",
                newName: "Attempt");

            migrationBuilder.RenameTable(
                name: "Assignments",
                newName: "Assignment");

            migrationBuilder.RenameIndex(
                name: "IX_UserGroupMembers_UserGroupId_UserId",
                table: "UserGroupMember",
                newName: "IX_UserGroupMember_UserGroupId_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_TrainingVersions_TrainingId_VersionNumber",
                table: "TrainingVersion",
                newName: "IX_TrainingVersion_TrainingId_VersionNumber");

            migrationBuilder.RenameIndex(
                name: "IX_TrainingSets_TrainingId",
                table: "TrainingSet",
                newName: "IX_TrainingSet_TrainingId");

            migrationBuilder.RenameIndex(
                name: "IX_TrainingSetExclusions_SetId",
                table: "TrainingSetExclusion",
                newName: "IX_TrainingSetExclusion_SetId");

            migrationBuilder.RenameIndex(
                name: "IX_Trainings_CategoryId",
                table: "Training",
                newName: "IX_Training_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_TrainingItems_TrainingVersionId",
                table: "TrainingItem",
                newName: "IX_TrainingItem_TrainingVersionId");

            migrationBuilder.RenameIndex(
                name: "IX_TrainingItems_StableKey",
                table: "TrainingItem",
                newName: "IX_TrainingItem_StableKey");

            migrationBuilder.RenameIndex(
                name: "IX_Sessions_Code",
                table: "Session",
                newName: "IX_Session_Code");

            migrationBuilder.RenameIndex(
                name: "IX_ItemResponses_AttemptId",
                table: "ItemResponse",
                newName: "IX_ItemResponse_AttemptId");

            migrationBuilder.RenameIndex(
                name: "IX_Attempts_TrainingVersionId_UserId",
                table: "Attempt",
                newName: "IX_Attempt_TrainingVersionId_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Attempts_SessionId",
                table: "Attempt",
                newName: "IX_Attempt_SessionId");

            migrationBuilder.RenameIndex(
                name: "IX_Assignments_TrainingId",
                table: "Assignment",
                newName: "IX_Assignment_TrainingId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserGroup",
                table: "UserGroup",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserGroupMember",
                table: "UserGroupMember",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrainingVersion",
                table: "TrainingVersion",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrainingSet",
                table: "TrainingSet",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrainingSetExclusion",
                table: "TrainingSetExclusion",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Training",
                table: "Training",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrainingItem",
                table: "TrainingItem",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Session",
                table: "Session",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MediaAsset",
                table: "MediaAsset",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ItemResponse",
                table: "ItemResponse",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Category",
                table: "Category",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AuditLog",
                table: "AuditLog",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Attempt",
                table: "Attempt",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Assignment",
                table: "Assignment",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserGroupMember",
                table: "UserGroupMember");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserGroup",
                table: "UserGroup");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrainingVersion",
                table: "TrainingVersion");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrainingSetExclusion",
                table: "TrainingSetExclusion");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrainingSet",
                table: "TrainingSet");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrainingItem",
                table: "TrainingItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Training",
                table: "Training");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Session",
                table: "Session");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MediaAsset",
                table: "MediaAsset");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ItemResponse",
                table: "ItemResponse");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Category",
                table: "Category");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AuditLog",
                table: "AuditLog");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Attempt",
                table: "Attempt");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Assignment",
                table: "Assignment");

            migrationBuilder.RenameTable(
                name: "UserGroupMember",
                newName: "UserGroupMembers");

            migrationBuilder.RenameTable(
                name: "UserGroup",
                newName: "UserGroups");

            migrationBuilder.RenameTable(
                name: "TrainingVersion",
                newName: "TrainingVersions");

            migrationBuilder.RenameTable(
                name: "TrainingSetExclusion",
                newName: "TrainingSetExclusions");

            migrationBuilder.RenameTable(
                name: "TrainingSet",
                newName: "TrainingSets");

            migrationBuilder.RenameTable(
                name: "TrainingItem",
                newName: "TrainingItems");

            migrationBuilder.RenameTable(
                name: "Training",
                newName: "Trainings");

            migrationBuilder.RenameTable(
                name: "Session",
                newName: "Sessions");

            migrationBuilder.RenameTable(
                name: "MediaAsset",
                newName: "MediaAssets");

            migrationBuilder.RenameTable(
                name: "ItemResponse",
                newName: "ItemResponses");

            migrationBuilder.RenameTable(
                name: "Category",
                newName: "Categories");

            migrationBuilder.RenameTable(
                name: "AuditLog",
                newName: "AuditLogs");

            migrationBuilder.RenameTable(
                name: "Attempt",
                newName: "Attempts");

            migrationBuilder.RenameTable(
                name: "Assignment",
                newName: "Assignments");

            migrationBuilder.RenameIndex(
                name: "IX_UserGroupMember_UserGroupId_UserId",
                table: "UserGroupMembers",
                newName: "IX_UserGroupMembers_UserGroupId_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_TrainingVersion_TrainingId_VersionNumber",
                table: "TrainingVersions",
                newName: "IX_TrainingVersions_TrainingId_VersionNumber");

            migrationBuilder.RenameIndex(
                name: "IX_TrainingSetExclusion_SetId",
                table: "TrainingSetExclusions",
                newName: "IX_TrainingSetExclusions_SetId");

            migrationBuilder.RenameIndex(
                name: "IX_TrainingSet_TrainingId",
                table: "TrainingSets",
                newName: "IX_TrainingSets_TrainingId");

            migrationBuilder.RenameIndex(
                name: "IX_TrainingItem_TrainingVersionId",
                table: "TrainingItems",
                newName: "IX_TrainingItems_TrainingVersionId");

            migrationBuilder.RenameIndex(
                name: "IX_TrainingItem_StableKey",
                table: "TrainingItems",
                newName: "IX_TrainingItems_StableKey");

            migrationBuilder.RenameIndex(
                name: "IX_Training_CategoryId",
                table: "Trainings",
                newName: "IX_Trainings_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Session_Code",
                table: "Sessions",
                newName: "IX_Sessions_Code");

            migrationBuilder.RenameIndex(
                name: "IX_ItemResponse_AttemptId",
                table: "ItemResponses",
                newName: "IX_ItemResponses_AttemptId");

            migrationBuilder.RenameIndex(
                name: "IX_Attempt_TrainingVersionId_UserId",
                table: "Attempts",
                newName: "IX_Attempts_TrainingVersionId_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Attempt_SessionId",
                table: "Attempts",
                newName: "IX_Attempts_SessionId");

            migrationBuilder.RenameIndex(
                name: "IX_Assignment_TrainingId",
                table: "Assignments",
                newName: "IX_Assignments_TrainingId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserGroupMembers",
                table: "UserGroupMembers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserGroups",
                table: "UserGroups",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrainingVersions",
                table: "TrainingVersions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrainingSetExclusions",
                table: "TrainingSetExclusions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrainingSets",
                table: "TrainingSets",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrainingItems",
                table: "TrainingItems",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Trainings",
                table: "Trainings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Sessions",
                table: "Sessions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MediaAssets",
                table: "MediaAssets",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ItemResponses",
                table: "ItemResponses",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Categories",
                table: "Categories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AuditLogs",
                table: "AuditLogs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Attempts",
                table: "Attempts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Assignments",
                table: "Assignments",
                column: "Id");
        }
    }
}
