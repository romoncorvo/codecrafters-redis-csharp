using System.Net;
using System.Net.Sockets;

var server = new TcpListener(IPAddress.Any, 6379);
server.Start();
server.AcceptSocket(); // wait for client
