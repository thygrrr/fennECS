using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace fennecs;

public class Tree : Dictionary<Entity, Entity>;

internal static class LTypeHelper
{
    public static ulong Id<T>() => (ulong)LanguageType<T>.Id << 48;

    public static ulong Sub<T>() => (ulong)LanguageType<T>.Id << 32;

    public static Type Resolve(ulong type) => LanguageType.Resolve((TypeID)((type & TypeIdentity.TypeMask) >> 48));

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

        World w = new World();
        var q = w.Query().Compile();

        q.Stream<Action, int>();
        
        Debug.Assert((raw & HeaderMask) != 0, "TypeIdentity must have a header.");
        return new(raw);   
    }
    
    public Kind kind => (Kind)(raw & (ulong) Kind.Mask);
    public Type type => LanguageType.Resolve((TypeID)((raw & TypeMask) >> 48));

    public Key Key => (Key)(raw & KeyMask);
    
    
    /// <summary>
    /// Relation target of this TypeIdentity.
    /// </summary>
    /// <remarks>
    /// Only valid for Relation Components. Used for Relation Cleanup.
    /// </remarks> 
    public LiveEntity relation
    {
        get
        {
            Debug.Assert(Key == Key.Entity, $"This TypeIdentity is not a Relation, it's pointing to a {Key}");
            return new(raw & TargetMask);
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
            Debug.Assert(Key is Key.Hash or Key.Object or Key.Entity or Key.Target, $"This TypeIdentity has no SubType, it's pointing to {Key}");
            return Key == Key.Entity ? typeof(Entity2) : LTypeHelper.SubResolve(raw);
        }
    }
    
    


    
    internal const ulong StorageMask      = 0xF000_0000_0000_0000ul;
    internal const ulong TypeMask         = 0x0FFF_0000_0000_0000ul;
    
    internal const ulong TargetMask       = 0x0000_FFFF_FFFF_FFFFul;
    internal const ulong KeyMask          = 0x0000_F000_0000_0000ul;
    
    internal const ulong EntityFlagMask   = 0x0000_0F00_0000_0000ul;
    internal const ulong WorldMask        = 0x0000_00FF_0000_0000ul;

    // For Typed objects (Object Links, Keyed Components)
    internal const ulong SubMask          = 0x0000_0FFF_0000_0000ul;

    // Header is Generation in concrete entities, but in Types, it is not needed (as no type may reference a dead entity...? but it might, if stored by user...!)
    internal const ulong HeaderMask = 0xFFFF_0000_0000_0000ul;
    internal const ulong GenerationMask = 0xFFFF_0000_0000_0000ul;

    
    private Id id => new(raw & TargetMask);

    /// <inheritdoc />
    public override string ToString()
    {
        if (raw == default) return $"None";
        return id == default ? $"{kind}<{type}>" : $"{kind}<{type}>\u2192{id}";  
    } 
}

public enum Kind : ulong
{
    None      = 0x0000_0000_0000_0000ul, // No Type
    Void      = 0x1000_0000_0000_0000ul, // Future: Comp<T>.Tag, Comp<T>.Tag<K> - Tags and other 0-size components, saving storage and migration costs.
    Data      = 0x2000_0000_0000_0000ul, // Data Components (Comp<T>.Plain and Comp<T>.Keyed<K> and Comp<T>.Link(T))
    Unique    = 0x3000_0000_0000_0000ul, // Singleton Components (Comp<T>.Unique / future Comp<T>.Link(T))

    //Spatial1D = 0x4000_0000_0000_0000ul, // Future: 1D Spatial Components
    //Spatial2D = 0x5000_0000_0000_0000ul, // Future: 2D Spatial Components
    //Spatial3D = 0x6000_0000_0000_0000ul, // Future: 3D Spatial Components
    
    WildVoid      = 0x8000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.
    WildData      = 0x9000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.
    WildUnique    = 0xA000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.

    //WildSpatial1D = 0xC000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.
    //WildSpatial2D = 0xD000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.
    //WildSpatial3D = 0xE000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.

    Any           = 0xF000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.

    Mask          = TypeIdentity.StorageMask, // Internal Use
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
public record struct LiveEntity(ulong raw) : IAddRemoveComponent<Entity>
{
    [FieldOffset(0)]
    public ulong raw = raw & TypeIdentity.TargetMask | (ulong)Key.Entity;

    [FieldOffset(0)]
    internal int index;

    [FieldOffset(4)]
    internal byte world;

    internal EntityFlags flags => (EntityFlags) (raw & (ulong) EntityFlags.Mask);
    
    internal World World => World.All[world];

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Entity{world}-{index:x8}/*";
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(index, world);

    #region CRUD

    public Entity Add<C>() where C : notnull, new() => new Entity(World, new(raw)).Add<C>();

    public Entity Add<C>(C value) where C : notnull => new Entity(World, new(raw)).Add(value);

    public Entity Add<T>(Entity target) where T : notnull, new() => new Entity(World, new(raw)).Add(target);
    
    public Entity Add<R>(R value, Entity relation) where R : notnull => new Entity(World, new(raw)).Add(value, relation);

    public Entity Add<L>(Link<L> link) where L : class => new Entity(World, new(raw)).Add(link);
    
    public Entity Remove<C>() where C : notnull => new Entity(World, new(raw)).Remove<C>();

    public Entity Remove<R>(Entity relation) where R : notnull  => new Entity(World, new(raw)).Remove<R>(relation);

    public Entity Remove<L>(L linkedObject) where L : class => new Entity(World, new(raw)).Remove<L>(linkedObject);

    public Entity Remove<L>(Link<L> link) where L : class => new Entity(World, new(raw)).Remove<L>(link);

    
    public void Despawn() => new Entity(World, new(raw)).Despawn();

    #endregion
    
}


[StructLayout(LayoutKind.Explicit)]
public record struct ObjectLink
{
    [FieldOffset(0)]
    public ulong raw;

    [FieldOffset(0)]
    public int hash;
    
    public ObjectLink(ulong Raw)
    {
        Debug.Assert((Raw & TypeIdentity.HeaderMask) == 0, "ObjectLink must not have a header.");
        Debug.Assert((Raw & TypeIdentity.KeyMask) == (ulong) Key.Object, "ObjectLink must have a Category.Object");
        raw = Raw;
    }
    
    public Type type => LTypeHelper.SubResolve(raw);

    /// <inheritdoc />
    public override string ToString() => $"O-<{type}>-{hash:x8}";
    
    public override int GetHashCode() => hash;
    
    internal Id id => new(raw);
    
    internal static Id Of<T>(T obj) where T : class => new((ulong) Key.Object | LTypeHelper.Sub<T>() | (uint) obj.GetHashCode());
}


[StructLayout(LayoutKind.Explicit)]
public record struct Entity2 : IComparable<Entity2>
{
    internal Id id => new(raw);

    public Type type => typeof(Entity2);
    
    internal static Entity2 Entity => new((ulong) Key.Entity);
    
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

    internal Entity2 Successor
    {
        get
        {
            Debug.Assert(Key == Key.Entity, $"{this} is not an Entity, it's a {Key}.");
            return this with { Generation = (ushort)(Generation + 1) };
        }
    }
    
    internal ref Meta Meta => ref fennecs.World.All[_world].GetEntityMeta(this);

    internal ulong living
    {
        get
        {
            Debug.Assert(Alive, $"Entity {this} is not alive.");
            return raw & TypeIdentity.TargetMask;
        }
    }

    public Entity2(byte world, int index) : this((ulong)Key.Entity | (ulong)world << 32 | (uint)index) { }

    public Entity2(ulong raw)
    {
        this.raw = raw;
        Debug.Assert((raw & TypeIdentity.KeyMask) == (ulong) Key.Entity, "Identity is not of Category.Entity.");
        Debug.Assert(World.TryGet(_world, out var world), $"World {_world} does not exist.");
        Debug.Assert(Alive, "Entity is not alive.");
    }

    public bool Alive => World.TryGet(_world, out var world) && world.IsAlive(this);

    /// <inheritdoc />
    public override string ToString()
    {
        return $"E-{_world}-{Index:x8}/{Generation:x4}";
    }
    
    /// <inheritdoc />
    public int CompareTo(Entity2 other) => raw.CompareTo(other.raw);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Index, _world);
}


[StructLayout(LayoutKind.Explicit)]
public readonly record struct Hash
{
    [FieldOffset(0)]
    public readonly ulong raw;

    [FieldOffset(0)]
    private readonly int hash;
    
    internal Hash(ulong value)
    {
        Debug.Assert((value & TypeIdentity.HeaderMask) == 0, "KeyExpression must not have a header.");
        Debug.Assert((value & TypeIdentity.KeyMask) == (ulong) Key.Hash, "KeyExpression is not of Category.Key.");
        raw = value;
    }

    public Hash Of<K>(K key) where K : notnull => new((ulong) Key.Hash | LTypeHelper.Sub<K>() | (uint) key.GetHashCode());

    private Type type => LTypeHelper.SubResolve(raw);
    
    internal Id id => new(raw);
    
    /// <inheritdoc />
    public override string ToString() => $"H<{type}>-{hash:x8}";
}



internal readonly record struct Relation
{
    public readonly ulong raw;
    
    internal Relation(ulong value)
    {
        Debug.Assert((value & TypeIdentity.HeaderMask) == 0, "RelationExpression must not have a header.");
        Debug.Assert((value & TypeIdentity.KeyMask) == (ulong) Key.Entity, "RelationExpression is not of Category.Entity.");
        Debug.Assert(new Entity2(value).Alive, "Relation target is not alive.");
        raw = value;
    }

    public Relation Of(Entity2 entity) => new(entity.living);
    internal Id id => new(raw);
    
    internal Entity2 target => new(raw);
}


internal readonly record struct Id : IComparable<Id>
{
    /// <summary>
    /// Creates a new Id from a ulong value.
    /// </summary>
    /// <param name="Value">value, must not have any of the 16 most significant bits set <see cref="TypeIdentity.TargetMask"/>.</param>
    public Id(ulong Value)
    {
        Debug.Assert((Value & TypeIdentity.HeaderMask) == 0, "fennecs.Id must not have a header.");
        _value = Value & TypeIdentity.TargetMask;
    }

    private Key Key => (Key) (_value & (ulong) Key.Mask);

    private readonly ulong _value;

    /// <inheritdoc />
    public override string ToString()
    {
        return Key switch
        {
            Key.None => $"None",
            Key.Entity => new LiveEntity(_value).ToString(),
            Key.Object => new ObjectLink(_value).ToString(),
            Key.Hash => new Hash(_value).ToString(),
            _ => $"?-{_value:x16}", 
        };
    }
    
    /// <inheritdoc />
    public int CompareTo(Id other) => _value.CompareTo(other._value);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();
}

