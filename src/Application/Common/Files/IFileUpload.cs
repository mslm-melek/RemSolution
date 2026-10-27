namespace RemSolution.Application.Common.Files;

/// <summary>A command carrying one uploaded file, validated by <see cref="FileUploadValidator"/>.</summary>
public interface IFileUpload
{
    string? FileName { get; }

    string? ContentType { get; }

    long Length { get; }
}
