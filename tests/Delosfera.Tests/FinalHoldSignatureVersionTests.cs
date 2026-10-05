using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;

namespace Delosfera.Tests;

/// <summary>
/// Все подписи Листа согласования — под одной итоговой версией текста. Чистые функции, база не нужна:
/// <see cref="FinalHoldCarryOver"/> (кому на финальной выдержке можно засчитать прежнее согласование,
/// а у кого запросить подпись под итоговым текстом) и <see cref="ApprovalSheetRowBuilder"/> (штамп ЭЦП
/// только под итоговой версией).
/// </summary>
public class FinalHoldSignatureVersionTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private static readonly RedactionTextVersion V1 = new(101, 201, null);
    private static readonly RedactionTextVersion V2 = new(102, 201, null);

    // --- FinalHoldCarryOver

    [Fact]
    public void NoSnapshots_DecisionIsOnCurrentText() =>
        Assert.True(FinalHoldCarryOver.DecidedOnCurrentText(T0, V1, []));

    [Fact]
    public void TextReplacedAfterDecision_NeedsNewSignature()
    {
        // Согласовал v1, затем при доработке инициатор заменил русский файл → действует v2.
        var snapshots = new[] { new RedactionTextSnapshot(T0.AddHours(1), V1) };

        Assert.False(FinalHoldCarryOver.DecidedOnCurrentText(T0, V2, snapshots));
    }

    [Fact]
    public void ResubmittedWithoutReplacingText_KeepsDecision()
    {
        // Повторная отправка без замены файлов: снимок есть, но текст тот же.
        var snapshots = new[] { new RedactionTextSnapshot(T0.AddHours(1), V1) };

        Assert.True(FinalHoldCarryOver.DecidedOnCurrentText(T0, V1, snapshots));
    }

    [Fact]
    public void DecisionAfterLastReplacement_IsOnCurrentText()
    {
        var snapshots = new[] { new RedactionTextSnapshot(T0.AddHours(1), V1) };

        Assert.True(FinalHoldCarryOver.DecidedOnCurrentText(T0.AddHours(2), V2, snapshots));
    }

    [Fact]
    public void TextAt_TakesFirstSnapshotAfterDecision_RegardlessOfOrder()
    {
        var v3 = new RedactionTextVersion(103, 201, null);
        // v1 → (снимок v1) → v2 → (снимок v2) → v3. Решение — между снимками, т.е. по v2.
        var snapshots = new[]
        {
            new RedactionTextSnapshot(T0.AddHours(3), V2),
            new RedactionTextSnapshot(T0.AddHours(1), V1),
        };

        Assert.Equal(V2, FinalHoldCarryOver.TextAt(T0.AddHours(2), v3, snapshots));
        Assert.False(FinalHoldCarryOver.DecidedOnCurrentText(T0.AddHours(2), v3, snapshots));
    }

    [Fact]
    public void RemovedLanguageVersion_CountsAsChange()
    {
        // Кыргызский файл убран — это тоже другой текст (он входит в подписанный отпечаток).
        var withoutKg = V1 with { KgFileId = null };
        var snapshots = new[] { new RedactionTextSnapshot(T0.AddHours(1), V1) };

        Assert.False(FinalHoldCarryOver.DecidedOnCurrentText(T0, withoutKg, snapshots));
    }

    [Fact]
    public void UnknownDecisionTime_IsNotCarriedOver() =>
        Assert.False(FinalHoldCarryOver.DecidedOnCurrentText(null, V1, []));

    // --- ApprovalSheetRowBuilder

    private const string DigestV1 = "ru=AAAA;kg=-;en=-";
    private const string DigestV2 = "ru=BBBB;kg=-;en=-";

    private static (VndApprovalProcess Process, Dictionary<int, User> Users) CarriedOverProcess()
    {
        // Согласующий чисто согласовал на первичном этапе, на финальной выдержке согласование
        // перенесено автоматически.
        var stage = new VndApprovalStage
        {
            Id = 7,
            Order = 1,
            ApproverUserId = 5,
            PrimaryDecision = ApprovalStageDecision.Approved,
            PrimaryDecidedAt = T0,
            FinalHoldDecision = ApprovalStageDecision.Approved,
            FinalHoldComment = "Согласовано автоматически",
            FinalHoldDecidedAt = T0.AddDays(2),
        };
        var process = new VndApprovalProcess { Id = 1, Stages = [stage] };
        var users = new Dictionary<int, User>
        {
            [5] = new() { Id = 5, FullName = "Иванов И.И.", Email = "i@bank.kg", PasswordHash = "x" },
        };
        return (process, users);
    }

    private static Dictionary<int, List<VndResolutionSignature>> PrimarySignature(string digest) => new()
    {
        [7] =
        [
            new VndResolutionSignature
            {
                StageId = 7,
                Phase = ApprovalStagePhase.Primary,
                Decision = ApprovalStageDecision.Approved,
                SignedAt = T0,
                PersonIdnp = "21234567890123",
                PersonFio = "Иванов Иван Иванович",
                HashSchemaVersion = "v2",
                Hash = new string('A', 64),
                CommentText = "Согласовано",
                CommentHash = new string('B', 64),
                ContentDigest = digest,
                VndCode = "10062",
                SignatureBase64 = "AA==",
                SignatureSha256 = new string('C', 64),
            },
        ],
    };

    [Fact]
    public void Sheet_StampsSignatureUnderFinalText()
    {
        var (process, users) = CarriedOverProcess();

        var row = Assert.Single(ApprovalSheetRowBuilder.Build(
            process, users, PrimarySignature(DigestV1), TimeZoneInfo.Utc,
            new ApprovalSheetReference(DigestV1, "10062")));

        Assert.NotNull(row.Stamp);
        Assert.Null(row.SignatureNote);
        // Дата в листе — момент реальной подписи, а не переноса на выдержку.
        Assert.Equal(T0, row.DecidedAtLocal);
    }

    [Fact]
    public void Sheet_DoesNotStampSignatureUnderOtherText()
    {
        var (process, users) = CarriedOverProcess();

        var row = Assert.Single(ApprovalSheetRowBuilder.Build(
            process, users, PrimarySignature(DigestV1), TimeZoneInfo.Utc,
            new ApprovalSheetReference(DigestV2, "10062")));

        Assert.Null(row.Stamp);
        Assert.Equal(ApprovalSheetRowBuilder.OtherVersionNote, row.SignatureNote);
    }

    [Fact]
    public void Sheet_InitiatorStage_IsMarkedAsInitiator()
    {
        var (process, users) = CarriedOverProcess();
        process.InitiatorUserId = 5; // согласующий этапа — сам инициатор, подписи под решением нет

        var row = Assert.Single(ApprovalSheetRowBuilder.Build(
            process, users, new Dictionary<int, List<VndResolutionSignature>>(), TimeZoneInfo.Utc,
            new ApprovalSheetReference(DigestV1, "10062")));

        Assert.Null(row.Stamp);
        Assert.Equal(ApprovalSheetRowBuilder.InitiatorAutoNote, row.SignatureNote);
    }

    [Fact]
    public void Sheet_OtherApproverAutoApproved_IsNotMarkedAsInitiator()
    {
        var (process, users) = CarriedOverProcess();
        process.InitiatorUserId = 99;

        var row = Assert.Single(ApprovalSheetRowBuilder.Build(
            process, users, new Dictionary<int, List<VndResolutionSignature>>(), TimeZoneInfo.Utc,
            new ApprovalSheetReference(DigestV1, "10062")));

        Assert.Equal("Без ЭЦП — согласовано автоматически", row.SignatureNote);
    }

    [Fact]
    public void Sheet_DoesNotStampSignatureUnderOtherVndCode()
    {
        var (process, users) = CarriedOverProcess();

        var row = Assert.Single(ApprovalSheetRowBuilder.Build(
            process, users, PrimarySignature(DigestV1), TimeZoneInfo.Utc,
            new ApprovalSheetReference(DigestV1, "10063")));

        Assert.Null(row.Stamp);
        Assert.Equal(ApprovalSheetRowBuilder.OtherVersionNote, row.SignatureNote);
    }
}
