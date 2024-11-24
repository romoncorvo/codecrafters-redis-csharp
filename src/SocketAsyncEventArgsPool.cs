using System.Net.Sockets;

namespace codecrafters_redis;

// Represents a collection of reusable SocketAsyncEventArgs objects.
public class SocketAsyncEventArgsPool
{
    private readonly Stack<SocketAsyncEventArgs> _mPool;

    // Initializes the object pool to the specified size
    //
    // The "capacity" parameter is the maximum number of
    // SocketAsyncEventArgs objects the pool can hold
    public SocketAsyncEventArgsPool(int capacity)
    {
        _mPool = new Stack<SocketAsyncEventArgs>(capacity);
    }

    // Add a SocketAsyncEventArg instance to the pool
    //
    //The "item" parameter is the SocketAsyncEventArgs instance
    // to add to the pool
    public void Push(SocketAsyncEventArgs item)
    {
        if (item == null) { throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null"); }
        lock (_mPool)
        {
            _mPool.Push(item);
        }
    }

    // Removes a SocketAsyncEventArgs instance from the pool
    // and returns the object removed from the pool
    public SocketAsyncEventArgs Pop()
    {
        lock (_mPool)
        {
            return _mPool.Pop();
        }
    }

    // The number of SocketAsyncEventArgs instances in the pool
    public int Count
    {
        get { return _mPool.Count; }
    }
}