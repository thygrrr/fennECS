using System.Diagnostics;

namespace fennecs;

/// <summary>
/// 
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

    /// <summary>
    /// Plain Component of type <typeparamref name="T"/>.
    /// </summary>
    public static Primary Plain<T>() where T : notnull => new((ulong)SecondaryKind.None | LTypeHelper.Id<T>());

    /// <summary>
    /// Entity Relation backed by Component of type <typeparamref name="T"/>,.
    /// </summary>
    public static Primary Relation<T>(Entity target) where T : notnull => new((ulong)SecondaryKind.Entity | LTypeHelper.Id<T>() | (uint) target.SecondaryKind);
    
    /// <summary>
    /// Object Link of type <typeparamref name="T"/>.
    /// </summary>
    public static Primary Link<T>(T target) where T : class => new((ulong)SecondaryKind.Object | LTypeHelper.Id<T>() | LTypeHelper.Sub<T>() | (uint) target.GetHashCode());



    private PrimaryKind PrimaryKind => (PrimaryKind)(raw & (ulong)PrimaryKind.Mask);
    private Type type => LanguageType.Resolve((TypeID)((raw & TypeMask) >> 48));


    /// <summary>
    /// Primary Key of this TypeIdentity.
    /// </summary>
    internal ulong PrimaryKey => raw & HeaderMask;

    /// <summary>
    /// Seconday Key of this TypeIdentity.
    /// </summary>
    internal ulong SecondaryKey => raw & KeyMask;

    /// <summary>
    /// Seconday Key Type of this TypeIdentity.
    /// </summary>
    private SecondaryKind SecondaryKindType => (SecondaryKind)(raw & KeyTypeMask);


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
            Debug.Assert(SecondaryKindType == SecondaryKind.Entity, $"This TypeIdentity is not a Relation, it's pointing to a {SecondaryKindType}");
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
            Debug.Assert(SecondaryKindType is SecondaryKind.Family or SecondaryKind.Object or SecondaryKind.Entity or SecondaryKind.Target, $"This TypeIdentity has no SubType, it's pointing to {SecondaryKindType}");
            return SecondaryKindType == SecondaryKind.Entity ? typeof(Entity) : LTypeHelper.SubResolve(raw);
        }
    }


    internal const ulong StorageMask = 0xF000_0000_0000_0000ul;
    internal const ulong TypeMask = 0x0FFF_0000_0000_0000ul;

    internal const ulong KeyMask = 0x0000_FFFF_FFFF_FFFFul;
    internal const ulong KeyTypeMask = 0x0000_F000_0000_0000ul;

    internal const ulong EntityFlagMask = 0x0000_0F00_0000_0000ul;
    internal const ulong WorldMask = 0x0000_00FF_0000_0000ul;

    // For Typed objects (Object Links, Keyed Components)
    internal const ulong SubMask = 0x0000_0FFF_0000_0000ul;

    // Header is Generation in concrete entities, but in Types, it is not needed (as no type may reference a dead entity...? but it might, if stored by user...!)
    internal const ulong HeaderMask = 0xFFFF_0000_0000_0000ul;
    internal const ulong GenerationMask = 0xFFFF_0000_0000_0000ul;


    private Primary Primary => new(raw & KeyMask);

    /// <inheritdoc />
    public override string ToString()
    {
        if (raw == default) return $"None";
        return Primary == default ? $"{PrimaryKind}<{type}>" : $"{PrimaryKind}<{type}>\u2192{Primary}";
    }
}


internal static class LTypeHelper
{
    public static ulong Id<T>() => (ulong)LanguageType<T>.Id << 48;
    public static ulong Sub<T>() => (ulong)LanguageType<T>.Id << 32;

    public static Type Resolve(ulong type) => LanguageType.Resolve((TypeID)((type & TypeIdentity.TypeMask) >> 48));
    public static Type SubResolve(ulong type) => LanguageType.Resolve((TypeID)((type & TypeIdentity.SubMask) >> 32));
}

