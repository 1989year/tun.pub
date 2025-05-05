using System.Buffers;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace tun.Models;

public static class Extensions
{
    public static void CancelAndDispose(this CancellationTokenSource cts)
    {
        try {
            cts?.Cancel();
        } catch (Exception) {
        } finally {
            cts?.Dispose();
        }
    }

    public static void ForEach<T>(this T[] array, Action<T> action)
    {
        foreach (var item in array) {
            action(item);
        }
    }

    public static string Query(this string[] args, string key, string defaultValue = null)
    {
        try {
            for (int i = 0; i < args.Length; i++) {
                if (args[i] == key) {
                    return args[i + 1];
                }
            }
        } catch (Exception) {
        }
        return defaultValue;
    }

    public static async Task CopyToAsync(this Socket source, Socket destination, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(destination.ReceiveBufferSize);
        try {
            int bytesRead;
            while ((bytesRead = await source.ReceiveAsync(new Memory<byte>(buffer), cancellationToken).ConfigureAwait(false)) != 0) {
                await destination.SendAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken).ConfigureAwait(false);
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async Task CopyToAsync(this Socket source, UdpClient destination, CancellationToken cancellationToken)
    {
        int err = 0;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(source.ReceiveBufferSize);
        try {
            int bytesRead;
            while ((bytesRead = await source.ReceiveAsync(new Memory<byte>(buffer), cancellationToken).ConfigureAwait(false)) != 0) {
                try {
                    await destination.SendAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken).ConfigureAwait(false);
                    err = 0;
                } catch (Exception) {
                    if (err++ > 6) throw;
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async Task CopyToAsync(this UdpClient source, Socket destination, CancellationToken cancellationToken)
    {
        int err = 0;
        while (!cancellationToken.IsCancellationRequested) {
            UdpReceiveResult response;
            try {
                response = await source.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                err = 0;
            } catch (Exception) {
                if (err++ > 6) throw;
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                continue;
            }
            await destination.SendAsync(response.Buffer, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task SendAsync(this ClientWebSocket websocket, Guid taskid, int action, byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(24 + data.Length);
        try {
            taskid.ToByteArray().CopyTo(buffer, 0);
            BitConverter.GetBytes(action).CopyTo(buffer, 16);
            BitConverter.GetBytes(data.Length).CopyTo(buffer, 20);
            data.CopyTo(buffer, 24);
            await websocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}