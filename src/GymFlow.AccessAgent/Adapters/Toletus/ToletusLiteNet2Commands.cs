namespace GymFlow.AccessAgent.Adapters.Toletus;

public static class ToletusLiteNet2Commands
{
    public const ushort ReleaseEntry = 0x0001;
    public const ushort ReleaseExit = 0x0002;
    public const ushort ReleaseBidirectional = 0x0006;

    public const ushort QueryDeviceId = 0x0103;
    public const ushort QueryFirmwareVersion = 0x010C;
    public const ushort QuerySerialNumber = 0x010D;
    public const ushort QueryInitializationState = 0x0112;

    public const ushort NotifyRfid = 0x0301;
    public const ushort NotifyBarcode = 0x0302;
    public const ushort NotifyKeyboard = 0x0303;
    public const ushort NotifyPassage = 0x0304;
    public const ushort NotifyReleaseTimeout = 0x0305;
    public const ushort NotifyBiometric = 0x0306;
    public const ushort NotifyUnknownBiometric = 0x0307;
}