using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using tun.Models;

namespace tun;

public class Worker(CustomSettings settings, ILogger<Worker> logger) : BackgroundService
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    public static readonly ConcurrentDictionary<Guid, CancellationTokenSource> Tunnels = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var ws = new ClientWebSocket();
                ws.Options.KeepAliveInterval = TimeSpan.FromMinutes(1);
                await ws.ConnectAsync(new Uri($"wss://tun.pub/{settings.Token}/{settings.Guid}?_={Environment.TickCount64}"), stoppingToken);
                await ReceiveMessageAsync(ws, stoppingToken);
            } catch (Exception ex) {
                logger.LogError("{ex}", ex.Message);
            } finally {
                await Task.Delay(6000, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ReceiveMessageAsync(ClientWebSocket ws, CancellationToken stoppingToken)
    {
        var startAndStopCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        try {
            while (!stoppingToken.IsCancellationRequested) {
                var buffer = new byte[32768];
                var result = await ws.ReceiveAsync(buffer, stoppingToken).ConfigureAwait(false);
                ArgumentOutOfRangeException.ThrowIfLessThan(result.Count, 24);
                var taskid = new Guid(buffer.AsSpan(0, 16));
                var action = BitConverter.ToInt32(buffer, 16);
                var length = BitConverter.ToInt32(buffer, 20);
                var data = new byte[length];
                if (length > 0) {
                    Buffer.BlockCopy(buffer, 24, data, 0, length);
                }
                logger.LogInformation("{b64}", Convert.ToBase64String(data));
                switch (action) {
                    // 释放实例
                    case 0x00:
                        Environment.Exit(0);
                        break;
                    // 接入成功
                    case 0x01:
                        settings.Guid = new Guid(data);
                        settings.Save();
                        await ws.SendAsync(default, action, JsonSerializer.SerializeToUtf8Bytes(new(), AppJsonSerializerContext.Default.CustomOSInformation), stoppingToken);
                        break;
                    // 启动实例
                    case 0x02:
                        await _semaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                        try {
                            JsonSerializer.Deserialize(data, AppJsonSerializerContext.Default.TunnelArray).ForEach(tnl => _ = tnl.StartAsync(startAndStopCts.Token));
                        } finally {
                            _semaphore.Release();
                        }
                        await ws.SendAsync(taskid, action, [], stoppingToken);
                        break;
                    // 停止实例
                    case 0x03:
                        await _semaphore.WaitAsync(stoppingToken).ConfigureAwait(false);
                        try {
                            startAndStopCts.CancelAndDispose();
                            startAndStopCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        } finally {
                            _semaphore.Release();
                        }
                        await ws.SendAsync(taskid, action, [], stoppingToken);
                        break;
                    // 启动隧道
                    case 0x05:
                        _ = JsonSerializer.Deserialize(data, AppJsonSerializerContext.Default.Tunnel).StartAsync(startAndStopCts.Token);
                        await ws.SendAsync(taskid, action, [], stoppingToken);
                        break;
                    // 停止隧道
                    case 0x06:
                        JsonSerializer.Deserialize(data, AppJsonSerializerContext.Default.Tunnel).Stop();
                        await ws.SendAsync(taskid, action, [], stoppingToken);
                        break;
                }
            }
        } finally {
            startAndStopCts.CancelAndDispose();
        }
    }
}