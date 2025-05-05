using System.Text.Json.Serialization;
namespace tun.Models;
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CustomOSInformation))]
[JsonSerializable(typeof(CustomSettings))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(Tunnel))]
[JsonSerializable(typeof(Tunnel[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;