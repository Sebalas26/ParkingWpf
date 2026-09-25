using System;
using System.Collections.Generic;
using FluentAssertions;
using Parking.Models;
using Parking.Services.Implementations;
using Xunit;

namespace Parking.UnitTests.Services;

public class SessionServiceCorporateConsistencyTests
{
    [Fact]
    public void SetSession_WhenUserOrAnyBranchHasCorporateData_PropagatesToAllBranchesAndCurrentUser()
    {
        // Arrange
        var sessionService = new SessionService();

        var user = new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            Username = "cajero1",
            FullName = "Cajero Uno",
            CompanyId = 1
            // CompanyName, CompanyNit, etc. son null
        };

        var branch1 = new BranchModel
        {
            Id = 1,
            Name = "Pepe sierra",
            CompanyName = "Parkgo",
            CompanyNit = "9088777777",
            CompanyPhone = "3102207910",
            CompanyEmail = "contacto@parkgo.com"
        };

        var branch2 = new BranchModel
        {
            Id = 2,
            Name = "Sede 136",
            CompanyName = null,
            CompanyNit = null,
            CompanyPhone = null,
            CompanyEmail = null
        };

        var branches = new List<BranchModel> { branch1, branch2 };

        // Act
        sessionService.SetSession(user, branches, branch1);

        // Assert: CurrentUser debe haber adoptado los datos corporativos
        sessionService.CurrentUser.Should().NotBeNull();
        sessionService.CurrentUser!.CompanyName.Should().Be("Parkgo");
        sessionService.CurrentUser.CompanyNit.Should().Be("9088777777");
        sessionService.CurrentUser.CompanyPhone.Should().Be("3102207910");
        sessionService.CurrentUser.CompanyEmail.Should().Be("contacto@parkgo.com");

        // Ambas sedes en UserBranches deben tener los datos corporativos
        sessionService.UserBranches.Should().HaveCount(2);
        foreach (var b in sessionService.UserBranches)
        {
            b.CompanyName.Should().Be("Parkgo");
            b.CompanyNit.Should().Be("9088777777");
            b.CompanyPhone.Should().Be("3102207910");
            b.CompanyEmail.Should().Be("contacto@parkgo.com");
        }
    }

    [Fact]
    public void SetActiveBranch_WhenSwitchingToAnotherBranch_PreservesCorporateIdentity()
    {
        // Arrange
        var sessionService = new SessionService();

        var user = new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            Username = "cajero1",
            FullName = "Cajero Uno",
            CompanyId = 1,
            CompanyName = "Parkgo",
            CompanyNit = "9088777777",
            CompanyPhone = "3102207910",
            CompanyEmail = "contacto@parkgo.com"
        };

        var branch1 = new BranchModel
        {
            Id = 1,
            Name = "Pepe sierra",
            CompanyName = "Parkgo",
            CompanyNit = "9088777777",
            CompanyPhone = "3102207910",
            Phone = "3102207910"
        };

        var branch2 = new BranchModel
        {
            Id = 2,
            Name = "Sede 136",
            Phone = "3188088885"
            // Corporate fields are null
        };

        sessionService.SetSession(user, new List<BranchModel> { branch1, branch2 }, branch1);

        // Act: Cambiar a la Sede 136
        sessionService.SetActiveBranch(branch2);

        // Assert: La sede activa ahora es Sede 136, y sus datos corporativos deben ser Parkgo y 9088777777
        sessionService.CurrentBranch.Should().NotBeNull();
        sessionService.CurrentBranch!.Id.Should().Be(2);
        sessionService.CurrentBranch.Name.Should().Be("Sede 136");
        sessionService.CurrentBranch.CompanyName.Should().Be("Parkgo");
        sessionService.CurrentBranch.CompanyNit.Should().Be("9088777777");
        sessionService.CurrentBranch.CompanyPhone.Should().Be("3102207910");
        sessionService.CurrentBranch.CompanyEmail.Should().Be("contacto@parkgo.com");
    }
}
