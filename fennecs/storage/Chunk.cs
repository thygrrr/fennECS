namespace fennecs.storage;

/// <summary>
/// A fixed-size storage that contains an array of elements.
/// Not all elements may be used.
/// </summary>
/// <typeparam name="T">type of the values stored</typeparam>
public sealed class Chunk<T> : IRefIndexable<T>, IDisposable where T : notnull
{
    private T[] _data = ChunkArrayPool<T>.Rent();

    /// <summary>
    /// Elements currently in this Chunk
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Is this chunk empty?
    /// </summary>
    public bool Empty => Count == 0;
    
    /// <summary>
    /// Is this chunk full?
    /// </summary>
    public bool Full => Count == _data.Length;
    
    /// <inheritdoc />
    public ref T this[int index] => ref _data[index];
    
    /// <summary>
    /// Add a value to the end of the chunk
    /// </summary>
    /// <param name="value"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void Add(T value)
    {
        if (Full) throw new InvalidOperationException("Chunk is full");
        _data[Count++] = value;
    }

    /// <summary>
    /// Remove the value at the index.
    /// </summary>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Count--;

        if (index != Count)
            _data[index] = _data[Count];
    }


    /// <inheritdoc />
    public void Dispose()
    {
        ChunkArrayPool<T>.Return(_data);
        _data = null!;
    }
}