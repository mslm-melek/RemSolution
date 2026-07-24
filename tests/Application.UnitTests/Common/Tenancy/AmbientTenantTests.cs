using FluentAssertions;
using NUnit.Framework;
using RemSolution.Application.Common.Tenancy;

namespace RemSolution.Application.UnitTests.Common.Tenancy;

public class AmbientTenantTests
{
    [Test]
    public void CurrentAgencyId_IsNull_WhenNothingPushed()
    {
        AmbientTenant.CurrentAgencyId.Should().BeNull();
    }

    [Test]
    public void Push_SetsAndRestoresAgency()
    {
        using (AmbientTenant.Push(42))
        {
            AmbientTenant.CurrentAgencyId.Should().Be(42);
        }

        AmbientTenant.CurrentAgencyId.Should().BeNull();
    }

    [Test]
    public void Push_Nests_RestoringPreviousOnDispose()
    {
        using (AmbientTenant.Push(1))
        {
            AmbientTenant.CurrentAgencyId.Should().Be(1);

            using (AmbientTenant.Push(2))
            {
                AmbientTenant.CurrentAgencyId.Should().Be(2);
            }

            AmbientTenant.CurrentAgencyId.Should().Be(1);
        }
    }
}
