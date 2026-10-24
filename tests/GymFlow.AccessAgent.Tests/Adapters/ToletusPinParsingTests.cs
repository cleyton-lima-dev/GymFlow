using System.Text;
using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Adapters.Toletus;

namespace GymFlow.AccessAgent.Tests.Adapters;

public class ToletusPinParsingTests
{
    [Theory]
    [InlineData("7", "0007")]
    [InlineData("007", "0007")]
    [InlineData("0007", "0007")]
    [InlineData("0000", "0000")]
    public void KeyboardPinPreservesFourDigits(string raw, string expected)
    {
        var packet = new ToletusLiteNet2Packet(
            ToletusLiteNet2Commands.NotifyKeyboard, Encoding.ASCII.GetBytes(raw));

        Assert.True(ToletusLiteNet2EventParser.TryParseAccessAttempt(packet, out var attempt));
        Assert.Equal(AccessCredentialType.PinExternalId, attempt!.CredentialType);
        Assert.Equal(expected, attempt.ExternalIdentifier);
    }

    [Theory]
    [InlineData("10000")]
    [InlineData("12AB")]
    public void InvalidKeyboardPinIsRejected(string raw)
    {
        var packet = new ToletusLiteNet2Packet(
            ToletusLiteNet2Commands.NotifyKeyboard, Encoding.ASCII.GetBytes(raw));
        Assert.False(ToletusLiteNet2EventParser.TryParseAccessAttempt(packet, out _));
    }
}
