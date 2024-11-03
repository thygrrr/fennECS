namespace fennecs;

/// <summary>
/// A storage that contains a single element.
/// </summary>
public class Mono<T>(T value) : IRefIndexable<T>
{
    private T _value = value;

    /// <summary>
    /// The value stored in this Mono. Same for any index.
    /// </summary>
    /// <inheritdoc />
    public ref T this[int _] => ref _value;
}
