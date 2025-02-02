﻿using System.Collections.Immutable;
using fennecs.CRUD;

namespace fennecs;

/// <summary>
/// Base type for Streams. A Stream is a fast, filterable Zip-View iterating a Query's Entities along with some of their Components.
/// </summary>
/// <remarks>
/// Typical Concrete Streams are: 
/// <ul>
/// <li><see cref="Stream{C0}"/></li>
/// <li><see cref="Stream{C0, C1}"/></li>
/// <li><see cref="Stream{C0, C1, C2}"/></li>
/// <li><see cref="Stream{C0, C1, C2, C3}"/></li>
/// <li><see cref="Stream{C0, C1, C2, C3, C4}"/></li>
/// </ul>
/// </remarks>
public record Stream(Query Query) : IBatchBegin
{
    private protected ImmutableArray<MatchExpression> StreamTypes = [];

    /// <summary>
    /// Archetypes, or Archetypes that match the Stream's Subset and Exclude filters.
    /// </summary>
    protected HashSet<Archetype> Filtered => 0 == Subset.Count && 0 == Exclude.Count
        ? Query.Archetypes
        : [..Query.Archetypes.Where(a => (0 == Subset.Count || a.Signature.Matches(Subset)) && !a.Signature.Matches(Exclude))];


    #region Concurrency

    /// <summary>
    /// Countdown signal for this stream when running Jobs.
    /// </summary>
    protected CountdownEvent Countdown = null!;

    /// <summary>
    /// Processor count to use for this thread
    /// </summary>
    protected int Concurrency = Math.Max(2, Environment.ProcessorCount - 2);

    #endregion

    #region Batch Operations
    
    /// <inheritdoc cref="fennecs.Query.Batch"/>
    public Batch Batch(Batch.AddConflict add = default, Batch.RemoveConflict remove = default) => new(Filtered, World, Query.Mask.Clone(), add, remove);

    /// <inheritdoc cref="fennecs.Query.Batch"/>
    public Batch Batch(Batch.RemoveConflict remove) => new(Filtered, World, Query.Mask.Clone(), default, remove);

    #endregion

    /// <summary>
    /// The number of entities that match the underlying Query (filtered by the Stream's Filters).
    /// </summary>
    public int Count => Filtered.Sum(
        f => f.Count
        );


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
    public HashSet<Comp> Subset { get; init; } = [];

    /// <summary>
    /// Exclude Stream Filter - any entities with these components will be excluded from the Stream. (none if empty)
    /// </summary>
    public HashSet<Comp> Exclude { get; init; } = [];


    #region Query Forwarding

    /// <inheritdoc cref="fennecs.Query.Truncate"/>
    public void Truncate(int targetSize, Query.TruncateMode mode = default)
    {
        Query.Truncate(targetSize, mode);
    }

    /// <inheritdoc cref="fennecs.Query.Despawn"/>
    public void Despawn()
    {
        foreach (var archetype in Filtered) archetype.Truncate(0);
    }

    #endregion
    

    #region Assertions

    /// <summary>
    /// Throws if the query has any Wildcards.
    /// </summary>
    protected void AssertNoWildcards()
    {
        if (StreamTypes.Any(expression => expression.IsWildcard)) throw new InvalidOperationException($"Cannot run a this operation on wildcard Stream Types (write destination Aliasing). {StreamTypes}");
    }

    #endregion


    #region IEnumerator (Generic)

    /// <summary>
    /// Returns an enumerator that iterates through the stream, with arbitrary component types.
    /// </summary>
    /// <remarks>
    /// The iterator will be empty if the component types are not present in the stream.
    /// </remarks>
    public IEnumerator<(Entity, E0)> GetEnumerator<E0>() where E0 : notnull
    {
        foreach (var table in Filtered)
        {
            using var join = table.CrossJoin<E0>(StreamTypes.AsSpan());
            if (join.Empty) continue;
            var snapshot = table.Version;
            do
            {
                var s0 = join.Select;
                for (var index = 0; index < table.Count; index++)
                {
                    yield return (table[index], s0[index]);
                    if (table.Version != snapshot)
                        throw new InvalidOperationException("Collection was modified during iteration.");
                }
            } while (join.Iterate());
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the stream, with arbitrary component types.
    /// </summary>
    /// <remarks>
    /// The iterator will be empty if the component types are not present in the stream.
    /// </remarks>
    public IEnumerator<(Entity, E0, E1)> GetEnumerator<E0, E1>() where E0 : notnull where E1 : notnull
    {
        foreach (var table in Filtered)
        {
            using var join = table.CrossJoin<E0, E1>(StreamTypes.AsSpan());
            if (join.Empty) continue;
            var snapshot = table.Version;
            do
            {
                var (s0, s1) = join.Select;
                for (var index = 0; index < table.Count; index++)
                {
                    yield return (table[index], s0[index], s1[index]);
                    if (table.Version != snapshot)
                        throw new InvalidOperationException("Collection was modified during iteration.");
                }
            } while (join.Iterate());
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the stream, with arbitrary component types.
    /// </summary>
    /// <remarks>
    /// The iterator will be empty if the component types are not present in the stream.
    /// </remarks>
    public IEnumerator<(Entity, E0, E1, E2)> GetEnumerator<E0, E1, E2>() where E0 : notnull where E1 : notnull where E2 : notnull
    {
        foreach (var table in Filtered)
        {
            using var join = table.CrossJoin<E0, E1, E2>(StreamTypes.AsSpan());
            if (join.Empty) continue;
            var snapshot = table.Version;
            do
            {
                var (s0, s1, s3) = join.Select;
                for (var index = 0; index < table.Count; index++)
                {
                    yield return (table[index], s0[index], s1[index], s3[index]);
                    if (table.Version != snapshot)
                        throw new InvalidOperationException("Collection was modified during iteration.");
                }
            } while (join.Iterate());
        }
    }

    #endregion


}
