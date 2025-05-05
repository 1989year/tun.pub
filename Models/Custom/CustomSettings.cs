using System.Text.Json;

namespace tun.Models;

public class CustomSettings(IConfiguration cfg)
{
    public Guid Guid { get; set; } = Guid.TryParse(cfg["guid"], out var value) ? value : default;

    public Guid Token { get; set; } = Guid.TryParse(cfg["token"], out var value) ? value : default;

    public void Save()
    {
        using var fs = File.Open("appsettings.json", FileMode.OpenOrCreate);
        JsonSerializer.Serialize(fs, this, AppJsonSerializerContext.Default.CustomSettings);
    }
}