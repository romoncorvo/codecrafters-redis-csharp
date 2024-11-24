using System.Net;
using System.Net.Sockets;
using System.Text;

namespace codecrafters_redis;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var server = new Server(8, 1024);
        server.Init();
        IPEndPoint ipEndPoint = new(IPAddress.Any, 6379);
        server.Start(ipEndPoint);
    }
}