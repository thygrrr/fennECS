using System.Diagnostics;
using System.Runtime.InteropServices;

namespace fennecs;

[StructLayout(LayoutKind.Explicit)]
public record struct Entity : IComparable<Entity>
{
    internal Primary Primary => new(raw);

    [FieldOffset(0)]
    internal ulong raw;

    [FieldOffset(0)]
    internal int Index;

    [FieldOffset(4)]
    private byte _world;

    [FieldOffset(6)]
    internal ushort Generation;

    internal EntityFlags Flags => (EntityFlags)(raw & (ulong)EntityFlags.Mask);

    internal Entity Successor
    {
        get
        {
            Debug.Assert(SecondaryKind == SecondaryKind.Entity, $"{this} is not an Entity, it's a {SecondaryKind}.");
            return this with { Generation = (ushort)(Generation + 1) };
        }
    }

    internal World World => World.All[_world];
    internal ref Meta Meta => ref World.All[_world].GetEntityMeta(this);

    internal ulong living
    {
        get
        {
            Debug.Assert(Alive, $"Entity {this} is not alive.");
            return raw & TypeIdentity.KeyMask;
        }
    }

    internal Entity(byte world, int index) : this((ulong)SecondaryKind.Entity | (ulong)world << 32 | (uint)index) { }

    internal Entity(ulong raw)
    {
        this.raw = raw;
        Debug.Assert((raw & TypeIdentity.KeyTypeMask) == (ulong)SecondaryKind.Entity, "Identity is not of Category.Entity.");
        Debug.Assert(World.TryGet(_world, out var world), $"World {_world} does not exist.");
        Debug.Assert(world.IsAlive(this), "Entity is not alive.");
    }

    
    /// <summary>
    /// Match Expression matching relations targeting this Entity.
    /// </summary>
    public static implicit operator Match(Entity entity) => new(entity.raw & (ulong) SecondaryKind.Mask);

    
    /// <summary>
    /// True if the Entity is alive in its world (and has a world).
    /// </summary>
    public static implicit operator bool(Entity self) => self.Alive;
    
    
    /// <summary>
    /// Wildcard Match Expression matching relations targeting any Entity.
    /// </summary>
    public static Match Any => new(SecondaryKind.Any);
    
    
    /// <inheritdoc />
    public override string ToString()
    {
        return $"E-{_world}-{Index:x8}/{Generation:x4}";
    }

    /// <inheritdoc />
    public int CompareTo(Entity other) => raw.CompareTo(other.raw);


    #region CRUD

    /// <summary>
    /// Gets a reference to the Component of type <typeparamref name="C"/> for the EntityOld.
    /// </summary>
    /// <remarks>
    /// Adds the component before if possible.
    /// </remarks>
    /// <param name="match">specific (targeted) Match Expression for the component type. No wildcards!</param>
    /// <typeparam name="C">any Component type</typeparam>
    /// <returns>ref C, reference to the Component</returns>
    /// <remarks>The reference may be left dangling if changes to the world are made after acquiring it. Use with caution.</remarks>
    /// <exception cref="ObjectDisposedException">If the EntityOld is not Alive..</exception>
    /// <exception cref="KeyNotFoundException">If no C or C(Target) exists in any of the World's tables for EntityOld.</exception>
    public ref C Ref<C>(Match match) where C : struct => ref World.GetComponent<C>(this, match);


    /// <inheritdoc cref="Ref{C}(fennecs.Match)"/>
    public ref C Ref<C>() => ref World.GetComponent<C>(this, Match.Plain);


    /// <summary>
    /// Gets a reference to the Object Link Target of type <typeparamref name="L"/> for the EntityOld.
    /// </summary>
    /// <param name="link">object link match expressioon</param>
    /// <typeparam name="L">any Component type</typeparam>
    /// <returns>ref C, reference to the Component</returns>
    /// <remarks>The reference may be left dangling if changes to the world are made after acquiring it. Use with caution.</remarks>
    /// <exception cref="ObjectDisposedException">If the EntityOld is not Alive..</exception>
    /// <exception cref="KeyNotFoundException">If no C or C(Target) exists in any of the World's tables for EntityOld.</exception>
    public ref L Ref<L>(Link<L> link) where L : class => ref World.GetComponent<L>(this, link);


    /// <inheritdoc />
    public Entity Add<T>(Entity relation) where T : notnull, new() => Add(new T(), relation);


    /// <inheritdoc cref="Add{R}(R,fennecs.EntityOld)"/>
    public Entity Add<R>(R value, Entity relation) where R : notnull
    {
        World.AddComponent(this, TypeExpression.Of<R>(relation), value);
        return this;
    }

    /// <summary>
    /// Adds a object link to the current EntityOld.
    /// Object links, in addition to making the object available as a Component,
    /// place all Entities with a link to the same object into a single Archetype,
    /// which can optimize processing them in queries.
    /// </summary>
    /// <remarks>
    /// Beware of Archetype fragmentation! 
    /// You can end up with a large number of Archetypes with few Entities in them,
    /// which negatively impacts processing speed and memory usage.
    /// Try to keep the size of your Archetypes as large as possible for maximum performance.
    /// </remarks>
    /// <typeparam name="T">Any reference type. The type the object to be linked with the EntityOld.</typeparam>
    /// <param name="link">The target of the link.</param>
    /// <returns>EntityOld struct itself, allowing for method chaining.</returns>
    public Entity Add<T>(Link<T> link) where T : class
    {
        World.AddComponent(this, TypeExpression.Of<T>(link), link.Target);
        return this;
    }

    /// <inheritdoc />
    public Entity Add<C>() where C : notnull, new() => Add(new C());

    /// <summary>
    /// Adds a Plain Component of a specific type, with specific data, to the current EntityOld. 
    /// </summary>
    /// <param name="data">The data associated with the relation.</param>
    /// <typeparam name="T">Any value or reference component type.</typeparam>
    /// <returns>EntityOld struct itself, allowing for method chaining.</returns>
    public Entity Add<T>(T data) where T : notnull => Add(data, default);


    /// <summary>
    /// Removes a Component of a specific type from the current EntityOld.
    /// </summary>
    /// <typeparam name="C">The type of the Component to be removed.</typeparam>
    /// <returns>EntityOld struct itself, allowing for method chaining.</returns>
    public Entity Remove<C>() where C : notnull
    {
        World.RemoveComponent(this, TypeExpression.Of<C>(Match.Plain));
        return this;
    }


    /// <summary>
    /// Removes a relation of a specific type between the current EntityOld and the target EntityOld.
    /// </summary>
    /// <param name="relation">target of the relation.</param>
    /// <typeparam name="R">backing type of the relation to be removed.</typeparam>
    /// <returns>EntityOld struct itself, allowing for method chaining.</returns>
    public Entity Remove<R>(Entity relation) where R : notnull
    {
        World.RemoveComponent(this, TypeExpression.Of<R>(relation));
        return this;
    }

    /// <inheritdoc />
    public Entity Remove<L>(L linkedObject) where L : class => Remove(Link<L>.With(linkedObject));


    /// <summary>
    /// Removes the link of a specific type with the target object.
    /// </summary>
    /// <typeparam name="T">The type of the link to be removed.</typeparam>
    /// <param name="link">The target object from which the link will be removed.</param>
    /// <returns>EntityOld struct itself, allowing for method chaining.</returns>
    public Entity Remove<T>(Link<T> link) where T : class
    {
        World.RemoveComponent(this, link.TypeExpression);
        return this;
    }


    /// <summary>
    /// Despawns the EntityOld from the World.
    /// </summary>
    /// <remarks>
    /// The EntityOld builder struct still exists afterwards, but the EntityOld is no longer alive and subsequent CRUD operations will throw.
    /// </remarks>
    public void Despawn() => World.Despawn(this);


    /// <summary>
    /// Checks if the EntityOld has a Plain Component.
    /// Same as calling <see cref="Has{T}()"/> with <see cref="Identity.Plain"/>
    /// </summary>
    public bool Has<T>() where T : notnull => World.HasComponent<T>(this, default);


    /// <inheritdoc />
    public bool Has<R>(Entity relation) where R : notnull => World.HasComponent<R>(this, relation);


    /// <inheritdoc />
    public bool Has<L>(L linkedObject) where L : class => Has(Link<L>.With(linkedObject));


    /// <summary>
    /// Checks if the EntityOld has a Component of a specific type.
    /// Allows for a <see cref="Match"/> Expression to be specified (Wildcards)
    /// </summary>
    public bool Has<T>(Match match) => World.HasComponent<T>(this, match);

    /// <summary>
    /// Checks if the EntityOld has an Object Link of a specific type and specific target.
    /// </summary>
    public bool Has<T>(Link<T> link) where T : class => World.HasComponent<T>(this, link);

    /// <summary>
    /// Boxes all the Components on the EntityOld into an array.
    /// Use sparingly, but don't be paranoid. Suggested uses: serialization and debugging.
    /// </summary>
    /// <remarks>
    /// Values and References are copied, changes to the array will not affect the EntityOld.
    /// Changes to objects in the array will affect these objects in the World.
    /// This array is re-created every time this getter is called.
    /// The values are re-boxed each time this getter is called.
    /// </remarks>
    public IReadOnlyList<Component> Components => World.GetComponents(this);


    /// <summary>
    /// Gets all Components of a specific type and match expression on the EntityOld.
    /// Supports relation Wildcards, for example:<ul>
    /// <li><see cref="EntityOld.Any">EntityOld.Any</see></li>
    /// <li><see cref="Link.Any">Link.Any</see></li>
    /// <li><see cref="Match.Target">Match.Target</see></li>
    /// <li><see cref="Match.Any">Match.Any</see></li>
    /// <li><see cref="Match.Plain">Match.Plain</see></li>
    /// </ul>
    /// </summary>
    /// <remarks>
    /// This is not intended as the main way to get a component from an EntityOld. Consider <see cref="Stream"/>s instead.
    /// </remarks>
    /// <param name="match">match expression, supports wildcards</param>
    /// <typeparam name="T">backing type of the component</typeparam>
    /// <returns>array with all the component values stored for this EntityOld</returns>
    public T[] Get<T>(Match match) => World.Get<T>(this, match);

    #endregion


    #region Cast Operators and IEquatable<EntityOld>

    /// <inheritdoc />
    public bool Equals(Entity other) => raw == other.raw || raw == other.living;


    /// <inheritdoc />
    public override int GetHashCode() => raw.GetHashCode();


    /// <summary>
    /// Is this EntityOld Alive in its World?
    /// </summary>
    public bool Alive => World != null! && World.IsAlive(this);

    /// <summary>
    /// Dumps the Entity to a nice readable string, including its component structure.
    /// </summary>
    public string DebugString()
    {
        var sb = new System.Text.StringBuilder(ToString());
        sb.Append(' ');
        if (Alive)
        {
            sb.AppendJoin("\n  |-", World.GetSignature(this));
        }
        else
        {
            sb.Append("-DEAD-");
        }

        return sb.ToString();
    }

    #endregion
}
[Flags]
internal enum EntityFlags : ulong
{
    None = 0x0000_0000_0000_0000ul,
    Disabled = 0x0000_0100_0000_0000ul,
    Mask = TypeIdentity.EntityFlagMask,
}
