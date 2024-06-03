﻿using System.Collections.Immutable;
using System.Text;
using fennecs.pools;

namespace fennecs;

/// <summary>
/// A fennecs.World contains Entities, their Components, compiled Queries, and manages the lifecycles of these objects.
/// </summary>
public partial class World : IDisposable
{
    #region Entity Spawn, Liveness, and Despawn

    /// <summary>
    /// Creates a new Identity in this World, and returns its Entity builder struct.
    /// Reuses previously despawned Entities, whose Identities will differ in Generation after respawn. 
    /// </summary>
    /// <returns>an Entity to operate on</returns>
    public Entity Spawn() => new(this, NewEntity()); //TODO: Check if semantically legal to spawn in Deferred mode.


    internal PooledList<Identity> SpawnBare(int count)
    {
        var identities = _identityPool.Spawn(count);
        while (_meta.Length <= _identityPool.Created) Array.Resize(ref _meta, _meta.Length * 2);
        return identities;
    }

    /// <summary>
    /// Spawns a number of pre-configured Entities. 
    /// </summary>
    public EntitySpawner Entity() => new(this);


    /// <summary>
    /// Spawns a number of pre-configured Entities 
    /// </summary>
    /// <remarks>
    /// It's more comfortable to spawn via <see cref="EntitySpawner"/>, from <c>world.Entity()</c>
    /// </remarks>
    /// <param name="components">TypeExpressions and boxed objects to spawn</param>
    /// <param name="count"></param>
    /// <param name="values">component values</param>
    internal void Spawn(int count, IReadOnlyList<TypeExpression> components, IReadOnlyList<object> values)
    {
        var signature = new Signature<TypeExpression>(components.ToImmutableSortedSet()).Add(TypeExpression.Of<Identity>(Target.Plain));
        var archetype = GetArchetype(signature);
        archetype.Spawn(count, components, values);
    }

    /// <summary>
    /// Despawn (destroy) an Entity from this World.
    /// </summary>
    /// <param name="entity">the entity to despawn.</param>
    public void Despawn(Entity entity) => DespawnImpl(entity);

    
    /// <summary>
    /// Checks if the entity is alive (was not despawned).
    /// </summary>
    /// <param name="identity">an Entity</param>
    /// <returns>true if the Entity is Alive, false if it was previously Despawned</returns>
    internal bool IsAlive(Identity identity) => identity == _meta[identity.Index].Identity;


    /// <summary>
    /// The number of living entities in the World.
    /// </summary>
    public override int Count => _identityPool.Count;

    /// <summary>
    /// All Queries that exist in this World.
    /// </summary>
    public IReadOnlySet<Query> Queries => _queries;

    #endregion


    #region Bulk Operations

    /// <summary>
    /// Despawn (destroy) all Entities matching a given Type and Match Expression.
    /// </summary>
    /// <typeparam name="T">any component type</typeparam>
    /// <param name="match">default <see cref="Target.Plain"/>.<br/>Can alternatively be one
    /// of <see cref="Target.Any"/>, <see cref="Target.Object"/> or <see cref="Target.AnyTarget"/>
    /// </param>
    public void DespawnAllWith<T>(Target match = default)
    {
        var query = Query<Identity>(Target.Plain).Has<T>(match).Stream();
        query.Raw(delegate(Memory<Identity> entities)
        {
            //TODO: This is not good. Need to untangle the types here.
            foreach (var identity in entities.Span) DespawnImpl(new(this, identity));
        });
    }


    /// <summary>
    /// Bulk Despawn Entities from a World.
    /// </summary>
    /// <param name="toDelete">the entities to despawn (remove)</param>
    internal void Despawn(ReadOnlySpan<Entity> toDelete)
    {
        lock (_spawnLock)
        {
            for (var i = toDelete.Length - 1; i >= 0; i--)
            {
                DespawnImpl(toDelete[i]);
            }
        }
    }

    /// <summary>
    /// Bulk Despawn Entities from a World.
    /// </summary>
    /// <param name="identities">the entities to despawn (remove)</param>
    internal void Recycle(ReadOnlySpan<Identity> identities)
    {
        lock (_spawnLock)
        {
            //TODO: Not good to assemble the Entity like that. Types need to be untangled.
            foreach (var identity in identities) DespawnDependencies(new(this, identity));
            _identityPool.Recycle(identities);
        }
    }

    /// <summary>
    /// Despawn one Entity from a World.
    /// </summary>
    /// <param name="entity">the entity to despawn (remove)</param>
    internal void Recycle(Entity entity)
    {
        lock (_spawnLock)
        {
            DespawnDependencies(entity);
            _identityPool.Recycle(entity);
        }
    }

    #endregion


    #region Lifecycle & Locking

    /// <summary>
    /// Create a new World.
    /// </summary>
    /// <param name="initialCapacity">initial Entity capacity to reserve. The world will grow automatically.</param>
    public World(int initialCapacity = 4096)
    {
        World = this;
       
        
        _identityPool = new(initialCapacity);

        _meta = new Meta[initialCapacity];

        //Create the "Entity" Archetype, which is also the root of the Archetype Graph.
        _root = GetArchetype(new(TypeExpression.Of<Identity>(Target.Plain)));
    }


    /// <summary>
    ///  Runs the World's Garbage Collection (placeholder for future GC - currently removes all empty Archetypes).
    /// </summary>
    public void GC()
    {
        lock (_modeChangeLock)
        {
            if (Mode != WorldMode.Immediate) throw new InvalidOperationException("Cannot run GC while in Deferred mode.");

            foreach (var archetype in Archetypes)
            {
                if (archetype.Count == 0) DisposeArchetype(archetype);
            }

            Archetypes.Clear();
            Archetypes.AddRange(_typeGraph.Values);
        }
    }


    private void DisposeArchetype(Archetype archetype)
    {
        _typeGraph.Remove(archetype.Signature);

        foreach (var type in archetype.Signature)
        {
            _tablesByType[type].Remove(archetype);

            // This is still relevant if ONE relation component is eliminated, but NOT all of them.
            // In the case where the target itself is Despawned, _typesByRelationTarget already
            // had its entire entry for that Target removed.
            if (type.isRelation && _typesByRelationTarget.TryGetValue(type.Relation, out var stillInUse))
            {
                stillInUse.Remove(type);
                if (stillInUse.Count == 0) _typesByRelationTarget.Remove(type.Relation);
            }

            // Same here, if all Archetypes with a Type are gone, we can clear the entry.
            if (_tablesByType[type].Count == 0) _tablesByType.Remove(type);
        }

        foreach (var query in _queries)
        {
            // TODO: Will require some optimization later.
            query.ForgetArchetype(archetype);
        }
    }


    /// <summary>
    /// Disposes of the World. Currently, a no-op.
    /// </summary>
    public new void Dispose()
    {
        //TODO: Dispose all Object Links, Queries, etc.?
    }


    /// <summary>
    /// Locks the World (setting into a Deferred mode) for the scope of the returned WorldLock.
    /// Multiple Locks can be taken out, and all structural Operations on Entities will be queued,
    /// and executed once the last Lock is released. 
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public WorldLock Lock() => new(this);

    #endregion

    #region Debug Tools

    /// <inheritdoc />
    public override string ToString()
    {
        return DebugString();
    }

    /// <inheritdoc cref="ToString"/>
    private string DebugString()
    {
        var sb = new StringBuilder("World:");
        sb.AppendLine();
        sb.AppendLine($" {Archetypes.Count} Archetypes");
        sb.AppendLine($" {Count} Entities");
        sb.AppendLine($" {_queries.Count} Queries");
        sb.AppendLine($"{nameof(WorldMode)}.{Mode}");
        return sb.ToString();
    }

    #endregion
}
