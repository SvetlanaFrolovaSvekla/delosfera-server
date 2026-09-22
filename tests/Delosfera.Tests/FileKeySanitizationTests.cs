using delosfera_server.Modules.Files.Services;
using Xunit;

namespace Delosfera.Tests;

/// <summary>
/// FILE-TRAV: имя файла от пользователя не должно вырываться из GUID-префикса ключа
/// объекта MinIO. SafeKeySegment отбрасывает путь, разделители, управляющие символы и «..».
/// </summary>
public class FileKeySanitizationTests
{
    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\windows\\system32", "windowssystem32")]
    [InlineData("/absolute/path/file.docx", "file.docx")]
    [InlineData("...", "file")]
    [InlineData("", "file")]
    [InlineData("   ", "file")]
    [InlineData("отчёт.pdf", "отчёт.pdf")]
    public void SafeKeySegment_StripsTraversalAndSeparators(string input, string expected)
    {
        var result = MinioFileStorageService.SafeKeySegment(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SafeKeySegment_HasNoPathSeparatorsOrTraversal()
    {
        foreach (var name in new[]
        {
            "../../evil", "a/../../b", "..\\..\\x", "x\0y", "dir/sub/file.txt", "..",
        })
        {
            var seg = MinioFileStorageService.SafeKeySegment(name);
            Assert.DoesNotContain('/', seg);
            Assert.DoesNotContain('\\', seg);
            Assert.DoesNotContain("..", seg);
            Assert.DoesNotContain('\0', seg);
            Assert.False(string.IsNullOrWhiteSpace(seg));
        }
    }

    [Fact]
    public void SafeKeySegment_CapsLength()
    {
        var seg = MinioFileStorageService.SafeKeySegment(new string('a', 5000) + ".txt");
        Assert.True(seg.Length <= 120);
    }

    [Fact]
    public void SafeKeySegment_DropsControlCharacters()
    {
        var seg = MinioFileStorageService.SafeKeySegment("na\nme\twith\0ctrl.txt");
        Assert.Equal("namewithctrl.txt", seg);
    }
}
