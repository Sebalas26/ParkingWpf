using System.Threading;
using System.Windows.Controls;
using FluentAssertions;
using Parking.Controls;
using Parking.Core.Helpers;
using Xunit;

namespace Parking.UnitTests.Helpers;

public class RadialSpinnerAndButtonHelperTests
{
    [Fact]
    public void ButtonHelper_SetAndGetIsLoading_WorksCorrectly()
    {
        var thread = new Thread(() =>
        {
            var button = new Button();
            ButtonHelper.GetIsLoading(button).Should().BeFalse();

            ButtonHelper.SetIsLoading(button, true);
            ButtonHelper.GetIsLoading(button).Should().BeTrue();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void RadialSpinner_CanBeInstantiated_WithoutXamlException()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var spinner = new RadialSpinner();
                spinner.Should().NotBeNull();
                spinner.Width.Should().Be(20);
                spinner.Height.Should().Be(20);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        exception.Should().BeNull();
    }
}
