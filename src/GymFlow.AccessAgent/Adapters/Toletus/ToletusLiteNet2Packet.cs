using System.Buffers.Binary;

namespace GymFlow.AccessAgent.Adapters.Toletus;

public sealed class ToletusLiteNet2Packet
{
    public const int PacketSize = 20;
    public const int DataSize = 16;

    public const byte Prefix = 83;
    public const byte Suffix = 195;

    public ushort Command { get; }

    public byte[] Data { get; }

    public ToletusLiteNet2Packet(
        ushort command,
        byte[]? data = null)
    {
        if (data is not null &&
            data.Length > DataSize)
        {
            throw new ArgumentException(
                "Os dados do pacote LiteNet2 não podem exceder 16 bytes.",
                nameof(data));
        }

        Command = command;
        Data = data ?? [];
    }

    public byte[] ToBytes()
    {
        var packet =
            new byte[PacketSize];

        packet[0] = Prefix;

        BinaryPrimitives.WriteUInt16LittleEndian(
            packet.AsSpan(1, 2),
            Command);

        Data.AsSpan()
            .CopyTo(
                packet.AsSpan(
                    3,
                    DataSize));

        packet[PacketSize - 1] =
            Suffix;

        return packet;
    }

    public static bool TryParse(
        ReadOnlySpan<byte> bytes,
        out ToletusLiteNet2Packet? packet)
    {
        packet = null;

        if (bytes.Length != PacketSize)
            return false;

        if (bytes[0] != Prefix ||
            bytes[PacketSize - 1] != Suffix)
        {
            return false;
        }

        var command =
            BinaryPrimitives.ReadUInt16LittleEndian(
                bytes.Slice(1, 2));

        var data =
            bytes.Slice(
                    3,
                    DataSize)
                .ToArray();

        packet =
            new ToletusLiteNet2Packet(
                command,
                data);

        return true;
    }
}