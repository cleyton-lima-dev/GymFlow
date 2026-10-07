using System.Buffers.Binary;
using System.Text;
using GymFlow.AccessAgent.Abstractions;

namespace GymFlow.AccessAgent.Adapters.Toletus;

public static class ToletusLiteNet2EventParser
{
    public static bool TryParseAccessAttempt(
        ToletusLiteNet2Packet packet,
        out DeviceAccessAttempt? attempt)
    {
        attempt = null;

        switch (packet.Command)
        {
            case ToletusLiteNet2Commands.NotifyRfid:
                return TryCreateTextAttempt(
                    packet,
                    AccessCredentialType.Card,
                    out attempt);

            case ToletusLiteNet2Commands.NotifyBarcode:
                return TryCreateTextAttempt(
                    packet,
                    AccessCredentialType.Card,
                    out attempt);

            case ToletusLiteNet2Commands.NotifyKeyboard:
                return TryCreateTextAttempt(
                    packet,
                    AccessCredentialType.PinExternalId,
                    out attempt);

            case ToletusLiteNet2Commands.NotifyBiometric:
                return TryCreateBiometricAttempt(
                    packet,
                    out attempt);

            default:
                return false;
        }
    }

    private static bool TryCreateTextAttempt(
        ToletusLiteNet2Packet packet,
        AccessCredentialType credentialType,
        out DeviceAccessAttempt? attempt)
    {
        attempt = null;

        var externalIdentifier =
            Encoding.ASCII
                .GetString(packet.Data)
                .TrimEnd('\0', ' ');

        if (string.IsNullOrWhiteSpace(externalIdentifier))
            return false;

        attempt =
            new DeviceAccessAttempt(
                Guid.NewGuid(),
                externalIdentifier,
                credentialType,
                DateTime.UtcNow);

        return true;
    }

    private static bool TryCreateBiometricAttempt(
        ToletusLiteNet2Packet packet,
        out DeviceAccessAttempt? attempt)
    {
        attempt = null;

        if (packet.Data.Length < 2)
            return false;

        var biometricId =
            BinaryPrimitives.ReadUInt16LittleEndian(
                packet.Data.AsSpan(0, 2));

        attempt =
            new DeviceAccessAttempt(
                Guid.NewGuid(),
                biometricId.ToString(),
                AccessCredentialType.BiometricExternalId,
                DateTime.UtcNow);

        return true;
    }
}