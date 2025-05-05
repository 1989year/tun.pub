using System.Reflection;
using System.Runtime.InteropServices;

namespace tun.Models;

public class CustomOSInformation
{
    public string OSDescription { get; set; } = RuntimeInformation.OSDescription;

    public string FrameworkDescription { get; set; } = RuntimeInformation.FrameworkDescription;

    public string RuntimeIdentifier { get; set; } = RuntimeInformation.RuntimeIdentifier;

    public string MachineName { get; set; } = Environment.MachineName;

    public string Version { get; set; } = Assembly.GetExecutingAssembly().GetName().Version.ToString();
}
