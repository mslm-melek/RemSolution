using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Common.Files;

/// <summary>
/// The file envelope every upload command checks: one of <see cref="UploadFileTypes"/>
/// by name and by declared type, not empty, and capped so a mistyped upload
/// cannot fill the disk. Pulled in with <c>Include</c>; internal so the assembly
/// scan does not register it as a validator of its own.
/// </summary>
internal sealed class FileUploadValidator : AbstractValidator<IFileUpload>
{
    private FileUploadValidator(ILocalizer localizer, string[] extensions, string[] contentTypes)
    {
        RuleFor(c => c.FileName)
            .NotEmpty()
            .Must(name => extensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            .WithMessage(_ => localizer["Validation.File.Extension", string.Join(", ", extensions)]);

        RuleFor(c => c.ContentType)
            .Must(ct => contentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
            .WithMessage(_ => localizer["Validation.File.ContentType", string.Join(", ", contentTypes)]);

        RuleFor(c => c.Length)
            .GreaterThan(0).WithMessage(_ => localizer["Validation.File.Empty"])
            .LessThanOrEqualTo(UploadFileTypes.MaxSizeBytes).WithMessage(_ => localizer["Validation.File.TooLarge"]);
    }

    /// <summary>A scan or a PDF.</summary>
    public static FileUploadValidator Document(ILocalizer localizer) =>
        new(localizer, UploadFileTypes.DocumentExtensions, UploadFileTypes.DocumentContentTypes);

    /// <summary>A picture only: the marketplace shows it inline.</summary>
    public static FileUploadValidator Image(ILocalizer localizer) =>
        new(localizer, UploadFileTypes.ImageExtensions, UploadFileTypes.ImageContentTypes);
}
