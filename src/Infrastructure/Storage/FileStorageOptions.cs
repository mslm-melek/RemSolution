namespace RemSolution.Infrastructure.Storage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    // Disk root files are written to; a relative value resolves against the
    // app's working directory (the content root for the web host).
    public string RootPath { get; set; } = "wwwroot/uploads";

    // Base of the public URLs returned by SaveAsync. Must map to RootPath —
    // UseStaticFiles serves wwwroot, so /uploads/* hits wwwroot/uploads/*.
    public string PublicBasePath { get; set; } = "/uploads";
}
