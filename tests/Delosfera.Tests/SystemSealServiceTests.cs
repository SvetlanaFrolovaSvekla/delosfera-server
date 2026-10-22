using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Signing.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Delosfera.Tests;

/// <summary>Локальная подпись (печать) системы «Делосфера» для сотрудников без ЭЦП КР.</summary>
public class SystemSealServiceTests
{
    private static readonly string Key1 = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
    private static readonly string Key2 = Convert.ToBase64String(Enumerable.Range(101, 32).Select(i => (byte)i).ToArray());

    internal static ISystemSealService NewSeal(bool configured, string? key = null, params string[] previous) =>
        new SystemSealService(Options.Create(new SystemSealOptions
        {
            HmacKey = configured ? key ?? Key1 : "",
            PreviousHmacKeys = [.. previous],
        }), NullLogger<SystemSealService>.Instance);

    private static VndResolutionSignature Entry() => new()
    {
        Method = ResolutionSignatureMethod.DelosferaSystem,
        VndId = 1, ApprovalProcessId = 2, RedactionId = 3, StageId = 4,
        Phase = ApprovalStagePhase.Primary, RoundNumber = 1, Decision = ApprovalStageDecision.Approved,
        UserId = 200, PersonIdnp = "",
        HashSchemaVersion = "v2", Hash = new string('A', 64), CommentText = "", CommentHash = new string('B', 64),
        ContentDigest = "ru=C;kg=-;en=-", VndCode = "TEST", SignatureBase64 = "", SignatureSha256 = "",
        SignedAt = new DateTime(2026, 10, 22, 4, 5, 6, 789, DateTimeKind.Utc),
        VerifiedAt = DateTime.UtcNow, Verified = true,
    };

    [Fact]
    public void Seal_Verifies_AndAnyFieldChangeBreaksIt()
    {
        var seal = NewSeal(configured: true);
        var entry = Entry();
        var value = seal.Seal(ResolutionSignatureService.SystemSealPayload(entry));

        Assert.True(seal.Verify(ResolutionSignatureService.SystemSealPayload(entry), value));

        foreach (var tamper in new Action<VndResolutionSignature>[]
                 {
                     e => e.UserId = 201,
                     e => e.Hash = new string('D', 64),
                     e => e.Decision = ApprovalStageDecision.Rejected,
                     e => e.StageId = 5,
                     e => e.SignedAt = e.SignedAt.AddMilliseconds(1),
                 })
        {
            var changed = Entry();
            tamper(changed);
            Assert.False(seal.Verify(ResolutionSignatureService.SystemSealPayload(changed), value));
        }
    }

    [Fact]
    public void Verify_AcceptsPreviousKey_AfterRotation_ButSealsWithCurrent()
    {
        var payload = ResolutionSignatureService.SystemSealPayload(Entry());
        var oldSeal = NewSeal(configured: true, Key1).Seal(payload);

        var rotated = NewSeal(configured: true, Key2, Key1);
        Assert.True(rotated.Verify(payload, oldSeal));
        Assert.NotEqual(oldSeal, rotated.Seal(payload));
        Assert.False(NewSeal(configured: true, Key2).Verify(payload, oldSeal));
    }

    [Fact]
    public void Unconfigured_CannotSeal_AndCannotVerify()
    {
        var seal = NewSeal(configured: false);
        Assert.False(seal.IsConfigured);
        Assert.Null(seal.KeyId);
        Assert.Throws<InvalidOperationException>(() => seal.Seal("x"));
        Assert.Null(seal.Verify("x", "AA=="));
    }

    [Fact]
    public void ShortKey_IsIgnored()
    {
        var seal = NewSeal(configured: true, Convert.ToBase64String(new byte[16]));
        Assert.False(seal.IsConfigured);
    }
}
