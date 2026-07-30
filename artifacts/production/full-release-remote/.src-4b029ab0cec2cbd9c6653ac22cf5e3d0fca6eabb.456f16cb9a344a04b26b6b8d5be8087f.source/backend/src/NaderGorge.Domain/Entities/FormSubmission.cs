using System;
using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class FormSubmission : BaseEntity
{
    public Guid CustomFormId { get; set; }
    public string SubmittedDataJson { get; set; } = null!; // JSON object mapping field IDs to submitted answers
    public FormSubmissionStatus Status { get; set; } = FormSubmissionStatus.Pending;
    public string? AdminNotes { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public virtual CustomForm CustomForm { get; set; } = null!;
}
