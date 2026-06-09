using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Public.Commands;

public record SubmitPublicFormCommand(
    string Slug,
    Dictionary<string, string> Answers
) : IRequest<ApiResponse>;

public class SubmitPublicFormCommandHandler : IRequestHandler<SubmitPublicFormCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    public SubmitPublicFormCommandHandler(IAppDbContext db) => _db = db;

    private class FormFieldModel
    {
        public string Id { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Label { get; set; } = null!;
        public bool IsRequired { get; set; }
    }

    public async Task<ApiResponse> Handle(SubmitPublicFormCommand request, CancellationToken ct)
    {
        var lowerSlug = request.Slug.ToLowerInvariant();
        var form = await _db.CustomForms
            .FirstOrDefaultAsync(f => f.Slug == lowerSlug && f.IsActive, ct);

        if (form == null) return ApiResponse.Fail("النموذج المطلوب غير موجود أو غير مفعل حالياً.");

        // Parse fields configuration to validate submitted answers
        List<FormFieldModel>? fields = null;
        try
        {
            fields = JsonSerializer.Deserialize<List<FormFieldModel>>(form.FieldsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return ApiResponse.Fail("عذراً، حدث خطأ في بنية حقول النموذج المسجلة.");
        }

        if (fields == null) return ApiResponse.Fail("هيكل حقول النموذج فارغ.");

        // Validate each field
        var validatedAnswers = new Dictionary<string, string>();
        foreach (var field in fields)
        {
            request.Answers.TryGetValue(field.Id, out var value);
            value = value?.Trim() ?? string.Empty;

            // Required validation
            if (field.IsRequired && string.IsNullOrEmpty(value))
            {
                return ApiResponse.Fail($"يرجى إدخال قيمة لحقل '{field.Label}' المطلوبة.");
            }

            if (!string.IsNullOrEmpty(value))
            {
                // Email validation
                if (field.Type == "email")
                {
                    var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                    if (!emailRegex.IsMatch(value))
                    {
                        return ApiResponse.Fail($"صيغة البريد الإلكتروني في حقل '{field.Label}' غير صالحة.");
                    }
                }

                // Phone validation
                if (field.Type == "phone")
                {
                    // Normalize Arabic numerals to Western
                    var normalizedValue = NormalizeNumbers(value);
                    var phoneRegex = new Regex(@"^[0-9+ ]{8,15}$");
                    if (!phoneRegex.IsMatch(normalizedValue))
                    {
                        return ApiResponse.Fail($"رقم الهاتف في حقل '{field.Label}' يجب أن يحتوي على أرقام فقط وبطول صالح.");
                    }
                    value = normalizedValue;
                }
            }

            validatedAnswers[field.Id] = value;
        }

        // Save submission snapshot
        var submission = new FormSubmission
        {
            CustomFormId = form.Id,
            SubmittedDataJson = JsonSerializer.Serialize(validatedAnswers),
            Status = FormSubmissionStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };

        _db.FormSubmissions.Add(submission);
        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok();
    }

    private string NormalizeNumbers(string input)
    {
        var arabicNumerals = new[] { "٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩" };
        var westernNumerals = new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        var result = input;
        for (int i = 0; i < 10; i++)
        {
            result = result.Replace(arabicNumerals[i], westernNumerals[i]);
        }
        return result;
    }
}
