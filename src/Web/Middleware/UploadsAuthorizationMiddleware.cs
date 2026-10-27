using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.MarketplaceSearch.Queries.IsMyBookingFileQuery;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure.Identity;
using RemSolution.Web.Infrastructure;

namespace RemSolution.Web.Middleware;

/// <summary>
/// Stands in front of the uploaded files. They used to be plain static files,
/// served before authentication to anyone holding the URL — passport scans,
/// payment proofs, and the issued contracts and invoices, whose paths
/// (<c>agencies/1/contracts/CTR-2026-000001.pdf</c>) could simply be counted.
///
/// <para>
/// Every path the app writes starts <c>agencies/{agencyId}/{area}/</c>, and the
/// rule is read off it (see <see cref="UploadPath"/>, which also refuses any
/// path the disk could read differently), so the common case costs no database
/// round trip:
/// </para>
/// <list type="bullet">
/// <item><c>cars</c> is public — the marketplace shows those photos to anyone.</item>
/// <item>Anything else needs a signed-in user of that agency who is not still on
/// a mailed temporary password. The finer permission (who may see a client's
/// papers) is already applied by the API that hands the URL out; this gate is
/// what stops the URL working anywhere else.</item>
/// <item>A platform administrator may open it, and each opening is written to
/// the audit trail like any other look inside an agency — an image request
/// carries no workspace header, so it cannot ride on an impersonation.</item>
/// <item>A customer may open what they sent in against their own booking
/// (<see cref="IsMyBookingFileQuery"/>).</item>
/// </list>
///
/// <para>
/// Refusals answer 404, not 403: a 403 would confirm that the counted contract
/// number exists. A file that is not there is refused before anything else, so
/// nothing is audited or looked up for it.
/// </para>
/// </summary>
public class UploadsAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IFileProvider _files;
    private readonly PathString _basePath;

    public UploadsAuthorizationMiddleware(RequestDelegate next, IFileProvider files, PathString basePath)
    {
        _next = next;
        _files = files;
        _basePath = basePath;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ISender sender,
        IImpersonationAuditor auditor,
        UserManager<ApplicationUser> userManager)
    {
        var access = await DecideAsync(context, sender, auditor, userManager);

        if (access == UploadAccess.Denied)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (access == UploadAccess.Private)
        {
            // A shared front-desk computer should not keep somebody's passport in
            // its browser cache after the session that opened it is gone.
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = "private, no-store";
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }

    private async Task<UploadAccess> DecideAsync(
        HttpContext context, ISender sender, IImpersonationAuditor auditor, UserManager<ApplicationUser> userManager)
    {
        // Static files only ever read. Refused first, so a stray POST from a
        // platform admin is not written to the audit trail as a write.
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            return UploadAccess.Denied;

        // Inside the Map branch: PathBase is the uploads prefix, Path the rest.
        if (!UploadPath.TryParse(context.Request.Path.Value, out var path) ||
            !_files.GetFileInfo(path.RelativePath).Exists)
        {
            return UploadAccess.Denied;
        }

        if (path.IsPublic)
            return UploadAccess.Public;

        var user = context.User;

        if (user.Identity?.IsAuthenticated != true ||
            await PasswordChangeRequiredMiddleware.HoldsTemporaryPasswordAsync(user, userManager))
        {
            return UploadAccess.Denied;
        }

        if (user.FindFirstValue(Claims.AgencyId) == path.AgencyId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            return UploadAccess.Private;

        if (user.IsInRole(Roles.PlatformAdministrator))
        {
            await auditor.RecordAsync(
                path.AgencyId, context.Request.Method, $"{_basePath}/{path.RelativePath}", context.RequestAborted);

            return UploadAccess.Private;
        }

        // Rebuilt from the parsed segments, in the form the storage issued it,
        // so a doubled slash in the request does not hide the customer's own file.
        if (user.IsInRole(Roles.Customer) &&
            await sender.Send(new IsMyBookingFileQuery($"{_basePath}/{path.RelativePath}"), context.RequestAborted))
        {
            return UploadAccess.Private;
        }

        return UploadAccess.Denied;
    }

    private enum UploadAccess
    {
        Denied,
        Public,
        Private,
    }
}
