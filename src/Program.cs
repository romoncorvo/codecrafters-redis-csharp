using System.Net;
using System.Net.Sockets;
using System.Text;

namespace codecrafters_redis;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        IPEndPoint ipEndPoint = new(IPAddress.Any, 6379);
        
        using Socket listener = new(
            ipEndPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);
        
        const string eom = "<|EOM|>";
        
        try
        {
            listener.Bind(ipEndPoint);
            listener.Listen(100);
            var handler = await listener.AcceptAsync();
            
            while (true)
            {
                var buffer = new byte[1_024];
                var received = await handler.ReceiveAsync(buffer, SocketFlags.None);
                var response = Encoding.UTF8.GetString(buffer, 0, received);
                
                if (response.Contains("PING", StringComparison.Ordinal))
                {
                    Console.WriteLine(
                        $"Socket server received message: \"{response}\"");
                    
                    var pong = "+PONG\r\n";
                    var echoBytes = Encoding.UTF8.GetBytes(pong);
                    await handler.SendAsync(echoBytes, 0);
                    Console.WriteLine(
                        $"Socket server sent acknowledgment: \"{pong}\"");
                }
            }
        }
        finally
        {
            listener.Close();
        }
    }
}