using System.Text;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class LiveSupportPdfTests
{
    [Fact]
    public async Task Staff_can_upload_send_and_download_pdf_larger_than_ten_megabytes()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        var root = Path.Combine(Path.GetTempPath(), $"support-pdf-test-{Guid.NewGuid():N}");
        try
        {
            var storage = new LiveSupportAttachmentStorage(new SharedFileStorage(
                new Dictionary<SharedFileArea, string> { [SharedFileArea.LiveSupport] = root }));
            var service = new LiveSupportService(fixture.Db, new LiveSupportEnabledSettings(), attachmentStorage: storage);
            var bytes = new byte[11 * 1024 * 1024];
            Encoding.ASCII.GetBytes("%PDF-1.7\n").CopyTo(bytes, 0);
            await using var input = new MemoryStream(bytes);
            var conversationId = LiveSupportTestData.Conversation().Id;
            var uploaded = await service.SaveStaffAttachmentAsync(LiveSupportTestData.StaffAId, false,
                conversationId, input, "lesson.pdf", "application/pdf", bytes.Length, CancellationToken.None);
            var sent = await service.SendStaffAttachmentMessageAsync(LiveSupportTestData.StaffAId, false,
                conversationId, Guid.NewGuid().ToString(), uploaded.Id, "lesson", LiveSupportMessageType.Pdf, CancellationToken.None);
            Assert.Equal(LiveSupportMessageType.Pdf, sent.Message.Type);
            var download = await service.OpenStaffAttachmentAsync(LiveSupportTestData.StaffAId, false, conversationId, uploaded.Id, CancellationToken.None);
            await using var content = download.Content;
            Assert.Equal(bytes.Length, download.SizeBytes);
            using var actual = new MemoryStream();
            await content.CopyToAsync(actual);
            Assert.Equal(bytes, actual.ToArray());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("fake document", "application/pdf", 13)]
    [InlineData("%PDF-1.7", "application/pdf", 99)]
    [InlineData("%PDF-1.7", "application/pdf", 94371841)]
    [InlineData("%PDF-1.7", "image/png", 11534336)]
    public async Task Invalid_or_oversized_upload_does_not_leave_stored_files(string content, string contentType, long declaredSize)
    {
        var root = Path.Combine(Path.GetTempPath(), $"support-pdf-test-{Guid.NewGuid():N}");
        try
        {
            var storage = new LiveSupportAttachmentStorage(new SharedFileStorage(
                new Dictionary<SharedFileArea, string> { [SharedFileArea.LiveSupport] = root }));
            await using var input = new MemoryStream(Encoding.ASCII.GetBytes(content));
            await Assert.ThrowsAsync<InvalidUploadContentException>(() => storage.SaveAsync(input, "lesson.pdf", contentType, declaredSize, CancellationToken.None));
            Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
