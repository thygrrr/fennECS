namespace fennecs;

/// <summary>
/// An object that can be indexed and returns references to its elements.
/// </summary>
public interface IRefIndexable<T>
{
    /// <summary>
    /// ref to the value at the given index.
    /// </summary>
    /// <remarks>
    /// Treat references as tightly bound to the current scope as possible.
    /// </remarks>
    ref T this[int index] { get; }
}