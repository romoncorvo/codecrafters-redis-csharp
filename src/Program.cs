using System.Net;
using System.Net.Sockets;
using System.Text;

namespace codecrafters_redis;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var listener  = new TcpListener(IPAddress.Any, 6379);
        
        try
        {
            listener.Start();

            using var handler = await listener.AcceptTcpClientAsync();
            await using var stream = handler.GetStream();

            var message = "+PONG\r\n";
            var dateTimeBytes = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(dateTimeBytes);
        }
        finally
        {
            listener.Stop();
        }
    }
}