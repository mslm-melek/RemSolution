using System.Diagnostics.CodeAnalysis;

namespace RemSolution.Web.Infrastructure;

public static class IEndpointRouteBuilderExtensions
{
    // The optional policy lets a route inside an authorized group demand a
    // specific authorization policy (e.g. a permission name like
    // "Client.Create") on top of the group's requirement.
    public static IEndpointRouteBuilder MapGet(this IEndpointRouteBuilder builder, Delegate handler, [StringSyntax("Route")] string pattern = "", string? policy = null)
    {
        Guard.Against.AnonymousMethod(handler);

        var route = builder.MapGet(pattern, handler)
            .WithName(handler.Method.Name);

        if (policy is not null)
            route.RequireAuthorization(policy);

        return builder;
    }

    public static IEndpointRouteBuilder MapPost(this IEndpointRouteBuilder builder, Delegate handler, [StringSyntax("Route")] string pattern = "", string? policy = null)
    {
        Guard.Against.AnonymousMethod(handler);

        var route = builder.MapPost(pattern, handler)
            .WithName(handler.Method.Name);

        if (policy is not null)
            route.RequireAuthorization(policy);

        return builder;
    }

    public static IEndpointRouteBuilder MapPut(this IEndpointRouteBuilder builder, Delegate handler, [StringSyntax("Route")] string pattern, string? policy = null)
    {
        Guard.Against.AnonymousMethod(handler);

        var route = builder.MapPut(pattern, handler)
            .WithName(handler.Method.Name);

        if (policy is not null)
            route.RequireAuthorization(policy);

        return builder;
    }

    public static IEndpointRouteBuilder MapDelete(this IEndpointRouteBuilder builder, Delegate handler, [StringSyntax("Route")] string pattern, string? policy = null)
    {
        Guard.Against.AnonymousMethod(handler);

        var route = builder.MapDelete(pattern, handler)
            .WithName(handler.Method.Name);

        if (policy is not null)
            route.RequireAuthorization(policy);

        return builder;
    }
}
