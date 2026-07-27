namespace RemSolution.Application.Common.Models;

/// <summary>
/// A stored file being handed back to a caller: the bytes plus what a browser
/// needs to save them. <paramref name="Content"/> is the caller's to dispose —
/// the endpoint streams it to the response and ASP.NET closes it.
/// <para>
/// Downloads go through the API rather than the file's public URL because the
/// static-file URL carries no authorization: routing the read through a query
/// keeps the permission and feature gate on the path.
/// </para>
/// </summary>
public sealed record FileDownload(Stream Content, string FileName, string ContentType);
