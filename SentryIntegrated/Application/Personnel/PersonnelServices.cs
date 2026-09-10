using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SentryIntegrated.Configuration;
using SentryIntegrated.Domain;
using SentryIntegrated.Infrastructure.Persistence;

namespace SentryIntegrated.Application.Personnel;

public interface IPersonnelResolver { Task<Person> ResolveAsync(string accessNumber, CancellationToken cancellationToken); }
public sealed class PersonnelResolver(IDbContextFactory<StaffDbContext> staffFactory, IDbContextFactory<StudentDbContext> studentFactory) : IPersonnelResolver
{
    public async Task<Person> ResolveAsync(string accessNumber, CancellationToken ct)
    {
        await using var staffDb = await staffFactory.CreateDbContextAsync(ct);
        var staff = await staffDb.Staff.AsNoTracking().SingleOrDefaultAsync(x => x.AccessNumber == accessNumber, ct);
        if (staff is not null) return new(accessNumber, Format(staff.FirstName, staff.LastName), staff.PhotoName, staff.PhoneNumber, PersonKind.Staff);
        await using var studentDb = await studentFactory.CreateDbContextAsync(ct);
        var student = await studentDb.Students.AsNoTracking().SingleOrDefaultAsync(x => x.AccessNumber == accessNumber, ct);
        return student is null
            ? new(accessNumber, "Unknown personnel", null, null, PersonKind.Unknown)
            : new(accessNumber, Format(student.FirstName, student.LastName), student.PhotoName, student.PhoneNumber, PersonKind.Student);
    }
    private static string Format(string first, string last) => $"{last.Trim()}, {first.Trim()}".Trim(' ', ',');
}

public interface IPhotoResolver { string Resolve(string? photoName); }
public sealed class PhotoResolver(IOptions<SentryOptions> options) : IPhotoResolver
{
    public string Resolve(string? photoName)
    {
        var value = options.Value.Photos;
        if (string.IsNullOrWhiteSpace(photoName) || photoName.IndexOfAny(['/', '\\']) >= 0) return value.FallbackUrl;
        return $"{value.BaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(photoName)}";
    }
}
