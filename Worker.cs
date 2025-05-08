using System.Net.WebSockets;
using System.Text.Json;
using tun.Models;

namespace tun;

public class Worker(CustomSettings settings, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var websocket = new ClientWebSocket();
                websocket.Options.KeepAliveInterval = TimeSpan.FromMinutes(1);
                await websocket.ConnectAsync(new Uri($"wss://tun.pub/{settings.Token}/{settings.Guid}?_={Environment.TickCount64}"), stoppingToken);
                await ReceiveMessageAsync(websocket, stoppingToken);
            } catch (Exception ex) {
                logger.LogError("{ex}", ex.Message);
            } finally {
                await Task.Delay(6000, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ReceiveMessageAsync(ClientWebSocket websocket, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) {
            var buffer = new byte[32768];
            var result = await websocket.ReceiveAsync(buffer, stoppingToken).ConfigureAwait(false);
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
                    await websocket.SendAsync(default, action, JsonSerializer.SerializeToUtf8Bytes(new(), AppJsonSerializerContext.Default.CustomOSInformation), stoppingToken);
                    break;
                // 启动实例
                case 0x02:
                    JsonSerializer.Deserialize(data, AppJsonSerializerContext.Default.TunnelArray)
                        .ForEach(tnl => _ = tnl.StartAsync(stoppingToken));
                    break;
                // 启动隧道
                case 0x05:
                    _ = JsonSerializer.Deserialize(data, AppJsonSerializerContext.Default.Tunnel).StartAsync(stoppingToken);
                    await websocket.SendAsync(taskid, action, [], stoppingToken);
                    break;
                // 停止隧道
                case 0x06:
                    JsonSerializer.Deserialize(data, AppJsonSerializerContext.Default.Tunnel).Stop();
                    await websocket.SendAsync(taskid, action, [], stoppingToken);
                    break;
            }
        }
    }
}