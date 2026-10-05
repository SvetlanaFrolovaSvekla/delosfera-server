using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Documents.VND.Services;

namespace Delosfera.Tests;

/// <summary>Проверка подлинности согласования ВНД по файлу — чистые функции, база не нужна.</summary>
public class VndAuthenticityMatcherTests
{
    private static readonly string Ru = new('A', 64);
    private static readonly string Kg = new('B', 64);
    private static readonly string Other = new('C', 64);

    private static readonly DateTime T0 = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LanguageOf_FindsFileInAnyLanguage()
    {
        var digest = ResolutionSignatureHasher.BuildContentDigest(Ru, Kg, null);

        Assert.Equal("ru", VndAuthenticityMatcher.LanguageOf(digest, Ru));
        Assert.Equal("kg", VndAuthenticityMatcher.LanguageOf(digest, Kg));
        Assert.Equal("ru", VndAuthenticityMatcher.LanguageOf(digest, Ru.ToLowerInvariant()));
        Assert.Null(VndAuthenticityMatcher.LanguageOf(digest, Other));
    }

    [Fact]
    public void LanguageOf_DoesNotMatchMissingFileMarker() =>
        Assert.Null(VndAuthenticityMatcher.LanguageOf(
            ResolutionSignatureHasher.BuildContentDigest(Ru, null, null), ResolutionSignatureHasher.MissingFileMarker));

    private static VndResolutionSignature Signature(
        ApprovalStagePhase phase, ApprovalStageDecision decision, string ruSha, int id = 1) => new()
    {
        Id = id,
        StageId = 7,
        Phase = phase,
        Decision = decision,
        SignedAt = T0.AddMinutes(id),
        PersonIdnp = "21234567890123",
        HashSchemaVersion = "v2",
        Hash = new string('D', 64),
        CommentText = "Согласовано",
        CommentHash = new string('E', 64),
        ContentDigest = ResolutionSignatureHasher.BuildContentDigest(ruSha, null, null),
        VndCode = "10062",
        SignatureBase64 = "AA==",
        SignatureSha256 = new string('F', 63) + id,
    };

    [Fact]
    public void FinalDecisionSignedUnderThisFile()
    {
        var stage = new VndApprovalStage
        {
            Id = 7, ApproverUserId = 5,
            PrimaryDecision = ApprovalStageDecision.ApprovedWithComment,
            RepeatDecision = ApprovalStageDecision.Approved,
        };
        var signatures = new[]
        {
            Signature(ApprovalStagePhase.Primary, ApprovalStageDecision.ApprovedWithComment, Other, 1),
            Signature(ApprovalStagePhase.Repeat, ApprovalStageDecision.Approved, Ru, 2),
        };

        var (state, signature) = VndAuthenticityMatcher.SignatureState(stage, 99, signatures, Ru);

        Assert.Equal("signed_this_text", state);
        Assert.Equal(2, signature!.Id);
    }

    [Fact]
    public void FinalDecisionSignedUnderOtherFile()
    {
        var stage = new VndApprovalStage
        {
            Id = 7, ApproverUserId = 5, PrimaryDecision = ApprovalStageDecision.Approved,
        };

        var (state, signature) = VndAuthenticityMatcher.SignatureState(
            stage, 99, [Signature(ApprovalStagePhase.Primary, ApprovalStageDecision.Approved, Other)], Ru);

        Assert.Equal("signed_other_text", state);
        Assert.Null(signature);
    }

    [Fact]
    public void TimeoutAndInitiator_AreAutomatic()
    {
        var timeout = new VndApprovalStage
        {
            Id = 7, ApproverUserId = 5,
            PrimaryDecision = ApprovalStageDecision.Approved,
            FinalHoldDecision = ApprovalStageDecision.AutoApprovedByTimeout,
        };
        var initiator = new VndApprovalStage
        {
            Id = 8, ApproverUserId = 5,
            PrimaryDecision = ApprovalStageDecision.Approved,
            PrimaryComment = "Согласовано автоматически — инициатор является согласующим на этом этапе",
        };

        Assert.Equal("auto", VndAuthenticityMatcher.SignatureState(timeout, 99, [], Ru).State);
        Assert.Equal("auto_initiator", VndAuthenticityMatcher.SignatureState(initiator, 5, [], Ru).State);
    }

    [Fact]
    public void History_ListsAllRoundsWithSignaturesAndInitiatorComments()
    {
        // Первичное: замечания (подпись под версией 1). Инициатор исправил → повторное согласование,
        // круг 1 сохранён в VndApprovalPhaseRound: согласовано (подпись под версией 2 = загруженный файл).
        // Финальная выдержка: согласование перенесено автоматически.
        var stage = new VndApprovalStage
        {
            Id = 7, ApproverUserId = 5, Title = "Юридическая служба",
            PrimaryDecision = ApprovalStageDecision.ApprovedWithComment,
            PrimaryComment = "Поправьте п. 3",
            PrimaryDecidedAt = T0,
            FinalHoldDecision = ApprovalStageDecision.Approved,
            FinalHoldComment = "Согласовано автоматически",
            FinalHoldDecidedAt = T0.AddDays(3),
        };
        var process = new VndApprovalProcess { Id = 1, InitiatorUserId = 9, Stages = [stage] };
        var repeatRound = new VndApprovalPhaseRound
        {
            ApprovalProcessId = 1,
            Phase = ApprovalStagePhase.Repeat,
            RoundNumber = 1,
            StartedAt = T0.AddDays(1),
            InitiatorComment = "Исправлено",
            StageDecisions =
            [
                new VndApprovalPhaseRoundStageDecision
                {
                    VndApprovalStageId = 7,
                    Decision = ApprovalStageDecision.Approved,
                    Comment = "Согласовано",
                    DecidedAt = T0.AddDays(2),
                },
            ],
        };
        var signatures = new List<VndResolutionSignature>
        {
            Signature(ApprovalStagePhase.Primary, ApprovalStageDecision.ApprovedWithComment, Other, 1),
            Signature(ApprovalStagePhase.Repeat, ApprovalStageDecision.Approved, Ru, 2),
        };
        signatures[1].SignedAt = T0.AddDays(2);
        var users = new Dictionary<int, delosfera_server.Modules.Users.Models.User>
        {
            [5] = new() { Id = 5, FullName = "Иванов И.И.", Email = "i@bank.kg", PasswordHash = "x" },
            [9] = new() { Id = 9, FullName = "Петров П.П.", Email = "p@bank.kg", PasswordHash = "x" },
        };

        var history = VndAuthenticityMatcher.BuildHistory(process, [repeatRound], signatures, users, Ru);

        Assert.Collection(history,
            e =>
            {
                Assert.Equal("primary", e.Phase);
                Assert.Equal("approved_with_comment", e.Decision);
                Assert.Equal("signed_other_text", e.SignatureState);
                Assert.False(e.Signature!.UnderThisText);
            },
            e =>
            {
                Assert.Equal("initiator_comment", e.Kind);
                Assert.Equal("Петров П.П.", e.FullName);
                Assert.Equal("Исправлено", e.Comment);
            },
            e =>
            {
                Assert.Equal("repeat", e.Phase);
                Assert.Equal(1, e.RoundNumber);
                Assert.Equal("signed_this_text", e.SignatureState);
                Assert.Equal(2, e.Signature!.Id);
                Assert.True(e.Signature.UnderThisText);
            },
            e =>
            {
                Assert.Equal("finalHold", e.Phase);
                Assert.Equal("auto", e.SignatureState);
                Assert.Null(e.Signature);
            });
    }

    [Fact]
    public void PendingDecision_IsPending() =>
        Assert.Equal("pending", VndAuthenticityMatcher.SignatureState(
            new VndApprovalStage { Id = 7, ApproverUserId = 5 }, 99, [], Ru).State);
}
