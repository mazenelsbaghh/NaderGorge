using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public interface IArchivableContent
{
    Guid Id { get; }
    ContentArchiveMode ArchiveMode { get; set; }
    DateTime? ArchivedAt { get; set; }
    Guid? ArchivedByUserId { get; set; }
}
