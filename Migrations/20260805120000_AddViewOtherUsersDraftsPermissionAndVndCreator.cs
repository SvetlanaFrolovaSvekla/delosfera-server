// PLACEHOLDER — DELETE THIS FILE before running EF Core tooling.
//
// The actual C# model changes for this feature (new PermissionCode.ViewOtherUsersDrafts,
// VndDocument.CreatedByUserId + its FK config, VndService search filters) are already in
// place. What's still needed is the matching migration + snapshot, which requires the
// dotnet EF tooling (not available in this sandbox — no disk space to run dotnet here).
//
// To finish:
//   1. Delete this file.
//   2. From delosfera-server/, run:
//        dotnet ef migrations add AddViewOtherUsersDraftsPermissionAndVndCreator
//      This will auto-generate the column/FK/index on vnd_document.created_by_user_id
//      AND the role.permission_codes update for Admin/Chief-Editor roles (picked up
//      automatically since RoleConfiguration.cs computes allPermissions from the enum),
//      plus a correct .Designer.cs — all verified by your own compiler.
//   3. Run: dotnet ef database update
