using System.Diagnostics;
using System.Runtime.InteropServices;

namespace fennecs;

public enum PrimaryKind : ulong
{
    None = 0x0000_0000_0000_0000ul,   // No Type
    Void = 0x1000_0000_0000_0000ul,   // Future: Comp<T>.Tag, Comp<T>.Tag<K> - Tags and other 0-size components, saving storage and migration costs.
    Data = 0x2000_0000_0000_0000ul,   // Data Components (Comp<T>.Plain and Comp<T>.Keyed<K> and Comp<T>.Link(T))
    Unique = 0x3000_0000_0000_0000ul, // Singleton Components (Comp<T>.Unique / future Comp<T>.Link(T))

    //Spatial1D = 0x4000_0000_0000_0000ul, // Future: 1D Spatial Components
    //Spatial2D = 0x5000_0000_0000_0000ul, // Future: 2D Spatial Components
    //Spatial3D = 0x6000_0000_0000_0000ul, // Future: 3D Spatial Components

    WildVoid = 0x8000_0000_0000_0000ul,   // Wildcard, details in bottom 32 bits.
    WildData = 0x9000_0000_0000_0000ul,   // Wildcard, details in bottom 32 bits.
    WildUnique = 0xA000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.

    //WildSpatial1D = 0xC000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.
    //WildSpatial2D = 0xD000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.
    //WildSpatial3D = 0xE000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.

    Any = 0xF000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.

    Mask = TypeIdentity.StorageMask, // Internal Use
}

[Flags]
internal enum SecondaryKind : ulong
{
    None = 0,
    Data = 0x0000_1000_0000_0000ul,
    Entity = 0x0000_2000_0000_0000ul,
    Object = 0x0000_4000_0000_0000ul,
    Hash = 0x0000_8000_0000_0000ul,

    Target = Entity | Object | Hash,
    Any = Data | Entity | Object | Hash,

    Mask = 0x0000_F000_0000_0000ul,
}


[StructLayout(LayoutKind.Explicit)]
public record struct ObjectLink
{
    [FieldOffset(0)]
    public ulong raw;

    [FieldOffset(0)]
    public int hash;

    public ObjectLink(ulong raw)
    {
        Debug.Assert((raw & TypeIdentity.HeaderMask) == 0, "ObjectLink must not have a header.");
        Debug.Assert((raw & TypeIdentity.KeyTypeMask) == (ulong)SecondaryKind.Object, "ObjectLink must have a Category.Object");
        this.raw = raw;
    }

    public Type type => LTypeHelper.SubResolve(raw);

    /// <inheritdoc />
    public override string ToString() => $"O-<{type}>-{hash:x8}";

    public override int GetHashCode() => hash;

    internal Primary Primary => new(raw);

    internal static Primary Of<T>(T obj) where T : class => new((ulong)SecondaryKind.Object | LTypeHelper.Sub<T>() | (uint)obj.GetHashCode());
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
        Debug.Assert((value & TypeIdentity.KeyTypeMask) == (ulong)SecondaryKind.Hash, "KeyExpression is not of Category.Key.");
        raw = value;
    }

    public Hash Of<K>(K key) where K : notnull => new((ulong)SecondaryKind.Hash | LTypeHelper.Sub<K>() | (uint)key.GetHashCode());

    private Type type => LTypeHelper.SubResolve(raw);

    internal Primary Primary => new(raw);

    /// <inheritdoc />
    public override string ToString() => $"H<{type}>-{hash:x8}";
}

internal readonly record struct Relate
{
    public readonly ulong raw;

    internal Relate(ulong value)
    {
        Debug.Assert((value & TypeIdentity.HeaderMask) == 0, "RelationExpression must not have a header.");
        Debug.Assert((value & TypeIdentity.KeyTypeMask) == (ulong)SecondaryKind.Entity, "RelationExpression is not of Category.Entity.");
        Debug.Assert(new Entity(value).Alive, "Relation target is not alive.");
        raw = value;
    }

    public static Relate To(Entity entity) => new(entity.living);

    internal Primary Primary => new(raw);

    internal Entity target => new(raw);
}

internal readonly record struct Primary : IComparable<Primary>
{
    /// <summary>
    /// Creates a new Id from a ulong value.
    /// </summary>
    /// <param name="value">value, must not have any of the 16 most significant bits set <see cref="TypeIdentity.KeyMask"/>.</param>
    public Primary(ulong value)
    {
        Debug.Assert((value & TypeIdentity.HeaderMask) == 0, "fennecs.Id must not have a header.");
        _value = value & TypeIdentity.KeyMask;
    }
    
    private SecondaryKind SecondaryKind => (SecondaryKind)(_value & (ulong)SecondaryKind.Mask);

    private readonly ulong _value;

    /// <inheritdoc />
    public override string ToString()
    {
        return SecondaryKind switch
        {
            SecondaryKind.None => $"None",
            SecondaryKind.Entity => new Entity(_value).ToString(),
            SecondaryKind.Object => new ObjectLink(_value).ToString(),
            SecondaryKind.Hash => new Hash(_value).ToString(),
            _ => $"?-{_value:x16}",
        };
    }

    /// <inheritdoc />
    public int CompareTo(Primary other) => _value.CompareTo(other._value);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();
}
