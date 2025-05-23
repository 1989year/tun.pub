﻿using tun;
using tun.Models;
Console.Clear();
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService().AddSystemd();
builder.Services.AddSingleton<CustomSettings>();
builder.Services.AddHostedService<Worker>();
var host = builder.Build();
host.Run();