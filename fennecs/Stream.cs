using System.Collections;
using System.Collections.Immutable;
using fennecs.pools;

namespace fennecs;

/// <summary>
/// A Stream is an accessor that allows for iteration over a Query's contents.
/// It exposes both the Runners as well as IEnumerable over a value tuple of the
/// Query's contents.
/// </summary>
/// <typeparam name="C0">component type to stream. if this type is not in the query, the stream will always be length zero.</typeparam>
// ReSharper disable once NotAccessedPositionalProperty.Global
public record Stream(Query Query, Match Match0) : IEnumerable<Entity>, IBatchBegin 
{
    private readonly ImmutableArray<TypeExpression> _streamTypes = [];

    /// <summary>
    /// Archetypes, or Archetypes that match the Stream's Subset and Exclude filters.
    /// </summary>
    protected SortedSet<Archetype> Filtered => Subset.IsEmpty && Exclude.IsEmpty 
        ? Archetypes 
        : new(Archetypes.Where(a => (Subset.IsEmpty || a.Signature.Matches(Subset)) && !a.Signature.Matches(Exclude)));

    /// <summary>
    /// Creates a builder for a Batch Operation on the Stream's underyling Query.
    /// </summary>
    /// <returns>fluent builder</returns>
    public Batch Batch() => Query.Batch();
    
    /// <inheritdoc cref="fennecs.Query.Batch()"/>
    public Batch Batch(Batch.AddConflict add) => Query.Batch(add);
    
    /// <inheritdoc cref="fennecs.Query.Batch()"/>
    public Batch Batch(Batch.RemoveConflict remove) => Query.Batch(remove);

    /// <inheritdoc cref="fennecs.Query.Batch()"/>
    public Batch Batch(Batch.AddConflict add, Batch.RemoveConflict remove) => Query.Batch(add, remove);
    
    
    /// <summary>
    /// The number of entities that match the underlying Query.
    /// </summary>
    public int Count => Filtered.Sum(f => f.Count);


    /// <summary>
    /// The Archetypes that this Stream is iterating over.
    /// </summary>
    protected SortedSet<Archetype> Archetypes => Query.Archetypes;

    /// <summary>
    /// The World this Stream is associated with.
    /// </summary>
    protected World World => Query.World;

    /// <summary>
    /// The Query this Stream is associated with.
    /// Can be re-inited via the with keyword.
    /// </summary>
    public Query Query { get; } = Query;

    /// <summary>
    /// Subset Stream Filter - if not empty, only entities with these components will be included in the Stream. 
    /// </summary>
    public ImmutableSortedSet<Comp> Subset { get; init; } = [];
    
    /// <summary>
    /// Exclude Stream Filter - any entities with these components will be excluded from the Stream. (none if empty)
    /// </summary>
    public ImmutableSortedSet<Comp> Exclude { get; init; } = [];
    
    /// <summary>
    ///     Countdown event for parallel runners.
    /// </summary>
    protected readonly CountdownEvent Countdown = new(initialCount: 1);

    /// <summary>   
    ///     The number of threads this Stream uses for parallel processing.
    /// </summary>
    protected static int Concurrency => Math.Max(1, Environment.ProcessorCount - 2);


    #region Stream.For

    /// <include file='XMLdoc.xml' path='members/member[@name="T:ForE"]'/>
    public void For(EntityAction action)
    {
        using var worldLock = World.Lock();
        foreach (var table in Filtered) foreach (var entity in table) action(entity);
    }


    /// <include file='XMLdoc.xml' path='members/member[@name="T:ForEU"]'/>
    public void For<U>(U uniform, UniformEntityAction<U> action)
    {
        using var worldLock = World.Lock();
        foreach (var table in Filtered) foreach (var entity in table) action(uniform, entity);
    }
    
    #endregion


    #region Query Forwarding
    
    /// <inheritdoc cref="fennecs.Query.Despawn"/>
    public void Despawn()
    {
        foreach (var archetype in Filtered) archetype.Truncate(0);
    }

    #endregion

    #region IEnumerable

    /// <inheritdoc />
    public IEnumerator<Entity> GetEnumerator()
    {
        foreach (var table in Filtered)
        {
            var snapshot = table.Version;
            foreach (var entity in table)
            {
                yield return entity;
                if (table.Version != snapshot) throw new InvalidOperationException("Collection was modified during iteration.");
            }
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}

