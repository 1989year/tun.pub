namespace tun.Models;

public static class Doh
{
    public static async Task<KeyValuePair<string, double>[]> GetTunnelsAsync()
    {
        var list = new Dictionary<string, double>();
        using (var net = new HttpClient()) {
            try {
                var text = await net.GetStringAsync($"https://tun.pub/subscribe?_={Environment.TickCount}").ConfigureAwait(false);
                foreach (var item in text.Split("\n")) {
                    try {
                        var st = DateTime.Now;
                        var t = Environment.TickCount;
                        if (t.ToString("x") != await net.GetStringAsync($"http://{item}:8080/verify/{Environment.TickCount}", CancellationToken.None).ConfigureAwait(false)) {
                            throw null;
                        }
                        list.Add(item, (DateTime.Now - st).TotalMilliseconds);
                    } catch (Exception) {
                    }
                }
            } catch (Exception) {
            }
        }
        return list.OrderBy(x => x.Value).ToArray();
    }
}