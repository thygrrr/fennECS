using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace fennecs;

public readonly record struct KeyExpression
{
    internal readonly ulong Raw;

    /// <summary>
    /// Plain, Non-Relation Component
    /// </summary>
    public KeyExpression(Primary primary)
    {
        Raw = primary.Raw;
    }
    
    public KeyExpression(Primary primary, Secondary secondary)
    {
        Raw = primary.Raw | secondary.raw;
    }

    public KeyExpression(Primary primary, Entity entity)
    {
        Raw = primary.Raw | entity.living;
    }
}

public struct Range<T>
{
    public T Min;
    public T Max;
}

/// <summary>
/// Primary Key of a TypeExpression used to identify a specific type of component in an Archetype Signature and Storage.
/// </summary>
/// <remarks>
/// Lives in the top 16 bits of a TypeIdentity. 12 of these are the associated LanguageType.
/// </remarks>
public readonly record struct Primary
{
    internal readonly ulong Raw;

    public static Primary Void<T>() => new((ulong) Kind.Void | (ulong) LanguageType<T>.Id << 48);
    public static Primary Data<T>() => new((ulong) Kind.Data | (ulong) LanguageType<T>.Id << 48);
    public static Primary Same<T>() => new((ulong) Kind.Same | (ulong) LanguageType<T>.Id << 48);

    
    public Primary(ulong raw)
    {
        Debug.Assert((raw & TypeIdentity.HeaderMask) != 0, "MainType must have a header.");
        Debug.Assert((raw & TypeIdentity.SubMask) == 0, "MainType must not have Subtype bits set.");
        Raw = raw & TypeIdentity.HeaderMask;
    }
}



/// <summary>
/// Part of a TypeExpression used to identify a specific type of Secondary Key target in a TypeExpression.
/// </summary>
public readonly record struct SubType
{
    internal readonly ulong Raw;
    
    public SubType(ulong raw)
    {
        Debug.Assert((raw & TypeIdentity.HeaderMask) == 0, "SubType must not have a header.");
        Debug.Assert((raw & TypeIdentity.SubMask) != 0, "SubType must have Subtype bits set.");
        Raw = raw & TypeIdentity.SubMask;
    }
}


internal static class LTypeHelper
{
    public static ulong Sub<T>() => (ulong)LanguageType<T>.Id << 32;
    public static Type SubResolve(ulong type) => LanguageType.Resolve((TypeID)((type & TypeIdentity.SubMask) >> 32));
}

/// <summary>
/// A TypeExpression used to identify a specific type of component in an Archetype, or Storage, or a Wildcard for querying.
/// </summary>
/// <param name="raw">raw 64 bit value backing this struct</param>
internal readonly record struct TypeIdentity(ulong raw)
{
    public static implicit operator TypeIdentity(ulong raw)
    {
        Debug.Assert((raw & HeaderMask) != 0, "TypeIdentity must have a header.");
        return new(raw);   
    }

    private Kind kind => (Kind)(raw & (ulong) Kind.Mask);
    private Type type => LanguageType.Resolve((TypeID)((raw & TypeMask) >> 48));

    
    /// <summary>
    /// Primary Key of this TypeIdentity.
    /// </summary>
    internal ulong Primary => raw & HeaderMask;

    /// <summary>
    /// Seconday Key of this TypeIdentity.
    /// </summary>
    internal ulong Secondary => raw & KeyMask;

    /// <summary>
    /// Seconday Key Type of this TypeIdentity.
    /// </summary>
    private Key KeyType => (Key)(raw & KeyTypeMask);

    
    /// <summary>
    /// Relation target of this TypeIdentity.
    /// </summary>
    /// <remarks>
    /// Only valid for Relation Components. Used for Relation Cleanup.
    /// </remarks> 
    public Entity relation
    {
        get
        {
            Debug.Assert(KeyType == Key.Entity, $"This TypeIdentity is not a Relation, it's pointing to a {KeyType}");
            return new(raw & KeyMask);
        }
    }

    
    /// <summary>
    /// SubType of this TypeIdentity.
    /// </summary>
    /// <example>
    /// For Object Links, this is the type of the Linked object. For Keyed Components, this is the type of the Key.
    /// </example>
    public Type sub
    {
        get
        {
            Debug.Assert(KeyType is Key.Hash or Key.Object or Key.Entity or Key.Target, $"This TypeIdentity has no SubType, it's pointing to {KeyType}");
            return KeyType == Key.Entity ? typeof(Entity) : LTypeHelper.SubResolve(raw);
        }
    }
    
    


    
    internal const ulong StorageMask      = 0xF000_0000_0000_0000ul;
    internal const ulong TypeMask         = 0x0FFF_0000_0000_0000ul;
    
    internal const ulong KeyMask          = 0x0000_FFFF_FFFF_FFFFul;
    internal const ulong KeyTypeMask      = 0x0000_F000_0000_0000ul;
    
    internal const ulong EntityFlagMask   = 0x0000_0F00_0000_0000ul;
    internal const ulong WorldMask        = 0x0000_00FF_0000_0000ul;

    // For Typed objects (Object Links, Keyed Components)
    internal const ulong SubMask          = 0x0000_0FFF_0000_0000ul;

    // Header is Generation in concrete entities, but in Types, it is not needed (as no type may reference a dead entity...? but it might, if stored by user...!)
    internal const ulong HeaderMask = 0xFFFF_0000_0000_0000ul;
    internal const ulong GenerationMask = 0xFFFF_0000_0000_0000ul;

    
    private Id id => new(raw & KeyMask);

    /// <inheritdoc />
    public override string ToString()
    {
        if (raw == default) return $"None";
        return id == default ? $"{kind}<{type}>" : $"{kind}<{type}>\u2192{id}";  
    } 
}

public enum Kind : ulong
{
    None = 0x0000_0000_0000_0000ul, // No Type
    Void = 0x1000_0000_0000_0000ul, // Future: Comp<T>.Tag, Comp<T>.Tag<K> - Tags and other 0-size components, saving storage and migration costs.
    Data = 0x2000_0000_0000_0000ul, // Data Components (Comp<T>.Plain and Comp<T>.Keyed<K> and Comp<T>.Link(T))
    Same = 0x3000_0000_0000_0000ul, // Singleton Components (Comp<T>.Unique / future Comp<T>.Link(T))

    Spatial1D = 0x4000_0000_0000_0000ul, // Future: 1D Spatial Components
    Spatial2D = 0x5000_0000_0000_0000ul, // Future: 2D Spatial Components
    Spatial3D = 0x6000_0000_0000_0000ul, // Future: 3D Spatial Components
    
    Any           = 0xF000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.

    Mask          = TypeIdentity.StorageMask, // Internal Use
    Share
}

[Flags]
internal enum Key : ulong
{
    None   = 0,
    Data   = 0x0000_1000_0000_0000ul,
    Entity = 0x0000_2000_0000_0000ul,
    Object = 0x0000_4000_0000_0000ul,
    Hash    = 0x0000_8000_0000_0000ul,

    Target = Entity | Object | Hash,
    Any = Data | Entity | Object | Hash,
    
    Mask =  0x0000_F000_0000_0000ul,
}

[Flags]
internal enum EntityFlags : ulong
{
    None     = 0x0000_0000_0000_0000ul,
    Disabled = 0x0000_0100_0000_0000ul,
    Mask     = TypeIdentity.EntityFlagMask,
}


[StructLayout(LayoutKind.Explicit)]
public readonly record struct ObjectLink
{
    [FieldOffset(0)]
    internal readonly ulong Raw;

    [FieldOffset(0)]
    public int hash;
    
    public ObjectLink(ulong raw)
    {
        Debug.Assert((Raw & TypeIdentity.HeaderMask) == 0, "ObjectLink must not have a header.");
        Debug.Assert((Raw & TypeIdentity.KeyTypeMask) == (ulong) Key.Object, "ObjectLink must have a Category.Object");
        Raw = raw;
    }
    
    public Type type => LTypeHelper.SubResolve(Raw);

    /// <inheritdoc />
    public override string ToString() => $"O-<{type}>-{hash:x8}";
    
    public override int GetHashCode() => hash;
    
    internal Id id => new(Raw);
    
    internal static Id Of<T>(T obj) where T : class => new((ulong) Key.Object | LTypeHelper.Sub<T>() | (uint) obj.GetHashCode());
}

[StructLayout(LayoutKind.Explicit)]
public record struct Entity : IComparable<Entity>
{
    internal Id id => new(raw);

    public Type type => typeof(Entity);
    
    [FieldOffset(0)]
    public ulong raw;

    [FieldOffset(0)]
    internal int Index;

    [FieldOffset(4)]
    private byte _world;

    [FieldOffset(6)]
    internal ushort Generation;
    
    internal EntityFlags Flags => (EntityFlags) (raw & (ulong) EntityFlags.Mask);
    
    internal Key Key => (Key) (raw & (ulong) Key.Mask);

    internal Entity Successor
    {
        get
        {
            Debug.Assert(Key == Key.Entity, $"{this} is not an Entity, it's a {Key}.");
            return this with { Generation = (ushort)(Generation + 1) };
        }
    }
    
    internal World World => fennecs.World.All[_world];
    internal ref Meta Meta => ref fennecs.World.All[_world].GetEntityMeta(this);

    internal ulong living
    {
        get
        {
            Debug.Assert(Alive, $"Entity {this} is not alive.");
            return raw & TypeIdentity.KeyMask;
        }
    }

    public Entity(byte world, int index) : this((ulong)Key.Entity | (ulong)world << 32 | (uint)index) { }

    public Entity(ulong raw)
    {
        this.raw = raw;
        Debug.Assert((raw & TypeIdentity.KeyTypeMask) == (ulong) Key.Entity, "Identity is not of Category.Entity.");
        Debug.Assert(World.TryGet(_world, out var world), $"World {_world} does not exist.");
        Debug.Assert(Alive, "Entity is not alive.");
    }

    /// <summary>
    /// True if the Entity is alive in its world (and has a world).
    /// </summary>
    public static implicit operator bool(Entity self) => self.Alive;
    

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
    public bool Equals(Entity other) => raw == other.raw;
    

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


[StructLayout(LayoutKind.Explicit)]
public readonly record struct Secondary
{
    [FieldOffset(0)]
    public readonly ulong raw;

    [FieldOffset(0)]
    private readonly int hash;
    
    internal Secondary(ulong value)
    {
        Debug.Assert((value & TypeIdentity.HeaderMask) == 0, "KeyExpression must not have a header.");
        Debug.Assert((value & TypeIdentity.KeyTypeMask) == (ulong) Key.Hash, "KeyExpression is not of Category.Key.");
        raw = value;
    }

    public Secondary Of<K>(K key) where K : notnull => new((ulong) Key.Hash | LTypeHelper.Sub<K>() | (uint) key.GetHashCode());

    private Type type => LTypeHelper.SubResolve(raw);
    
    internal Id id => new(raw);
    
    /// <inheritdoc />
    public override string ToString() => $"H<{type}>-{hash:x8}";
}



internal readonly record struct Relate
{
    public readonly ulong raw;
    
    internal Relate(ulong value)
    {
        Debug.Assert((value & TypeIdentity.HeaderMask) == 0, "RelationExpression must not have a header.");
        Debug.Assert((value & TypeIdentity.KeyTypeMask) == (ulong) Key.Entity, "RelationExpression is not of Category.Entity.");
        Debug.Assert(new Entity(value).Alive, "Relation target is not alive.");
        raw = value;
    }

    public static Relate To(Entity entity) => new(entity.living);
    internal Id id => new(raw);
    
    internal Entity target => new(raw);
}


internal readonly record struct Id : IComparable<Id>
{
    /// <summary>
    /// Creates a new Id from a ulong value.
    /// </summary>
    /// <param name="Value">value, must not have any of the 16 most significant bits set <see cref="TypeIdentity.KeyMask"/>.</param>
    public Id(ulong Value)
    {
        Debug.Assert((Value & TypeIdentity.HeaderMask) == 0, "fennecs.Id must not have a header.");
        _value = Value & TypeIdentity.KeyMask;
    }

    private Key Key => (Key) (_value & (ulong) Key.Mask);

    private readonly ulong _value;

    /// <inheritdoc />
    public override string ToString()
    {
        return Key switch
        {
            Key.None => $"None",
            Key.Entity => new Entity(_value).ToString(),
            Key.Object => new ObjectLink(_value).ToString(),
            Key.Hash => new Secondary(_value).ToString(),
            _ => $"?-{_value:x16}", 
        };
    }
    
    /// <inheritdoc />
    public int CompareTo(Id other) => _value.CompareTo(other._value);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();
}

