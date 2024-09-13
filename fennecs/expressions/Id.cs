using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace fennecs;

public record struct LType(ushort index)
{
    public ushort value = (ushort)(index & 0x0FFF);

    public static ulong Id<T>() => (ulong) LanguageType<T>.Id << 48;
}

public record struct TypeIdentity(ulong raw)
{
    public Type type => LanguageType.Resolve((TypeID) typeId.index);

    public TypeKind kind => (TypeKind)((raw & 0xF0000000u) >> 28);

    public Entity2 relation
    {
        get
        {
            Debug.Assert(kind == TypeKind.Relation);
            return new(raw & 0x0000FFFFFFFFFFFF);
        }
    }

    public ObjectLink link
    {
        get
        {
            Debug.Assert(kind == TypeKind.Link);
            return new(raw & 0x0000FFFFFFFFFFFF);
        }
    }

    public ulong key
    {
        get
        {
            Debug.Assert(kind == TypeKind.Keyed);
            return raw & 0x0000FFFFFFFFFFFF;
        }
    }

    private LType typeId => new LType((ushort)((raw >> 48) & 0x0FFF));

    public static TypeIdentity Relation<T>(Entity2 target) => new((ulong)TypeKind.Relation | LType.Id<T>() | target.value);
    public static TypeIdentity Link<T>(T target) => new((ulong)TypeKind.Link | LType.Id<T>() | ObjectLink.Of(target).value);
    public static TypeIdentity Keyed<T, K>(K target) where K : notnull => new(KeyExpression.Of<T, K>(target).value);

    
    public TypeIdentity(LType type, Entity2 relation) : this((ulong)TypeKind.Relation | (ulong)type.index << 48 | relation.value) { }
    public TypeIdentity(LType type, ObjectLink link) : this ((ulong)TypeKind.Link | (ulong)type.index << 48 | link.value){}
}

public enum TypeKind : ulong
{
    Plain =    0xA000000000000000,  // Plain Components (Comp<T>.Plain)
    Relation = 0xE000000000000000,  // Entity Relations (Comp<T>.Matching(Entity e))

    Link =      0xB000000000000000, // Object Links (Comp<T>.Link)
    
    Keyed =     0xC000000000000000, // Keyed Components (Comp<T>.Keyed<K>)

    Devoid    = 0xD000000000000000, // Future: Tags and other 0-size components, saving storage and migration costs.
    Spatial1D = 0x1000000000000000, // Future: 1D Spatial Components
    Spatial2D = 0x2000000000000000, // Future: 2D Spatial Components
    Spatial3D = 0x3000000000000000, // Future: 3D Spatial Components
}


[StructLayout(LayoutKind.Explicit)]
public record struct ObjectLink(ulong value)
{
    [FieldOffset(0)]
    public ulong value = value;
    
    [FieldOffset(0)]
    internal int hashcode; 
    
    [FieldOffset(4)]
    private uint header;
    private uint kind => (header & 0xF0000000u) >> 28;
    private Type type => LanguageType.Resolve((TypeID) ((header & 0x0FFF0000u) >> 16));

    internal static ObjectLink Of<T>(T target)
    {
        return new((ulong)TypeKind.Link | (ulong)LanguageType<T>.Id << 48  | (ulong)LanguageType<T>.Id << 32 | (uint)target!.GetHashCode());
    }
}

[StructLayout(LayoutKind.Explicit)]
public record struct KeyExpression(ulong value)
{
    [FieldOffset(0)]
    public ulong value = value;
    
    [FieldOffset(0)]
    internal int hashcode; 
    
    [FieldOffset(4)]
    private uint header;
    private uint kind => (header & 0xF0000000u) >> 28;
    private Type type => LanguageType.Resolve((TypeID) ((header & 0x0FFF0000u) >> 16));

    internal static KeyExpression Of<T, K>(K target) where K : notnull
    {
        return new((ulong)TypeKind.Keyed | (ulong)LanguageType<T>.Id << 48 | (ulong)LanguageType<K>.Id << 32 | (uint)target.GetHashCode());
    }
}

/* New Identity / Entity / TypeExpression
0x 0000 0000 0000 0000   64 bit (former identity)

   0000 0000 0000 0000   None/Null (default)

   0000 wggg eeee eeee   Entity (World w, Generation g, Entity e)

   Attt 0000 0000 0000   Comp<T>.Plain
   Bttt rrrr cccc cccc   Comp<T>.Link(T Link) - future objects can be retrieved from a registry by index r
   Cttt 0kkk cccc cccc   Comp<T>.Keyed<K>(K key) (custom key typeId k, hash c)
   Dttt 0000 0000 0000   Comp<T>.Plain (future: Tags and other 0-size components, saving storage and migration costs)
   Ettt wggg eeee eeee   Entity Relation (World w, Generation g, EntityIndex e)

 // Hypothetical "Spatial Archetypes" (not implemented)
   1ttt cccc cccc cccc   1D spatial with configuration parameters c
   2ttt cccc cccc cccc   2D spatial with configuration parameters c
   3ttt cccc cccc cccc   3D spatial with configuration parameters c
*/  

[StructLayout(LayoutKind.Explicit)]
public record struct Entity2 : IEquatable<object>
{
    [FieldOffset(0)]
    public ulong value;
    
    [FieldOffset(0)]
    internal int index; // Index in World Meta, or HashCode for Object
    
    [FieldOffset(4)]
    private ushort header;

    public Entity2(ulong value1)
    {
        value = value1;
    }
    
    private int generation => header & 0x0FFF;
    private int worldIndex => header & 0xF000 >> 12;
    private World World => World.All[worldIndex];
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
        header = (short) idClass;
        decoration = (ushort) type;
        index = value;
        this.value = (ulong) value << 32 | (ulong) type << 16 | (ushort) idClass;
    }
    
    internal Identity Identity => new(value);

    private World World => World.All[header];
    
    internal ref Meta Meta => ref World.GetEntityMeta(this);
    
    internal EntityNew Successor => this with { decoration = (ushort)(decoration + 1) };
    internal Type Type => header switch
    {
        // Decoration is Object Type Id
        -1 => LanguageType.Resolve((TypeID) decoration),

        // Decoration is Generation
        _ => typeof(Identity),
    };

    internal IdClass Class => header switch
    {
        > 0 => IdClass.Entity,
        _ => (IdClass) header,
    };

    bool IEquatable<object>.Equals(object? obj) => 
        (obj is EntityNew other && other.value == value) || 
        (Class == IdClass.Object && obj != null && obj.GetHashCode() == index);

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
        return new((ulong) target.value);
    }
}

record struct Txpr
{
    public TypeID type;
    public EntityNew target;
}