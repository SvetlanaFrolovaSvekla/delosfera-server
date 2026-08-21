// PLACEHOLDER — DELETE THIS FILE before running EF Core tooling.
//
// The actual C# model change for this feature (VndActualizationRecord.ConsolidationStartedAt,
// stamped in VndService.AddRedactionAsync and VndApprovalService.FinalizeApprovalAsync whenever
// the document enters Consolidation as part of an actualization cycle, and used by
// VndService.ApplyLinkedToMeFilter for the "pastConsolidator" relation) is already in place.
// What's still needed is the matching migration + snapshot, which requires the dotnet EF
// tooling (not available in this sandbox — no disk space to run dotnet here).
//
// To finish:
//   1. Delete this file.
//   2. From delosfera-server/, run:
//        dotnet ef migrations add AddConsolidationStartedAtToVndActualizationRecord
//      This will auto-generate an AddColumn call for a nullable
//      vnd_actualization_record.consolidation_started_at (timestamp with time zone) column,
//      plus a correct .Designer.cs, all verified by your own compiler.
//   3. Run: dotnet ef database update
