using GymFlow.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace GymFlow.IntegrationTests.Security;

public class ConfigurationGymErpPolicyTests
{
    [Fact]
    public void EmptyJsonObject_DoesNotEnableAnyTenant()
    {
        using var json = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"GymFeatures\":{}}"));
        var policy = new ConfigurationGymErpPolicy(new ConfigurationBuilder().AddJsonStream(json).Build());
        Assert.False(policy.IsEnabled(Guid.NewGuid()));
    }

    [Fact]
    public void EmptyConfiguration_AndOldGlobalFlag_DoNotEnableAnyTenant()
    {
        var policy = Create(new() { ["EnrollmentAccess:EnforceStudentEnrollment"] = "true" });
        Assert.False(policy.IsEnabled(Guid.NewGuid()));
        Assert.False(policy.IsEnabled(Guid.Empty));
    }

    [Fact]
    public void OnlyExplicitTrueEnablesItsOwnTenant_AndSnapshotDoesNotReload()
    {
        var enabled = Guid.NewGuid();
        var disabled = Guid.NewGuid();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"GymFeatures:{enabled:D}:ErpEnabled"] = "true",
            [$"GymFeatures:{disabled:D}:ErpEnabled"] = "false"
        }).Build();
        var policy = new ConfigurationGymErpPolicy(configuration);

        Assert.True(policy.IsEnabled(enabled));
        Assert.False(policy.IsEnabled(disabled));
        Assert.False(policy.IsEnabled(Guid.NewGuid()));
        configuration[$"GymFeatures:{disabled:D}:ErpEnabled"] = "true";
        Assert.False(policy.IsEnabled(disabled));
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData("not-a-boolean")]
    [InlineData(null)]
    public void InvalidFlagFailsConstruction(string? value)
    {
        Assert.Throws<InvalidOperationException>(() => Create(new()
        {
            [$"GymFeatures:{Guid.NewGuid():D}:ErpEnabled"] = value
        }));
    }

    [Fact]
    public void ScalarTenantSectionFailsConstruction()
    {
        Assert.Throws<InvalidOperationException>(() => Create(new()
        {
            [$"GymFeatures:{Guid.NewGuid():D}"] = "true"
        }));
    }

    [Theory]
    [InlineData("default")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void InvalidTenantKeyFailsConstruction(string key)
    {
        Assert.Throws<InvalidOperationException>(() => Create(new()
        {
            [$"GymFeatures:{key}:ErpEnabled"] = "true"
        }));
    }

    [Fact]
    public void NestedFlagFailsConstruction()
    {
        Assert.Throws<InvalidOperationException>(() => Create(new()
        {
            [$"GymFeatures:{Guid.NewGuid():D}:ErpEnabled:Value"] = "true"
        }));
    }

    private static ConfigurationGymErpPolicy Create(Dictionary<string, string?> values) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    [Theory]
    [InlineData("ErpEnable")]
    [InlineData("UnknownFeature")]
    [InlineData("ErpEnabledExtra")]
    public void UnknownFeatureFailsConstruction(string name)
    {
        Assert.Throws<InvalidOperationException>(() => Create(new()
        {
            [$"GymFeatures:{Guid.NewGuid():D}:{name}"] = "true"
        }));
    }

    [Fact]
    public void UnknownFeatureAlongsideValidFlagAlsoFailsConstruction()
    {
        var gym = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => Create(new()
        {
            [$"GymFeatures:{gym:D}:ErpEnabled"] = "false",
            [$"GymFeatures:{gym:D}:ErpEnable"] = "true"
        }));
    }

    [Fact]
    public void RecognizedFeatureIsCaseInsensitive()
    {
        var gym = Guid.NewGuid();
        Assert.True(Create(new() { [$"GymFeatures:{gym:D}:erpenabled"] = "true" }).IsEnabled(gym));
    }
}
