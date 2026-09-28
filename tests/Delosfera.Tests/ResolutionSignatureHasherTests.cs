using System.Security.Cryptography;
using System.Text;
using delosfera_server.Modules.Documents.VND.Services;

namespace Delosfera.Tests;

/// <summary>Хэш резолюции для ЭЦП (схема v1) — чистые функции, база не нужна.</summary>
public class ResolutionSignatureHasherTests
{
    private static readonly string RuHash = new('1', 64);
    private static readonly string KgHash = new('2', 64);

    private static string Content() => ResolutionSignatureHasher.BuildContentDigest(RuHash, KgHash, null);

    private static string Hash(string? comment, string? content = null, string code = "10062") =>
        ResolutionSignatureHasher.Compute(
            ResolutionSignatureHasher.CreateComponents(comment, content ?? Content(), code));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t ")]
    public void EmptyComment_BecomesSoglasovano(string? comment)
    {
        Assert.Equal("Согласовано", ResolutionSignatureHasher.NormalizeComment(comment));
        Assert.Equal(Hash("Согласовано"), Hash(comment));
    }

    [Fact]
    public void SameInput_SameHash()
    {
        Assert.Equal(Hash("Согласовано с учётом замечаний"), Hash("Согласовано с учётом замечаний"));
    }

    [Fact]
    public void Hash_IsUpperHexSha256()
    {
        var hash = Hash("Ок");
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9A-F]{64}$", hash);
    }

    [Fact]
    public void ChangingComment_ChangesHash() =>
        Assert.NotEqual(Hash("Согласовано"), Hash("Согласовано."));

    [Fact]
    public void ChangingContent_ChangesHash()
    {
        var other = ResolutionSignatureHasher.BuildContentDigest(RuHash, KgHash, new string('3', 64));
        Assert.NotEqual(Hash("Ок"), Hash("Ок", other));
    }

    [Fact]
    public void ChangingAnyLanguageFile_ChangesContentDigest()
    {
        var baseline = ResolutionSignatureHasher.BuildContentDigest(RuHash, KgHash, null);
        Assert.NotEqual(baseline, ResolutionSignatureHasher.BuildContentDigest(new string('9', 64), KgHash, null));
        Assert.NotEqual(baseline, ResolutionSignatureHasher.BuildContentDigest(RuHash, new string('9', 64), null));
        Assert.NotEqual(baseline, ResolutionSignatureHasher.BuildContentDigest(RuHash, null, null));
    }

    [Fact]
    public void ChangingCode_ChangesHash() =>
        Assert.NotEqual(Hash("Ок", code: "10062"), Hash("Ок", code: "10063"));

    [Fact]
    public void CrLf_And_Lf_GiveSameHash()
    {
        Assert.Equal(Hash("Строка 1\nСтрока 2"), Hash("Строка 1\r\nСтрока 2"));
        Assert.Equal(Hash("Строка 1\nСтрока 2"), Hash("Строка 1\rСтрока 2"));
    }

    [Fact]
    public void CommentIsTrimmedAtEdges_ButInnerWhitespaceMatters()
    {
        Assert.Equal(Hash("Ок"), Hash("  Ок \r\n"));
        Assert.NotEqual(Hash("Ок да"), Hash("Ок  да"));
    }

    [Fact]
    public void UnicodeNfc_And_Nfd_GiveSameHash()
    {
        // "й" составной (и + кратка) против готового символа.
        var nfd = "Согласовано: й";
        var nfc = "Согласовано: й";
        Assert.NotEqual(nfd, nfc);
        Assert.Equal(Hash(nfc), Hash(nfd));
    }

    [Fact]
    public void Canonical_HasVersionAndLengthPrefixedFields()
    {
        var canonical = ResolutionSignatureHasher.BuildCanonical(
            new ResolutionHashComponents("Ок", "ru=A;kg=-;en=-", "10062"));

        // "Ок" — 4 байта UTF-8 (кириллица по 2 байта).
        Assert.Equal("v1\ncomment:4:Ок\ncontent:14:ru=A;kg=-;en=-\ncode:5:10062", canonical);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))),
            ResolutionSignatureHasher.Compute(new ResolutionHashComponents("Ок", "ru=A;kg=-;en=-", "10062")));
    }

    [Fact]
    public void LengthPrefix_PreventsAmbiguousConcatenation()
    {
        // Без длин эти две пары склеивались бы одинаково.
        var a = ResolutionSignatureHasher.Compute(new ResolutionHashComponents("a\ncode:1:b", "c", "d"));
        var b = ResolutionSignatureHasher.Compute(new ResolutionHashComponents("a", "c", "d"));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void HashesEqual_IsCaseInsensitive_AndRejectsGarbage()
    {
        var hash = Hash("Ок");
        Assert.True(ResolutionSignatureHasher.HashesEqual(hash, hash.ToLowerInvariant()));
        Assert.False(ResolutionSignatureHasher.HashesEqual(hash, Hash("Ок!")));
        Assert.False(ResolutionSignatureHasher.HashesEqual(hash, null));
        Assert.False(ResolutionSignatureHasher.HashesEqual(hash, "not-hex"));
        Assert.False(ResolutionSignatureHasher.HashesEqual(hash, hash[..62]));
    }

    [Fact]
    public void ContentDigest_RequiresRussianFile() =>
        Assert.Throws<ArgumentException>(() => ResolutionSignatureHasher.BuildContentDigest("", KgHash, null));
}
