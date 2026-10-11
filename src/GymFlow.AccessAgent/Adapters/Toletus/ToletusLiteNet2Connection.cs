using System.Net.Sockets;

namespace GymFlow.AccessAgent.Adapters.Toletus;

public sealed class ToletusLiteNet2Connection : IAsyncDisposable
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;

    public bool IsConnected =>
        _tcpClient?.Connected == true &&
        _stream is not null;

    public async Task ConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        await DisposeConnectionAsync();

        var client = new TcpClient();

        try
        {
            await client.ConnectAsync(
                host,
                port,
                cancellationToken);

            _tcpClient = client;
            _stream = client.GetStream();
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task<ToletusLiteNet2Packet> ReadPacketAsync(
        CancellationToken cancellationToken)
    {
        var stream = GetStream();

        var buffer =
            new byte[
                ToletusLiteNet2Packet.PacketSize];

        await stream.ReadExactlyAsync(
            buffer,
            cancellationToken);

        if (!ToletusLiteNet2Packet.TryParse(
                buffer,
                out var packet) ||
            packet is null)
        {
            throw new InvalidDataException(
                "Pacote inválido recebido da LiteNet2.");
        }

        return packet;
    }

    public async Task SendPacketAsync(
        ToletusLiteNet2Packet packet,
        CancellationToken cancellationToken)
    {
        var stream = GetStream();
        var bytes = packet.ToBytes();

        await _writeLock.WaitAsync(
            cancellationToken);

        try
        {
            await stream.WriteAsync(
                bytes,
                cancellationToken);

            await stream.FlushAsync(
                cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeConnectionAsync();
        _writeLock.Dispose();

        GC.SuppressFinalize(this);
    }

    private NetworkStream GetStream()
    {
        return _stream ??
            throw new InvalidOperationException(
                "Não existe conexão ativa com a LiteNet2.");
    }

    private ValueTask DisposeConnectionAsync()
    {
        _stream?.Dispose();
        _tcpClient?.Dispose();

        _stream = null;
        _tcpClient = null;

        return ValueTask.CompletedTask;
    }
}