using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Infrastructure.Data;

/// <summary>
/// <see cref="ICatalogTemplateCopier"/> as raw SQL over the concrete context.
/// Raw because the caller is the platform administrator, who has no tenant: an
/// EF read would be filtered to nothing. It joins the caller's transaction when
/// there is one (CreateAgencyCommand), so a failed agency creation takes its
/// copies with it.
/// </summary>
public sealed class CatalogTemplateCopier : ICatalogTemplateCopier
{
    private readonly ApplicationDbContext _context;
    private readonly IUser _user;
    private readonly TimeProvider _dateTime;

    public CatalogTemplateCopier(ApplicationDbContext context, IUser user, TimeProvider dateTime)
    {
        _context = context;
        _user = user;
        _dateTime = dateTime;
    }

    public async Task CopyMissingAsync(int? agencyId, CancellationToken cancellationToken)
    {
        var now = _dateTime.GetUtcNow();
        var by = _user.Id;

        await _context.Database.ExecuteSqlAsync($@"
INSERT INTO ExpenseTypes (AgencyId, TemplateId, IsCustomized, Name, IsActive, WithNotif, AfterKilometer, AfterMonth, CreatedBy, CreatedOn)
SELECT a.Id, t.Id, 0, t.Name, 1, t.WithNotif, t.AfterKilometer, t.AfterMonth, {by}, {now}
FROM ExpenseTypeTemplates t CROSS JOIN Agencies a
WHERE t.IsActive = 1
  AND ({agencyId} IS NULL OR a.Id = {agencyId})
  AND NOT EXISTS (SELECT 1 FROM ExpenseTypes c WHERE c.AgencyId = a.Id AND c.TemplateId = t.Id);

INSERT INTO ExtraServicesTypes (AgencyId, TemplateId, IsCustomized, Name, IsActive, CreatedBy, CreatedOn)
SELECT a.Id, t.Id, 0, t.Name, 1, {by}, {now}
FROM ExtraServicesTypeTemplates t CROSS JOIN Agencies a
WHERE t.IsActive = 1
  AND ({agencyId} IS NULL OR a.Id = {agencyId})
  AND NOT EXISTS (SELECT 1 FROM ExtraServicesTypes c WHERE c.AgencyId = a.Id AND c.TemplateId = t.Id);", cancellationToken);
    }

    public Task<int> PropagateExpenseTypeAsync(int templateId, CancellationToken cancellationToken)
    {
        var now = _dateTime.GetUtcNow();
        var by = _user.Id;

        return _context.Database.ExecuteSqlAsync($@"
UPDATE c SET
    Name = t.Name,
    WithNotif = t.WithNotif, AfterKilometer = t.AfterKilometer, AfterMonth = t.AfterMonth,
    UpdatedBy = {by}, UpdatedOn = {now}
FROM ExpenseTypes c JOIN ExpenseTypeTemplates t ON t.Id = c.TemplateId
WHERE t.Id = {templateId} AND c.IsCustomized = 0;", cancellationToken);
    }

    public Task<int> PropagateExtraServicesTypeAsync(int templateId, CancellationToken cancellationToken)
    {
        var now = _dateTime.GetUtcNow();
        var by = _user.Id;

        return _context.Database.ExecuteSqlAsync($@"
UPDATE c SET
    Name = t.Name,
    UpdatedBy = {by}, UpdatedOn = {now}
FROM ExtraServicesTypes c JOIN ExtraServicesTypeTemplates t ON t.Id = c.TemplateId
WHERE t.Id = {templateId} AND c.IsCustomized = 0;", cancellationToken);
    }
}
