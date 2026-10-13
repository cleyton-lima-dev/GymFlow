using GymFlow.Api.Platform;
using GymFlow.Application.Security;

namespace GymFlow.IntegrationTests.Security;

public sealed class PlatformBootstrapMfaTests
{
    // RFC 6238 SHA-1 key: ASCII "12345678901234567890", encoded in Base32.
    private const string Secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59L, "94287082")]
    [InlineData(1111111109L, "07081804")]
    [InlineData(1111111111L, "14050471")]
    [InlineData(1234567890L, "89005924")]
    [InlineData(2000000000L, "69279037")]
    [InlineData(20000000000L, "65353130")]
    public void Rfc6238_Sha1VectorsAndSixDigitValidation(long seconds, string expected)
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(seconds);
        Assert.Equal(expected, PlatformTotp.Generate(Secret, seconds / 30, 8));
        Assert.Equal(expected[^6..], PlatformTotp.Generate(Secret, seconds / 30));
        Assert.Equal(seconds / 30, PlatformTotp.Validate(Secret, expected[^6..], now, -1));
    }

    [Fact]
    public void NewSecret_IsTwentyByteUnpaddedBase32()
    {
        var secret = PlatformTotp.NewSecret();
        Assert.Equal(32, secret.Length);
        Assert.All(secret, c => Assert.Contains(c, "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"));
        var now = DateTimeOffset.FromUnixTimeSeconds(1234567890);
        Assert.NotNull(PlatformTotp.Validate(secret, PlatformTotp.Generate(secret, now.ToUnixTimeSeconds() / 30), now, -1));
    }

    [Theory]
    [InlineData(-2, false)]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Validation_OnlyAcceptsAdjacentThirtySecondSteps(int offset, bool accepted)
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1234567890);
        var step = now.ToUnixTimeSeconds() / 30;
        var code = PlatformTotp.Generate(Secret, step + offset);
        Assert.Equal(accepted, PlatformTotp.Validate(Secret, code, now, -1).HasValue);
        Assert.Equal(PlatformTotp.Validate(Secret, code, now, -1),
            PlatformTotp.Validate(Secret, code, now.ToOffset(TimeSpan.FromHours(-3)), -1));
    }

    [Fact]
    public void Validation_RejectsReplayAndCodeFromPreviousBootstrapSecret()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1234567890);
        var step = now.ToUnixTimeSeconds() / 30;
        var code = PlatformTotp.Generate(Secret, step);
        Assert.Null(PlatformTotp.Validate(Secret, code, now, step));
        Assert.Null(PlatformTotp.Validate("JBSWY3DPEHPK3PXP", code, now, -1));
    }

    [Fact]
    public void Bootstrap_BufferedEnterAndMalformedInputDoNotSubmitMfa()
    {
        var inputs = new Queue<string>(["", "\r\n", "123", "１２３４５６", "123abc", " 287082 "]);
        var prompts = new List<string>();
        var code = PlatformBootstrap.ReadMfaCode(inputs.Dequeue, prompts.Add);
        Assert.Equal("287082", code);
        Assert.Empty(inputs);
        Assert.Equal(6, prompts.Count(x => x.StartsWith("Código")));
        Assert.NotNull(PlatformTotp.Validate(Secret, code, DateTimeOffset.FromUnixTimeSeconds(59), -1));
    }

    [Fact]
    public void Bootstrap_ClockIsEvaluatedAfterInputAndLeadingZeroIsPreserved()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(0);
        var code = PlatformBootstrap.ReadMfaCode(() =>
        {
            now = DateTimeOffset.FromUnixTimeSeconds(1111111109);
            return "081804";
        }, _ => { });
        Assert.Equal("081804", code);
        Assert.NotNull(PlatformTotp.Validate(Secret, code, now, -1));
    }
}
