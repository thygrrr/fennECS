using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace fennecs;

internal static class LTypeHelper
{
    public static ulong Id<T>() => (ulong)LanguageType<T>.Id << 48;

    public static ulong Sub<T>() => (ulong)LanguageType<T>.Id << 32;

    public static Type Resolve(ulong type) => LanguageType.Resolve((TypeID)((type & TypeIdentity.TypeMask) >> 48));

    public static Type SubResolve(ulong type) => LanguageType.Resolve((TypeID)((type & TypeIdentity.SubMask) >> 32));
}

/// <summary>
/// 
/// </summary>
/// <param name="raw"></param>
public readonly record struct TypeIdentity(ulong raw)
{
    public static implicit operator TypeIdentity(ulong raw) => new(raw);
    
    public Type type => LanguageType.Resolve((TypeID)((raw & TypeMask) >> 48));

    public TargetFlag kind => (TargetFlag)(raw & TargetFlagMask);

    /// <summary>
    /// Relation target of this TypeIdentity.
    /// </summary>
    /// <remarks>
    /// Only valid for Relation Components.
    /// </remarks> 

    public Entity3 relation
    {
        get
        {
            Debug.Assert(kind == TargetFlag.Entity, $"This TypeIdentity is not a Relation, it's pointing to a {kind}");
            return new(raw & TargetMask);
        }
    }

    /// <summary>
    /// ObjectLink of this TypeIdentity.
    /// </summary>
    /// <remarks>
    /// Only valid for Object Links.
    /// </remarks> 
    public ObjectLink link
    {
        get
        {
            Debug.Assert(kind == TargetFlag.Object, $"This TypeIdentity is not a Link, it's pointing to {kind}");
            return new(raw & TargetMask);
        }
    }

    /// <summary>
    /// KeyExpression of this TypeIdentity.
    /// </summary>
    /// <remarks>
    /// Only valid for Keyed components.
    /// </remarks> 
    public KeyExpression key
    {
        get
        {
            Debug.Assert(kind == TargetFlag.Key, $"This TypeIdentity is not Keyed, it's pointing to {kind}");
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
            Debug.Assert(kind is TargetFlag.Key or TargetFlag.Object or TargetFlag.Entity, $"This TypeIdentity has no SubType, it's pointing to {kind}");
            return kind == TargetFlag.Entity ? typeof(Entity3) : LTypeHelper.SubResolve(raw);
        }
    }
    
    
    /// <summary>
    /// Plain Data
    /// </summary>
    public static TypeIdentity Plain<D>() where D : notnull => 
        (ulong)TypeKind.Data 
        | LTypeHelper.Id<D>()
        | (ulong)TargetFlag.None;

    /// <summary>
    /// Plain Data
    /// </summary>
    public static TypeIdentity Data<D>() where D : notnull => 
        (ulong)TypeKind.Data 
        | LTypeHelper.Id<D>()
        | (ulong)TargetFlag.None 
        | (ulong)TargetFlag.None;

    /// <summary>
    /// Keyed Data
    /// </summary>
    public static TypeIdentity Data<D, K>(K target) where K : notnull => 
        (ulong)TypeKind.Data
        | LTypeHelper.Id<D>() 
        | (ulong)TargetFlag.Key 
        | LTypeHelper.Sub<K>() 
        | (uint) target.GetHashCode();
    
    /// <summary>
    /// Object Link (Data Component Keyed with itself)
    /// </summary>
    public static TypeIdentity Link<L>(L target) where L : class => 
        (ulong)TypeKind.Data 
        | LTypeHelper.Id<L>() 
        | (ulong)TargetFlag.Object 
        | LTypeHelper.Sub<L>() 
        | (uint)target.GetHashCode();
    
    /// <summary>
    /// Relation Data
    /// </summary>
    public static TypeIdentity Relation<D>(Entity3 entity) =>
        (ulong)TypeKind.Data
        | LTypeHelper.Id<D>()
        | (ulong)TargetFlag.Entity
        | entity.living;

    /// <summary>
    /// Plain Singleton
    /// </summary>
    public static TypeIdentity Unique<T>() where T : notnull =>
        (ulong)TypeKind.Unique
        | LTypeHelper.Id<T>()
        | (ulong)TargetFlag.None; 

    /// <summary>
    /// Singleton Relation
    /// </summary>
    public static TypeIdentity Unique<S>(S singleton, Entity3 entity) where S : notnull => 
        (ulong)TypeKind.Unique 
        | (ulong)TargetFlag.None 
        | entity.living;

    /// <summary>
    /// Keyed Singleton
    /// </summary>
    public static TypeIdentity Unique<S, K>(S singleton, K target) where S : notnull where K : notnull => 
        (ulong)TypeKind.Unique 
        | (ulong)TargetFlag.Key 
        | LTypeHelper.Id<S>() 
        | (uint) target.GetHashCode();
    
    /// <summary>
    /// Object Link (Data Component Keyed with itself)
    /// </summary>
    public static TypeIdentity Link2<L>(L target) where L : class => 
        (ulong)TypeKind.Data 
        | (ulong)TargetFlag.Object 
        | LTypeHelper.Id<L>() 
        | LTypeHelper.Sub<L>() 
        | (uint)target.GetHashCode();
    

    /// <summary>
    /// Plain Tag
    /// </summary>
    public static TypeIdentity Tag<T>() where T : struct => 
        (ulong)TypeKind.Void 
        | LTypeHelper.Id<T>();

    /// <summary>
    /// Relation Tag
    /// </summary>
    public static TypeIdentity Tag<T>(Entity3 entity) where T : struct =>
        (ulong)TypeKind.Void 
        | LTypeHelper.Id<T>() 
        | entity.living;

    /// <summary>
    /// Keyed Tag
    /// </summary>
    public static TypeIdentity Tag<S>(S target) where S : notnull =>
        (ulong)TypeKind.Void
        | (uint)target.GetHashCode();


    
    
    internal const ulong StorageMask      = 0xF000_0000_0000_0000ul;
    internal const ulong TypeMask         = 0x0FFF_0000_0000_0000ul;
    
    internal const ulong TargetMask       = 0x0000_FFFF_FFFF_FFFFul;
    internal const ulong TargetFlagMask   = 0x0000_F000_0000_0000ul;
    
    internal const ulong EntityFlagMask   = 0x0000_0F00_0000_0000ul;
    internal const ulong WorldMask        = 0x0000_00FF_0000_0000ul;

    // For Typed objects (Object Links, Keyed Components)
    internal const ulong SubMask          = 0x0000_0FFF_0000_0000ul;

    // Header is Generation in concrete entities, but in Types, it is not needed (as no type may reference a dead entity...? but it might, if stored by user...!)
    internal const ulong HeaderMask = 0xFFFF_0000_0000_0000ul;
    internal const ulong GenerationMask = 0xFFFF_0000_0000_0000ul;
}

public enum TargetFlag : ulong
{
    None   = 0UL,
    Entity = 0x0000_E000_0000_0000ul,
    Object = 0x0000_B000_0000_0000ul,
    Key    = 0x0000_C000_0000_0000ul,

    Target = Entity | Object,
    Any    = 0x0000_F000_0000_0000ul,
}

public enum TypeKind : ulong
{
    Void      = 0x0000_0000_0000_0000ul, // Future: Comp<T>.Tag, Comp<T>.Tag<K> - Tags and other 0-size components, saving storage and migration costs.

    Data      = 0x1000_0000_0000_0000ul, // Data Components (Comp<T>.Plain and Comp<T>.Keyed<K> and Comp<T>.Link())

    Unique    = 0x2000_0000_0000_0000ul, // Singleton Components (Comp<T>.Unique)

    Spatial1D = 0x4000_0000_0000_0000ul, // Future: 1D Spatial Components
    Spatial2D = 0x5000_0000_0000_0000ul, // Future: 2D Spatial Components
    Spatial3D = 0x6000_0000_0000_0000ul, // Future: 3D Spatial Components
    
    WildVoid      = 0x8000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.
    WildData      = 0x9000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.
    WildUnique    = 0xA000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.

    WildSpatial1D = 0xC000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.
    WildSpatial2D = 0xD000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.
    WildSpatial3D = 0xE000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.

    WildAny       = 0xF000_0000_0000_0000ul, // Wildcard, details in bottom 32 bits.
}

[Flags]
internal enum WildcardFlag : ulong
{
    None   = 0x0000_1000_0000_0000ul,
    Entity = 0x0000_2000_0000_0000ul,
    Object = 0x0000_4000_0000_0000ul,
    Key    = 0x0000_8000_0000_0000ul,

    Target = Entity | Object,
    Any = None | Entity | Object | Key,
}

[Flags]
internal enum EntityFlags : byte
{
    None = 0,
    Disabled = 1,
}

[StructLayout(LayoutKind.Explicit)]
public record struct LiveEntity(ulong value) : IAddRemoveComponent<Entity>
{
    [FieldOffset(0)]
    public ulong value = value & TypeIdentity.TargetMask;

    [FieldOffset(0)]
    internal int index;

    [FieldOffset(4)]
    internal byte world;

    [FieldOffset(5)]
    internal EntityFlags flags;

    internal World World => World.All[world];

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Entity{world}-{index:x8}/*";
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(index, world);

    #region CRUD

    public Entity Add<C>() where C : notnull, new()
    {
        throw new NotImplementedException();
        //return new Entity(World, value).Add<C>();
    }

    public Entity Add<C>(C value) where C : notnull
    {
        throw new NotImplementedException();
    }

    public Entity Add<T>(Entity target) where T : notnull, new()
    {
        throw new NotImplementedException();
    }

    public Entity Add<R>(R value, Entity relation) where R : notnull
    {
        throw new NotImplementedException();
    }

    public Entity Add<L>(Link<L> link) where L : class
    {
        throw new NotImplementedException();
    }

    public Entity Remove<C>() where C : notnull
    {
        throw new NotImplementedException();
    }

    public Entity Remove<R>(Entity relation) where R : notnull
    {
        throw new NotImplementedException();
    }

    public Entity Remove<L>(L linkedObject) where L : class
    {
        throw new NotImplementedException();
    }

    public Entity Remove<L>(Link<L> link) where L : class
    {
        throw new NotImplementedException();
    }

    #endregion
}

[StructLayout(LayoutKind.Explicit)]
public record struct Entity3
{
    [FieldOffset(0)]
    public ulong value;

    [FieldOffset(0)]
    internal int index;

    [FieldOffset(4)]
    internal byte world;

    [FieldOffset(5)]
    internal EntityFlags flags;

    [FieldOffset(6)]
    internal ushort generation;

    internal Entity3 Successor => this with { generation = (ushort)(generation + 1) };

    internal World World => World.All[world];
    
    internal Meta Meta => World.GetEntityMeta(this);

    internal ulong living
    {
        get
        {
            Debug.Assert(value != default && Meta.Identity.Value == value, $"Entity {this} is not alive.");
            return value & TypeIdentity.TargetMask;
        }
    }

    public static implicit operator Identity(Entity3 self) => self.Identity;

    public Identity Identity => new(value);

    public Entity3(ulong Raw)
    {
        value = Raw;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Entity{world}-{index:x8}/{generation:x4}";
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(index, world);
}

[StructLayout(LayoutKind.Explicit)]
public record struct ObjectLink(ulong value)
{
    [FieldOffset(0)]
    public ulong value = value;

    [FieldOffset(0)]
    internal int hashcode;

    private Type type => LTypeHelper.Resolve(value);

    internal static ObjectLink Of<T>(T target)
    {
        return new((ulong)TypeKind.Data | (ulong)TargetFlag.Object | LTypeHelper.Id<T>() | LTypeHelper.Sub<T>() | (uint)(target == null ? 0 : target.GetHashCode()));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Link<{type.Name}>#{hashcode:x8}";
    }
}

[StructLayout(LayoutKind.Explicit)]
public record struct KeyExpression(ulong value)
{
    [FieldOffset(0)]
    public ulong value;

    [FieldOffset(0)]
    internal int hashcode;

    [FieldOffset(4)]
    private uint header;

    private Type type => LTypeHelper.Resolve(value);
    private Type keyType => LTypeHelper.SubResolve(value);

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Keyed<{type.Name}>({keyType.Name}#{{hashcode:x8}})";
    }
}

[StructLayout(LayoutKind.Explicit)]
public record struct EntityNew : IEquatable<object>
{
    [FieldOffset(0)]
    public ulong value;

    [FieldOffset(0)]
    internal int index; // Index in World Meta, or HashCode for Object

    [FieldOffset(4)]
    private ushort decoration; //Generation or TypeID for Object

    [FieldOffset(6)]
    private short header; //World index or Global Virtual Entity Class

    //TODO Remove us when old classes retired. :)
    public static implicit operator Identity(EntityNew self) => self.Identity;

    public static implicit operator Entity(EntityNew self) => new(self.World, self.Identity);

    public EntityNew(IdClass idClass, TypeID type, int value)
    {
        header = (short)idClass;
        decoration = (ushort)type;
        index = value;
        this.value = (ulong)value << 32 | (ulong)type << 16 | (ushort)idClass;
    }

    internal Identity Identity => new(value);

    private World World => World.All[header];

    internal ref Meta Meta => ref World.GetEntityMeta(this);

    internal EntityNew Successor => this with { decoration = (ushort)(decoration + 1) };

    internal Type Type => header switch
    {
        // Decoration is Object Type Id
        -1 => LanguageType.Resolve((TypeID)decoration),

        // Decoration is Generation
        _ => typeof(Identity),
    };

    internal IdClass Class => header switch
    {
        > 0 => IdClass.Entity,
        _ => (IdClass)header,
    };

    bool IEquatable<object>.Equals(object? obj) =>
        (obj is EntityNew other && other.value == value) || (Class == IdClass.Object && obj != null && obj.GetHashCode() == index);

    public override int GetHashCode() => value.GetHashCode();

    public static EntityNew Of<T>(T target) where T : class => new(IdClass.Object, LanguageType<T>.Id, target.GetHashCode());

    #region CRUD

    /// <summary>
    /// Gets a reference to the Component of type <typeparamref name="C"/> for the entity.
    /// </summary>
    /// <remarks>
    /// Adds the component before if possible.
    /// </remarks>
    /// <param name="match">specific (targeted) Match Expression for the component type. No wildcards!</param>
    /// <typeparam name="C">any Component type</typeparam>
    /// <returns>ref C, reference to the Component</returns>
    /// <remarks>The reference may be left dangling if changes to the world are made after acquiring it. Use with caution.</remarks>
    /// <exception cref="ObjectDisposedException">If the Entity is not Alive..</exception>
    /// <exception cref="KeyNotFoundException">If no C or C(Target) exists in any of the World's tables for entity.</exception>
    public ref C Ref<C>(Match match) where C : struct
    {
        Debug.Assert(Class == IdClass.Entity, $"Only Entities can have Components, this is a {Class}");
        return ref World.GetComponent<C>(this, match);
    }


    /// <inheritdoc cref="Ref{C}(fennecs.Match)"/>
    public ref C Ref<C>()
    {
        Debug.Assert(Class == IdClass.Entity, $"Only Entities can have Components, this is a {Class}");
        return ref World.GetComponent<C>(this, Match.Plain);
    }


    /// <summary>
    /// Gets a reference to the Object Link Target of type <typeparamref name="L"/> for the entity.
    /// </summary>
    /// <param name="link">object link match expressioon</param>
    /// <typeparam name="L">any Component type</typeparam>
    /// <returns>ref C, reference to the Component</returns>
    /// <remarks>The reference may be left dangling if changes to the world are made after acquiring it. Use with caution.</remarks>
    /// <exception cref="ObjectDisposedException">If the Entity is not Alive..</exception>
    /// <exception cref="KeyNotFoundException">If no C or C(Target) exists in any of the World's tables for entity.</exception>
    public ref L Ref<L>(Link<L> link) where L : class
    {
        Debug.Assert(Class == IdClass.Entity, $"Only Entities can have Components, this is a {Class}");
        return ref World.GetComponent<L>(this, link);
    }


    /// <inheritdoc />
    public Entity Add<T>(Entity relation) where T : notnull, new() => Add(new T(), relation);


    /// <inheritdoc cref="Add{R}(R,fennecs.Entity)"/>
    public Entity Add<R>(R value, Entity relation) where R : notnull
    {
        Debug.Assert(Class == IdClass.Entity, $"Only Entities can have Components, this is a {Class}");
        World.AddComponent(Identity, TypeExpression.Of<R>(relation), value);
        return this;
    }

    /// <summary>
    /// Adds a object link to the current entity.
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
    /// <typeparam name="T">Any reference type. The type the object to be linked with the entity.</typeparam>
    /// <param name="link">The target of the link.</param>
    /// <returns>Entity struct itself, allowing for method chaining.</returns>
    public Entity Add<T>(Link<T> link) where T : class
    {
        Debug.Assert(Class == IdClass.Entity, $"Only Entities can have Components, this is a {Class}");
        World.AddComponent(Identity, TypeExpression.Of<T>(link), link.Target);
        return this;
    }

    /// <inheritdoc />
    public Entity Add<C>() where C : notnull, new() => Add(new C());

    /// <summary>
    /// Adds a Plain Component of a specific type, with specific data, to the current entity. 
    /// </summary>
    /// <param name="data">The data associated with the relation.</param>
    /// <typeparam name="T">Any value or reference component type.</typeparam>
    /// <returns>Entity struct itself, allowing for method chaining.</returns>
    public Entity Add<T>(T data) where T : notnull => Add(data, default);


    /// <summary>
    /// Removes a Component of a specific type from the current entity.
    /// </summary>
    /// <typeparam name="C">The type of the Component to be removed.</typeparam>
    /// <returns>Entity struct itself, allowing for method chaining.</returns>
    public Entity Remove<C>() where C : notnull
    {
        Debug.Assert(Class == IdClass.Entity, $"Only Entities can have Components, this is a {Class}");
        World.RemoveComponent(Identity, TypeExpression.Of<C>(Match.Plain));
        return this;
    }


    /// <summary>
    /// Removes a relation of a specific type between the current entity and the target entity.
    /// </summary>
    /// <param name="relation">target of the relation.</param>
    /// <typeparam name="R">backing type of the relation to be removed.</typeparam>
    /// <returns>Entity struct itself, allowing for method chaining.</returns>
    public Entity Remove<R>(Entity relation) where R : notnull
    {
        Debug.Assert(Class == IdClass.Entity, $"Only Entities can have Components, this is a {Class}");
        World.RemoveComponent(Identity, TypeExpression.Of<R>(new Relate(relation)));
        return this;
    }

    /// <inheritdoc />
    public Entity Remove<L>(L linkedObject) where L : class => Remove(Link<L>.With(linkedObject));


    /// <summary>
    /// Removes the link of a specific type with the target object.
    /// </summary>
    /// <typeparam name="T">The type of the link to be removed.</typeparam>
    /// <param name="link">The target object from which the link will be removed.</param>
    /// <returns>Entity struct itself, allowing for method chaining.</returns>
    public Entity Remove<T>(Link<T> link) where T : class
    {
        Debug.Assert(Class == IdClass.Entity, $"Only Entities can have Components, this is a {Class}");
        World.RemoveComponent(Identity, link.TypeExpression);
        return this;
    }


    /// <summary>
    /// Despawns the Entity from the World.
    /// </summary>
    /// <remarks>
    /// The entity builder struct still exists afterwards, but the entity is no longer alive and subsequent CRUD operations will throw.
    /// </remarks>
    public void Despawn() => World.Despawn(this);


    /// <summary>
    /// Checks if the Entity has a Plain Component.
    /// Same as calling <see cref="Has{T}()"/> with <see cref="Identity.Plain"/>
    /// </summary>
    public bool Has<T>() where T : notnull => World.HasComponent<T>(Identity, default);


    /// <inheritdoc />
    public bool Has<R>(Entity relation) where R : notnull => World.HasComponent<R>(Identity, new Relate(relation));


    /// <inheritdoc />
    public bool Has<L>(L linkedObject) where L : class => Has(Link<L>.With(linkedObject));


    /// <summary>
    /// Checks if the Entity has a Component of a specific type.
    /// Allows for a <see cref="Match"/> Expression to be specified (Wildcards)
    /// </summary>
    public bool Has<T>(Match match) => World.HasComponent<T>(Identity, match);

    /// <summary>
    /// Checks if the Entity has an Object Link of a specific type and specific target.
    /// </summary>
    public bool Has<T>(Link<T> link) where T : class => World.HasComponent<T>(Identity, link);

    /// <summary>
    /// Boxes all the Components on the entity into an array.
    /// Use sparingly, but don't be paranoid. Suggested uses: serialization and debugging.
    /// </summary>
    /// <remarks>
    /// Values and References are copied, changes to the array will not affect the Entity.
    /// Changes to objects in the array will affect these objects in the World.
    /// This array is re-created every time this getter is called.
    /// The values are re-boxed each time this getter is called.
    /// </remarks>
    public IReadOnlyList<Component> Components => World.GetComponents(Identity);


    /// <summary>
    /// Gets all Components of a specific type and match expression on the Entity.
    /// Supports relation Wildcards, for example:<ul>
    /// <li><see cref="Entity.Any">Entity.Any</see></li>
    /// <li><see cref="Link.Any">Link.Any</see></li>
    /// <li><see cref="Match.Target">Match.Target</see></li>
    /// <li><see cref="Match.Any">Match.Any</see></li>
    /// <li><see cref="Match.Plain">Match.Plain</see></li>
    /// </ul>
    /// </summary>
    /// <remarks>
    /// This is not intended as the main way to get a component from an entity. Consider <see cref="Stream"/>s instead.
    /// </remarks>
    /// <param name="match">match expression, supports wildcards</param>
    /// <typeparam name="T">backing type of the component</typeparam>
    /// <returns>array with all the component values stored for this entity</returns>
    public T[] Get<T>(Match match) => World.Get<T>(Identity, match);

    #endregion

}

public enum IdClass : short
{
    Entity = 1,
    None = default,
    Object = -1,
    WildAny = -100,
    WildObject = -200,
    WildEntity = -300,
    WildTarget = -400,
}

public record struct Wildcard(long value)
{
    public static Wildcard Any => new(-1);
    public static Wildcard Object => new(-2);
    public static Wildcard Entity => new(-3);
    public static Wildcard Target => new(-4);
}

public record struct Relation(ulong value)
{
    public static Relation To(EntityNew target)
    {
        return new(target.value);
    }

    public static Relation To(Wildcard target)
    {
        return new((ulong)target.value);
    }
}

record struct Txpr
{
    public TypeID type;
    public EntityNew target;
}
