using System.Net;
using System.Net.Sockets;
using System.Text;

namespace codecrafters_redis;

// Implements the connection logic for the socket server.
// After accepting a connection, all data read from the client
// is sent back to the client. The read and echo back to the client pattern
// is continued until the client disconnects.
public class Server
{
    private readonly int _mNumConnections;   // the maximum number of connections the sample is designed to handle simultaneously
    private int _mReceiveBufferSize;// buffer size to use for each socket I/O operation
    private readonly BufferManager _mBufferManager;  // represents a large reusable set of buffers for all socket operations
    private const int OpsToPreAlloc = 2;    // read, write (don't alloc buffer space for accepts)

    private Socket _listenSocket;            // the socket used to listen for incoming connection requests
    // pool of reusable SocketAsyncEventArgs objects for write, read and accept socket operations
    private readonly SocketAsyncEventArgsPool _mReadWritePool;
    private int _mTotalBytesRead;           // counter of the total # bytes received by the server
    private int _mNumConnectedSockets;      // the total number of clients connected to the server
    private readonly Semaphore _mMaxNumberAcceptedClients;

    // Create an uninitialized server instance.
    // To start the server listening for connection requests
    // call the Init method followed by Start method
    //
    // <param name="numConnections">the maximum number of connections the sample is designed to handle simultaneously</param>
    // <param name="receiveBufferSize">buffer size to use for each socket I/O operation</param>
    public Server(int numConnections, int receiveBufferSize)
    {
        _mTotalBytesRead = 0;
        _mNumConnectedSockets = 0;
        _mNumConnections = numConnections;
        _mReceiveBufferSize = receiveBufferSize;
        // allocate buffers such that the maximum number of sockets can have one outstanding read and
        //write posted to the socket simultaneously
        _mBufferManager = new BufferManager(receiveBufferSize * numConnections * OpsToPreAlloc,
            receiveBufferSize);

        _mReadWritePool = new SocketAsyncEventArgsPool(numConnections);
        _mMaxNumberAcceptedClients = new Semaphore(numConnections, numConnections);
    }

    // Initializes the server by preallocating reusable buffers and
    // context objects.  These objects do not need to be preallocated
    // or reused, but it is done this way to illustrate how the API can
    // easily be used to create reusable objects to increase server performance.
    //
    public void Init()
    {
        // Allocates one large byte buffer which all I/O operations use a piece of.  This gaurds
        // against memory fragmentation
        _mBufferManager.InitBuffer();

        // preallocate pool of SocketAsyncEventArgs objects
        SocketAsyncEventArgs readWriteEventArg;

        for (var i = 0; i < _mNumConnections; i++)
        {
            //Pre-allocate a set of reusable SocketAsyncEventArgs
            readWriteEventArg = new SocketAsyncEventArgs();
            readWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);

            // assign a byte buffer from the buffer pool to the SocketAsyncEventArg object
            _mBufferManager.SetBuffer(readWriteEventArg);

            // add SocketAsyncEventArg to the pool
            _mReadWritePool.Push(readWriteEventArg);
        }
    }

    // Starts the server such that it is listening for
    // incoming connection requests.
    //
    // <param name="localEndPoint">The endpoint which the server will listening
    // for connection requests on</param>
    public void Start(IPEndPoint localEndPoint)
    {
        // create the socket which listens for incoming connections
        _listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(localEndPoint);
        // start the server with a listen backlog of 100 connections
        _listenSocket.Listen(100);

        // post accepts on the listening socket
        var acceptEventArg = new SocketAsyncEventArgs();
        acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
        StartAccept(acceptEventArg);

        //Console.WriteLine("{0} connected sockets with one outstanding receive posted to each....press any key", m_outstandingReadCount);
        Console.WriteLine("Press any key to terminate the server process....");
        while (true)
        {
            
        }
    }

    // Begins an operation to accept a connection request from the client
    //
    // <param name="acceptEventArg">The context object to use when issuing
    // the accept operation on the server's listening socket</param>
    public void StartAccept(SocketAsyncEventArgs acceptEventArg)
    {
        // loop while the method completes synchronously
        var willRaiseEvent = false;
        while (!willRaiseEvent)
        {
            _mMaxNumberAcceptedClients.WaitOne();

            // socket must be cleared since the context object is being reused
            acceptEventArg.AcceptSocket = null;            
            willRaiseEvent = _listenSocket.AcceptAsync(acceptEventArg);
            if (!willRaiseEvent)
            {
                ProcessAccept(acceptEventArg);
            }
        }
    }

    // This method is the callback method associated with Socket.AcceptAsync
    // operations and is invoked when an accept operation is complete
    //
    private void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
    {
        ProcessAccept(e);
        
        // Accept the next connection request
        StartAccept(e);
    }

    private void ProcessAccept(SocketAsyncEventArgs e)
    {
        Interlocked.Increment(ref _mNumConnectedSockets);
        Console.WriteLine("Client connection accepted. There are {0} clients connected to the server",
            _mNumConnectedSockets);

        // Get the socket for the accepted client connection and put it into the
        //ReadEventArg object user token
        var readEventArgs = _mReadWritePool.Pop();
        readEventArgs.UserToken = e.AcceptSocket;

        // As soon as the client is connected, post a receive to the connection
        var willRaiseEvent = e.AcceptSocket.ReceiveAsync(readEventArgs);
        if (!willRaiseEvent)
        {
            ProcessReceive(readEventArgs);
        }
    }

    // This method is called whenever a receive or send operation is completed on a socket
    //
    // <param name="e">SocketAsyncEventArg associated with the completed receive operation</param>
    private void IO_Completed(object sender, SocketAsyncEventArgs e)
    {
        // determine which type of operation just completed and call the associated handler
        switch (e.LastOperation)
        {
            case SocketAsyncOperation.Receive:
                ProcessReceive(e);
                break;
            case SocketAsyncOperation.Send:
                ProcessSend(e);
                break;
            default:
                throw new ArgumentException("The last operation completed on the socket was not a receive or send");
        }
    }

    // This method is invoked when an asynchronous receive operation completes.
    // If the remote host closed the connection, then the socket is closed.
    // If data was received then the data is echoed back to the client.
    //
    private void ProcessReceive(SocketAsyncEventArgs e)
    {
        // check if the remote host closed the connection
        if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
        {
            //increment the count of the total bytes receive by the server
            Interlocked.Add(ref _mTotalBytesRead, e.BytesTransferred);
            Console.WriteLine("The server has read a total of {0} bytes", _mTotalBytesRead);
            
            var response = Encoding.UTF8.GetString(e.MemoryBuffer.ToArray(), e.Offset, e.BytesTransferred);
            
            Console.WriteLine(
                $"Socket server received message: \"{response}\"");
                
            // if (response.Contains("PING", StringComparison.Ordinal))
            // {
            //     
            // }
            
            var pong = "+PONG\r\n";
            var echoBytes = Encoding.UTF8.GetBytes(pong);

            e.SetBuffer(echoBytes);
            
            var socket = (Socket)e.UserToken;
            var willRaiseEvent = socket.SendAsync(e);
            if (!willRaiseEvent)
            {
                ProcessSend(e);
            }
        }
        else
        {
            CloseClientSocket(e);
        }
    }

    // This method is invoked when an asynchronous send operation completes.
    // The method issues another receive on the socket to read any additional
    // data sent from the client
    //
    // <param name="e"></param>
    private void ProcessSend(SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success)
        {
            // done echoing data back to the client
            var socket = (Socket)e.UserToken;
            // read the next block of data send from the client
            var willRaiseEvent = socket.ReceiveAsync(e);
            if (!willRaiseEvent)
            {
                ProcessReceive(e);
            }
        }
        else
        {
            CloseClientSocket(e);
        }
    }

    private void CloseClientSocket(SocketAsyncEventArgs e)
    {
        var socket = (Socket)e.UserToken;

        // close the socket associated with the client
        try
        {
            socket.Shutdown(SocketShutdown.Send);
        }
        // throws if client process has already closed
        catch (Exception) { }
        socket.Close();

        // decrement the counter keeping track of the total number of clients connected to the server
        Interlocked.Decrement(ref _mNumConnectedSockets);

        // Free the SocketAsyncEventArg so they can be reused by another client
        _mReadWritePool.Push(e);

        _mMaxNumberAcceptedClients.Release();
        Console.WriteLine("A client has been disconnected from the server. There are {0} clients connected to the server", _mNumConnectedSockets);
    }
}