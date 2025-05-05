using System.Net.Sockets;
using System.Net.WebSockets;

namespace tun.Models;

public class Tunnel
{
    public string Server { get; set; }

    public Guid Token { get; set; }

    public string Host { get; set; }

    public int Port { get; set; }

    public void Stop()
    {
        if (Worker.Tunnels.TryRemove(this.Token, out var cts)) {
            try {
                cts.Cancel();
            } catch (Exception) {
            }
        }
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        if (Worker.Tunnels.ContainsKey(this.Token)) {
            return;
        }
        if (string.IsNullOrWhiteSpace(this.Server)) {
            var doh = await Doh.GetTunnelsAsync();
            this.Server = doh.First().Key;
        }
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        if (Worker.Tunnels.TryAdd(this.Token, cts)) {
            try {
                while (!cts.IsCancellationRequested) {
                    try {
                        using var ws = new ClientWebSocket();
                        ws.Options.KeepAliveInterval = TimeSpan.FromMinutes(1);
                        await ws.ConnectAsync(new Uri($"ws://{this.Server}:8080/v2/{this.Token}?_={Environment.TickCount64}"), cts.Token).ConfigureAwait(false);
                        await ReceiveMessageAsync(ws, cts.Token).ConfigureAwait(false);
                    } catch (Exception) {
                    } finally {
                        await Task.Delay(6000, cts.Token).ConfigureAwait(false);
                    }
                }
            } finally {
                Worker.Tunnels.TryRemove(this.Token, out _);
            }
        }
    }

    private async Task ReceiveMessageAsync(ClientWebSocket ws, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) {
            var buffer = new byte[32768];
            var result = await ws.ReceiveAsync(buffer, stoppingToken).ConfigureAwait(false);
            ArgumentOutOfRangeException.ThrowIfLessThan(result.Count, 8);
            var action = BitConverter.ToInt32(buffer, 0);
            var length = BitConverter.ToInt32(buffer, 4);
            var data = new byte[length];
            if (length > 0) {
                Buffer.BlockCopy(buffer, 8, data, 0, length);
            }
            switch (action) {
                // TCP Accept
                case 200:
                    _ = ExchangeAsync(data, stoppingToken);
                    break;
            }
        }
    }

    private async Task ExchangeAsync(byte[] data, CancellationToken stoppingToken)
    {
        using var local = CreateSocketInstance(262144);
        await local.ConnectAsync(this.Host, this.Port, stoppingToken).ConfigureAwait(false);
        using var server = CreateSocketInstance(262144);
        await server.ConnectAsync(this.Server, 1024, stoppingToken).ConfigureAwait(false);
        await server.SendAsync(data, stoppingToken).ConfigureAwait(false);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        try {
            await Task.WhenAny(server.CopyToAsync(local, cts.Token), local.CopyToAsync(server, cts.Token)).ConfigureAwait(false);
        } finally {
            cts.Cancel();
        }
    }

    private static Socket CreateSocketInstance(int bufferSize)
    {
        return new Socket(SocketType.Stream, ProtocolType.Tcp) {
            ReceiveBufferSize = bufferSize,
            SendBufferSize = bufferSize,
            Blocking = false,
            NoDelay = true,
            LingerState = new LingerOption(true, 0)
        };
    }
}