using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

internal sealed class AdminAIStudentIdentitySnapshotReader(IAppDbContext db)
{
    public async Task<AdminAIStudentProfileSection> LoadProfileAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        var includeAccount = request.ProfileFields.Contains("account");
        var includePersonal = request.ProfileFields.Contains("personal");
        var includeAcademic = request.ProfileFields.Contains("academic");
        var includeSchool = request.ProfileFields.Contains("school");
        var profile = await db.Users.AsNoTracking()
            .Where(user => user.Id == request.StudentId)
            .Select(user => new
            {
                StudentCode = includeAccount ? user.StudentProfile!.StudentCode : null,
                IsActive = includeAccount ? user.IsActive : (bool?)null,
                CreatedAt = includeAccount ? user.CreatedAt : (DateTime?)null,
                IsProfileComplete = includeAccount ? user.IsProfileComplete : (bool?)null,
                DateOfBirth = includePersonal ? user.StudentProfile!.DateOfBirth : (DateTime?)null,
                Gender = includePersonal ? user.StudentProfile!.Gender : (Gender?)null,
                Nationality = includePersonal ? user.StudentProfile!.Nationality : null,
                EducationStage = includeAcademic
                    ? user.StudentProfile!.EducationStage
                    : (EducationStage?)null,
                GradeLevel = includeAcademic ? user.StudentProfile!.GradeLevel : (GradeLevel?)null,
                StudyTrack = includeAcademic ? user.StudentProfile!.StudyTrack : null,
                SchoolName = includeSchool ? user.StudentProfile!.SchoolName : null,
                SchoolType = includeSchool ? user.StudentProfile!.SchoolType : null
            })
            .SingleAsync(ct);

        var account = includeAccount
            ? new AdminAIStudentProfileAccount(
                AdminAIReadArguments.SafeText(profile.StudentCode, 100),
                profile.IsActive == true ? "active" : "disabled",
                profile.CreatedAt!.Value,
                profile.IsProfileComplete == true)
            : null;
        var personal = includePersonal
            ? new AdminAIStudentProfilePersonal(
                profile.DateOfBirth!.Value,
                profile.Gender!.Value.ToString(),
                AdminAIReadArguments.SafeText(profile.Nationality, 80))
            : null;
        var academic = includeAcademic
            ? new AdminAIStudentProfileAcademic(
                profile.EducationStage!.Value.ToString(),
                profile.GradeLevel!.Value.ToString(),
                profile.StudyTrack?.ToString() ?? string.Empty)
            : null;
        var school = includeSchool
            ? new AdminAIStudentProfileSchool(
                AdminAIReadArguments.SafeText(profile.SchoolName, 160),
                profile.SchoolType?.ToString() ?? string.Empty)
            : null;
        return new(account, personal, academic, school);
    }

    public async Task<AdminAIStudentContactSection> LoadContactAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        var includeStudentPhones = request.ContactFields.Contains("studentPhones");
        var includeGuardianPhones = request.ContactFields.Contains("guardianPhones");
        var includeLocation = request.ContactFields.Contains("location");
        var contact = await db.Users.AsNoTracking()
            .Where(user => user.Id == request.StudentId)
            .Select(user => new
            {
                PhoneNumber = includeStudentPhones ? user.PhoneNumber : null,
                SecondaryPhone = includeStudentPhones ? user.StudentProfile!.SecondaryPhone : null,
                ParentPhone = includeGuardianPhones ? user.StudentProfile!.ParentPhone : null,
                SecondaryParentPhone = includeGuardianPhones ? user.StudentProfile!.SecondaryParentPhone : null,
                MotherPhone = includeGuardianPhones ? user.StudentProfile!.MotherPhone : null,
                Governorate = includeLocation ? user.StudentProfile!.Governorate : null,
                District = includeLocation ? user.StudentProfile!.District : null,
                Address = includeLocation ? user.StudentProfile!.Address : null
            })
            .SingleAsync(ct);

        var studentPhones = includeStudentPhones
            ? new AdminAIStudentOwnPhones(
                AdminAIReadArguments.SafeText(contact.PhoneNumber, 32),
                AdminAIReadArguments.SafeText(contact.SecondaryPhone, 32))
            : null;
        var guardianPhones = includeGuardianPhones
            ? new AdminAIStudentGuardianPhones(
                AdminAIReadArguments.SafeText(contact.ParentPhone, 32),
                AdminAIReadArguments.SafeText(contact.SecondaryParentPhone, 32),
                AdminAIReadArguments.SafeText(contact.MotherPhone, 32))
            : null;
        var location = includeLocation
            ? new AdminAIStudentLocation(
                AdminAIReadArguments.SafeText(contact.Governorate, 100),
                AdminAIReadArguments.SafeText(contact.District, 120),
                AdminAIReadArguments.SafeText(contact.Address, 300))
            : null;
        return new(studentPhones, guardianPhones, location);
    }
}
