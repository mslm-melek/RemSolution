using FluentAssertions;
using NUnit.Framework;
using RemSolution.Application.Common.Tenancy;

namespace RemSolution.Application.UnitTests.Common.Tenancy;

public class ImpersonationScopeTests
{
    [Test]
    public void IsActive_IsFalse_WhenNotBegun()
    {
        ImpersonationScope.IsActive.Should().BeFalse();
    }

    [Test]
    public void Begin_SetsAndRestores()
    {
        using (ImpersonationScope.Begin())
        {
            ImpersonationScope.IsActive.Should().BeTrue();
        }

        ImpersonationScope.IsActive.Should().BeFalse();
    }

    [Test]
    public void Begin_Nests_RestoringPreviousOnDispose()
    {
        using (ImpersonationScope.Begin())
        {
            ImpersonationScope.IsActive.Should().BeTrue();

            using (ImpersonationScope.Begin())
            {
                ImpersonationScope.IsActive.Should().BeTrue();
            }

            ImpersonationScope.IsActive.Should().BeTrue();
        }
    }
}
