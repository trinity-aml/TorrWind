using System.ComponentModel;
using TorrWind.Core.Models;

namespace TorrWind.Core.Tests;

public sealed class ServerProfileTests
{
    [Theory]
    [InlineData("", "http://127.0.0.1:8090/")]
    [InlineData("   ", "http://127.0.0.1:8090/")]
    [InlineData("127.0.0.1:8090", "http://127.0.0.1:8090/")]
    [InlineData("192.168.1.2:8090/", "http://192.168.1.2:8090/")]
    [InlineData("media.local:8090/base", "http://media.local:8090/base/")]
    [InlineData("https://media.local:9443", "https://media.local:9443/")]
    [InlineData("http://media.local:8090/base/", "http://media.local:8090/base/")]
    public void BaseUri_NormalizesAddressForRemoteServerProfiles(string baseUrl, string expected)
    {
        var profile = new ServerProfile { BaseUrl = baseUrl };

        Assert.Equal(expected, profile.BaseUri.AbsoluteUri);
    }

    [Fact]
    public void BaseUrlChange_RaisesBaseUriPropertyChanged()
    {
        var profile = new ServerProfile();
        var changedProperties = new List<string?>();
        profile.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        profile.BaseUrl = "192.168.1.2:8090";

        Assert.Contains(nameof(ServerProfile.BaseUrl), changedProperties);
        Assert.Contains(nameof(ServerProfile.BaseUri), changedProperties);
    }

    [Fact]
    public void BaseUrlChange_DoesNotRaiseWhenValueIsUnchanged()
    {
        var profile = new ServerProfile { BaseUrl = "http://127.0.0.1:8090" };
        var eventRaised = false;
        profile.PropertyChanged += (_, _) => eventRaised = true;

        profile.BaseUrl = "http://127.0.0.1:8090";

        Assert.False(eventRaised);
    }
}
