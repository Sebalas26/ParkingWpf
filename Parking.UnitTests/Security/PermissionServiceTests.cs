using System.Collections.Generic;
using FluentAssertions;
using Parking.Services.Implementations;
using Xunit;

namespace Parking.UnitTests.Security;

public class PermissionServiceTests
{
    [Fact]
    public void HasPermission_WhenAdmin_AlwaysReturnsTrue()
    {
        // Arrange
        var service = new PermissionService();
        service.LoadPermissions(new List<string>(), isAdmin: true);

        // Act & Assert
        service.HasPermission("shifts.view_current").Should().BeTrue();
        service.HasPermission("checkout.process_payment").Should().BeTrue();
        service.HasPermission("any_arbitrary_slug").Should().BeTrue();
    }

    [Fact]
    public void HasPermission_ExactSlugGranted_ReturnsTrue()
    {
        // Arrange
        var service = new PermissionService();
        service.LoadPermissions(new[] { "checkin.create_ticket", "shifts.open" }, isAdmin: false);

        // Act & Assert
        service.HasPermission("checkin.create_ticket").Should().BeTrue();
        service.HasPermission("shifts.open").Should().BeTrue();
        service.HasPermission("checkout.waive_fee").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_WildcardModule_GrantsSubPermissions()
    {
        // Arrange: El usuario tiene el módulo "shifts.*"
        var service = new PermissionService();
        service.LoadPermissions(new[] { "shifts.*" }, isAdmin: false);

        // Act & Assert
        service.HasPermission("shifts.view_current").Should().BeTrue();
        service.HasPermission("shifts.close").Should().BeTrue();
        service.HasPermission("shifts.blind_count").Should().BeTrue();
        service.HasPermission("checkout.view").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_AliasAndPrefixedSlugs_ResolvesCrossCompatibility()
    {
        // Arrange: Si se otorgó "wpf.checkin.create"
        var service = new PermissionService();
        service.LoadPermissions(new[] { "wpf.checkin.create" }, isAdmin: false);

        // Act & Assert: Debe reconocer "checkin.create_ticket" o "checkin.create"
        service.HasPermission("checkin.create_ticket").Should().BeTrue();
        service.HasPermission("checkin.create").Should().BeTrue();
    }

    [Fact]
    public void HasPermission_EmptyOrNullSlug_ReturnsTrue()
    {
        // Arrange
        var service = new PermissionService();
        service.LoadPermissions(new List<string>(), isAdmin: false);

        // Act & Assert
        service.HasPermission(null).Should().BeTrue();
        service.HasPermission("").Should().BeTrue();
        service.HasPermission("   ").Should().BeTrue();
    }
}
