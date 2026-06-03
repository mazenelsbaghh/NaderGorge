using System.Collections.Generic;
using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class CustomForm : BaseEntity
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public int VisitCount { get; set; }
    public string FieldsJson { get; set; } = null!; // JSON array representation of dynamic fields

    public virtual ICollection<FormSubmission> Submissions { get; set; } = new List<FormSubmission>();
}
