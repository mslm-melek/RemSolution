namespace RemSolution.Infrastructure.Data;

/// <summary>
/// Switches the demo dataset on. Opt-in and off by default, because the seeder
/// writes real rows into whatever database it is pointed at — a shared dev
/// database should not sprout fake bookings because someone ran the app.
/// <para>
/// Set it in appsettings.Development.json (or via
/// <c>DemoData__Enabled=true</c>). It is additionally gated on the Development
/// environment at the call site.
/// </para>
/// </summary>
public class DemoDataOptions
{
    public const string SectionName = "DemoData";

    public bool Enabled { get; set; }
}
