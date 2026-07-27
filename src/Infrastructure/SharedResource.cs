namespace RemSolution.Infrastructure;

/// <summary>
/// Marker type for the application's single shared resource set: validation
/// messages, domain errors and the Identity Razor pages all resolve through
/// <c>IStringLocalizer&lt;SharedResource&gt;</c>.
/// <para>
/// The namespace and file location are load-bearing. ResourceManagerStringLocalizer
/// derives the resource base name as
/// <c>{assembly}.{ResourcesPath}.{type name minus assembly root namespace}</c>,
/// so with <c>ResourcesPath = "Resources"</c> this type resolves to
/// <c>RemSolution.Infrastructure.Resources.SharedResource</c> — i.e. the .resx
/// files under <c>src/Infrastructure/Resources/</c>. Moving either the type or
/// the folder silently falls back to the resource key.
/// </para>
/// </summary>
public sealed class SharedResource
{
}
