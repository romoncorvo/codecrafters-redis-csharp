using System.Net.Sockets;

namespace codecrafters_redis;

// This class creates a single large buffer which can be divided up
// and assigned to SocketAsyncEventArgs objects for use with each
// socket I/O operation.
// This enables bufffers to be easily reused and guards against
// fragmenting heap memory.
//
// The operations exposed on the BufferManager class are not thread safe.
public class BufferManager
{
    private readonly int _mNumBytes;                 // the total number of bytes controlled by the buffer pool
    private byte[] _mBuffer;                        // the underlying byte array maintained by the Buffer Manager
    private readonly Stack<int> _mFreeIndexPool;     //
    private int _mCurrentIndex;
    private readonly int _mBufferSize;

    public BufferManager(int totalBytes, int bufferSize)
    {
        _mNumBytes = totalBytes;
        _mCurrentIndex = 0;
        _mBufferSize = bufferSize;
        _mFreeIndexPool = new Stack<int>();
    }

    // Allocates buffer space used by the buffer pool
    public void InitBuffer()
    {
        // create one big large buffer and divide that
        // out to each SocketAsyncEventArg object
        _mBuffer = new byte[_mNumBytes];
    }

    // Assigns a buffer from the buffer pool to the
    // specified SocketAsyncEventArgs object
    //
    // <returns>true if the buffer was successfully set, else false</returns>
    public bool SetBuffer(SocketAsyncEventArgs args)
    {

        if (_mFreeIndexPool.Count > 0)
        {
            args.SetBuffer(_mBuffer, _mFreeIndexPool.Pop(), _mBufferSize);
        }
        else
        {
            if ((_mNumBytes - _mBufferSize) < _mCurrentIndex)
            {
                return false;
            }
            args.SetBuffer(_mBuffer, _mCurrentIndex, _mBufferSize);
            _mCurrentIndex += _mBufferSize;
        }
        return true;
    }

    // Removes the buffer from a SocketAsyncEventArg object.
    // This frees the buffer back to the buffer pool
    public void FreeBuffer(SocketAsyncEventArgs args)
    {
        _mFreeIndexPool.Push(args.Offset);
        args.SetBuffer(null, 0, 0);
    }
}