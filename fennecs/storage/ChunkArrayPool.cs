using System.Collections.Concurrent;

namespace fennecs.storage;

internal static class ChunkArrayPool<T> where T : notnull
{
    // ReSharper disable once StaticMemberInGenericType
    public static int MinChunks = 128;
    // ReSharper disable once StaticMemberInGenericType
    public static int MaxChunks = 1024;

    private const int ChunkSize = 1024;

    private static readonly ConcurrentBag<T[]> Pool = [];
    
    static ChunkArrayPool()
    {
        for (var i = 0; i < MinChunks; i++) Pool.Add(new T[ChunkSize]);
    }

    /// <summary>
    /// Rent a chunk from the pool.
    /// </summary>
    public static T[] Rent()
    {
        return Pool.TryTake(out var chunk) ? chunk : new T[ChunkSize];
    }
    
    /// <summary>
    /// Return a chunk to the pool.
    /// </summary>
    public static void Return(T[] chunk)
    {
        if (Pool.Count > MaxChunks) return;
        if (chunk.Length != ChunkSize) return;
        Pool.Add(chunk);
    }
}