using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class WindowsServiceSecurityDescriptorTests
{
    [Fact]
    public void ExtractFromScOutput_ReturnsDescriptorLine()
    {
        const string descriptor = "D:(A;;CCLCSWLOCRRC;;;IU)S:(AU;FA;CC;;;WD)";
        var output = "[SC] QueryServiceObjectSecurity SUCCESS\r\n\r\n" + descriptor + "\r\n";

        Assert.Equal(descriptor, WindowsServiceSecurityDescriptor.ExtractFromScOutput(output));
    }

    [Fact]
    public void GrantInteractiveStartStop_InsertsAllowAceBeforeSacl()
    {
        const string descriptor = "O:SYG:SYD:(A;;CCLCSWLOCRRC;;;IU)S:(AU;FA;CC;;;WD)";

        var updated = WindowsServiceSecurityDescriptor.GrantInteractiveStartStop(descriptor);

        Assert.Equal(
            "O:SYG:SYD:(A;;CCLCSWLOCRRC;;;IU)(A;;LCRPWP;;;IU)S:(AU;FA;CC;;;WD)",
            updated);
    }

    [Theory]
    [InlineData("D:(A;;LCRPWP;;;IU)")]
    [InlineData("D:(A;;CCLCSWRPWPDTLOCRRC;;;IU)")]
    [InlineData("D:(A;;0x34;;;S-1-5-4)")]
    public void GrantInteractiveStartStop_DoesNotDuplicateExistingAccess(string descriptor)
    {
        Assert.Equal(descriptor, WindowsServiceSecurityDescriptor.GrantInteractiveStartStop(descriptor));
    }

    [Fact]
    public void GrantInteractiveStartStop_RejectsDescriptorWithoutDacl()
    {
        Assert.Throws<FormatException>(() =>
            WindowsServiceSecurityDescriptor.GrantInteractiveStartStop("O:SYG:SY"));
    }
}
