using System.Reflection;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Behaviours;
using RemSolution.Application.Common.Interfaces;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        var mapsterConfig = TypeAdapterConfig.GlobalSettings;
        mapsterConfig.Scan(Assembly.GetExecutingAssembly());
        builder.Services.AddSingleton(mapsterConfig);
        builder.Services.AddScoped<IMapper, ServiceMapper>();

        builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Per-request carriers shared by the web middleware (writer), the audit
        // pipeline behaviour (writer) and the audit interceptor (reader).
        builder.Services.AddScoped<ICorrelationContext, CorrelationContext>();
        builder.Services.AddScoped<IAuditScope, AuditScope>();

        builder.Services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
            // Innermost: only requests that pass auth + validation open an audit scope.
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditableBehaviour<,>));
        });
    }
}
