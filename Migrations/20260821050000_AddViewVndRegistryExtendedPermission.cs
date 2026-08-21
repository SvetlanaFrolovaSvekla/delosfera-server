// PLACEHOLDER — DELETE THIS FILE before running EF Core tooling.
//
// The actual C# model changes for this feature (new PermissionCode.ViewVndRegistryExtended
// and the updated RoleConfiguration.cs seed data for "Редактор ВНД") are already in place.
// What's still needed is the matching migration + snapshot, which requires the dotnet EF
// tooling (not available in this sandbox — no disk space to run dotnet here).
//
// To finish:
//   1. Delete this file.
//   2. From delosfera-server/, run:
//        dotnet ef migrations add AddViewVndRegistryExtendedPermission
//      This will auto-generate the role.permission_codes UpdateData calls for roles 1
//      ("Администратор"), 3 ("Редактор ВНД") and 4 ("Главный редактор ВНД") — picked up
//      automatically since RoleConfiguration.cs computes allPermissions from the enum and
//      vndEditorPermissions now includes ViewVndRegistryExtended — plus a correct
//      .Designer.cs, all verified by your own compiler.
//   3. Run: dotnet ef database update
