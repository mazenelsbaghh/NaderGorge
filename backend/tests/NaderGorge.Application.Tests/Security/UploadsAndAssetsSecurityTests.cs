using NaderGorge.Application.Common;

namespace NaderGorge.Application.Tests.Security;

public class UploadsAndAssetsSecurityTests
{
    [Fact]
    public void Validate_RejectsHtmlRenamedAsPdf()
    {
        var bytes = "<html><script>alert(1)</script></html>"u8.ToArray();

        Assert.Throws<InvalidUploadContentException>(() =>
            UploadFileSafety.Validate(bytes, "lesson.pdf", "application/pdf", SafeUploadKind.ProtectedResource));
    }

    [Fact]
    public void Validate_RejectsSvgForPublicImage()
    {
        var bytes = """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>"""u8.ToArray();

        Assert.Throws<InvalidUploadContentException>(() =>
            UploadFileSafety.Validate(bytes, "image.svg", "image/svg+xml", SafeUploadKind.PublicImage));
    }

    [Fact]
    public void Validate_AcceptsRealPdfHeader()
    {
        var bytes = "%PDF-1.7\nbody"u8.ToArray();

        var result = UploadFileSafety.Validate(bytes, "lesson.pdf", "application/pdf", SafeUploadKind.ProtectedResource);

        Assert.Equal(".pdf", result.Extension);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.EndsWith(".pdf", result.SafeFileName);
    }

    [Fact]
    public void Validate_RejectsMismatchedDeclaredContentType()
    {
        var bytes = "%PDF-1.7\nbody"u8.ToArray();

        Assert.Throws<InvalidUploadContentException>(() =>
            UploadFileSafety.Validate(bytes, "lesson.pdf", "image/png", SafeUploadKind.ProtectedResource));
    }

    [Fact]
    public void Validate_SanitizesTraversalDisplayName()
    {
        var bytes = "%PDF-1.7\nbody"u8.ToArray();

        var result = UploadFileSafety.Validate(bytes, "../../evil.pdf", "application/pdf", SafeUploadKind.ProtectedResource);

        Assert.DoesNotContain("..", result.DisplayFileName);
        Assert.DoesNotContain("/", result.DisplayFileName);
    }

    [Fact]
    public void Validate_AcceptsWebmAudioForPrivateAttachments()
    {
        var bytes = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x93, 0x42, 0x82, 0x88 };

        var result = UploadFileSafety.Validate(bytes, "voice.webm", "audio/webm;codecs=opus", SafeUploadKind.PrivateAttachment);

        Assert.Equal(".webm", result.Extension);
        Assert.Equal("audio/webm", result.ContentType);
    }
}
