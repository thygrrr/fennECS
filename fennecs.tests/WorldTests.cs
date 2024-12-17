using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace fennecs.tests;

[SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local")]
public class WorldTests(ITestOutputHelper output)
{
    [Fact]
    public void Creates()
    {
        using var world = new World();
        Assert.NotNull(world);
    }


    [Fact]
    public void Disposes()
    {
        var world = new World();
        world.Dispose();
    }

    [Fact]
    public void Has_ToString()
    {
        using var world = new World();
        var str = world.ToString();
        output.WriteLine(str);
        Assert.StartsWith(nameof(World), str);
    }

    [Fact]
    public void Spawns_valid_Entities()
    {
        using var world = new World();
        var entity = world.Spawn();
        Assert.True(entity.Alive);
        Assert.True(entity.Key.IsEntity);
    }


    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(100_000)]
    private void Can_Spawn_Many_Bare_Entities(int count)
    {
        using var world = new World();
        for (var i = 0; i < count; i++)
        {
            var entity = world.Spawn();
            Assert.True(entity.Alive);
        }
    }


    [Theory]
    [InlineData(0)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(1_000_000)]
    private void Can_Batch_Spawn_Bare(int count)
    {
        using var world = new World();
        var identities = world.SpawnBare(count);
        Assert.Equal(count, identities.Count);
        Assert.Equal(count, identities.ToImmutableSortedSet().Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(123)]
    [InlineData(9_000)]
    [InlineData(69_420)]
    private void Can_Batch_Spawn(int count)
    {
        using var world = new World();
        using var spawner = world.Entity()
            .Add(555)
            .Add("hallo")
            .Spawn(count);

        var query = world.Query<int, string>().Stream();
        Assert.Equal(count, query.Count);

        query.For((i, s) =>
        {
            Assert.Equal(555, i);
            Assert.Equal("hallo", s);
            i.write++;
            s.write = "correct.";
        });
    }


    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(123)]
    [InlineData(9_000)]
    private void Can_Batch_Spawn_Twice(int count)
    {
        using var world = new World();
        using var spawner = world.Entity();

        spawner.Add(555)
            .Add("hallo")
            .Spawn(count);

        spawner.Add(420.0f);
        spawner.Spawn(count);

        var query = world.Query<int, string>().Stream();
        Assert.Equal(count * 2, query.Count);

        query.For((i, s) =>
        {
            Assert.Equal(555, i);
            Assert.Equal("hallo", s);
            i.write++;
            s.write = "correct.";
        });
    }


    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    private void Batch_Spawn_with_Duplicate_Replaces(int count)
    {
        using var world = new World();
        world.Entity()
            .Add(555)
            .Add(666)
            .Spawn(count);

        var query = world.Query<int>().Stream();
        Assert.Equal(count, query.Count);
        query.For((i) => Assert.Equal(666, i));
    }


    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(123)]
    [InlineData(9_000)]
    [InlineData(69_420)]
    private void Can_Batch_Spawn_Linked(int count)
    {
        using var world = new World();
        world.Entity()
            .Add(555)
            .Add(Link.With("dieter"))
            .Spawn(count);

        var query = world.Query<int, string>(default, Link.With("dieter")).Stream();
        Assert.Equal(count, query.Count);

        query.For((i, s) =>
        {
            Assert.Equal(555, i);
            Assert.Equal("dieter", s);
        });
    }


    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(123)]
    [InlineData(9_000)]
    [InlineData(69_420)]
    private void Can_Batch_Spawn_Related(int count)
    {
        using var world = new World();
        var other = world.Spawn();

        world.Entity()
            .Add(555)
            .Add("relation", other)
            .Spawn(count);

        var query = world.Query<int, string>(default(Key), other).Stream();
        Assert.Equal(count, query.Count);

        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Global
        query.For((i, s) =>
        {
            Assert.Equal(555, i);
            Assert.Equal("relation", s);
        });
    }


    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(123)]
    [InlineData(9_000)]
    [InlineData(69_420)]
    private void Can_Batch_Spawn_Entity_With_No_Components(int count)
    {
        using var world = new World();
        world.Entity().Spawn(count);

        var query = world.Query().Compile();
        Assert.Equal(count, query.Count);
    }


    [Theory]
    [InlineData(1)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    public void Cannot_Spawn_while_Iterating_EntityRoot(int count)
    {
        var world = new World();
        for (var i = 0; i < count; i++) world.Spawn();

        var query = world.Query<Entity>(default(Key)).Stream();

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var _ in query) world.Spawn();
        });

        world.Dispose();
    }


    [Theory]
    [InlineData(0)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    public void Cannot_Safely_Spawn_in_Raw(int count)
    {
        var world = new World();
        for (var i = 0; i < count; i++) world.Spawn();

        var query = world.Query<Entity>(default(Key)).Stream();
        Assert.Throws<InvalidOperationException>(() =>
            query.Raw(world, (uniform, _) =>
            {
                for (var i = 0; i < count; i++)
                {
                    var entity = uniform.Spawn();
                    Assert.True(entity.Alive);
                }
            }));

        world.Dispose();
    }


    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    public void Can_Safely_Spawn_in_ForEach(int count)
    {
        var world = new World(1);
        for (var i = 0; i < count; i++) world.Spawn();

        var stream = world.Query<Entity>().Stream();
        stream.For(world, (uniform, _) =>
        {
            var entity = uniform.Spawn();
            Assert.True(entity.Alive);
            Thread.Yield();
        });

        world.Dispose();
    }


    [Fact]
    public void Count_Accurate()
    {
        using var world = new World();
        Assert.Equal(0, world.Count);

        var e1 = world.Spawn();
        Assert.Equal(1, world.Count);

        e1.Add(new { });
        Assert.Equal(1, world.Count);

        var e2 = world.Spawn();
        e2.Add(new { });
        Assert.Equal(2, world.Count);
    }


    [Fact]
    public void Despawn_Target_Removes_Relation_From_Origins()
    {
        using var world = new World();
        var target1 = world.Spawn();
        var target2 = world.Spawn();

        for (var i = 0; i < 1000; i++)
        {
            world.Spawn().Add(666, target1);
            world.Spawn().Add(444, target2);
        }

        var query1 = world.Query<Entity>(default(Key)).Has<int>(target1).Stream();
        var query2 = world.Query<Entity>(default(Key)).Has<int>(target2).Stream();

        Assert.Equal(1000, query1.Count);
        Assert.Equal(1000, query2.Count);
        world.Despawn(target1);
        Assert.Equal(0, query1.Count);
        Assert.Equal(1000, query2.Count);
    }


    [Fact]
    public void Added_Newable_Class_is_not_Null()
    {
        using var world = new World();
        var entity = world.Spawn().Add<NewableClass>();
        Assert.True(world.HasComponent<NewableClass>(entity, default));
        Assert.NotNull(world.GetComponent<NewableClass>(entity, default));
    }


    [Theory]
    [ClassData(typeof(KeyGenerator))]
    public void Added_Newable_Struct_is_default(Key key)
    {
        using var world = new World();
        var entity = world.Spawn().Add<NewableStruct>(key);
        Assert.True(world.HasComponent<NewableStruct>(entity, key));
        Assert.Equal(default, world.GetComponent<NewableStruct>(entity, key));
    }


    [Theory]
    [ClassData(typeof(KeyGenerator))]
    public void Can_add_Non_Newable(Key key)
    {
        using var world = new World();
        var entity = world.Spawn().Add<string>("12", key);
        Assert.True(world.HasComponent<string>(entity, key));
        Assert.NotNull(world.GetComponent<string>(entity, key));
    }


    [Theory]
    [ClassData(typeof(KeyGenerator))]
    public void Adding_Component_in_Deferred_Mode_Is_Deferred(Key key)
    {
        using var world = new World();
        var entity = world.Spawn();
        var worldLock = world.Lock();

        entity.Add(666);
        Assert.False(world.HasComponent<int>(entity, key));
        worldLock.Dispose();
        Assert.True(world.HasComponent<int>(entity, key));
        Assert.Equal(666, world.GetComponent<int>(entity, key));
    }


    [Fact]
    public void Can_Lock_and_Unlock_World()
    {
        using var world = new World();
        using var worldLock = world.Lock();
    }


    [Fact]
    public void Can_Lock_Locked_World()
    {
        using var world = new World();
        using var worldLock = world.Lock();
    }


    [Fact]
    public void Apply_Can_Spawn_while_Locked()
    {
        using var world = new World();
        using var worldLock = world.Lock();
        var entity = world.Spawn();
        Assert.True(world.IsAlive(entity));
    }


    [Theory]
    [ClassData(typeof(KeyGenerator))]
    public void Apply_Deferred_Add(Key key)
    {
        using var world = new World();
        var entity = world.Spawn();

        var worldLock = world.Lock();
        entity.Add(666);

        Assert.False(world.HasComponent<int>(entity, key));
        worldLock.Dispose();

        Assert.True(world.HasComponent<int>(entity, key));
        Assert.Equal(666, world.GetComponent<int>(entity, key));
    }


    [Fact]
    public void Apply_Deferred_Remove()
    {
        using var world = new World();
        var entity = world.Spawn().Add(666);
        var worldLock = world.Lock();
        entity.Remove<int>();

        worldLock.Dispose();
        Assert.False(world.HasComponent<int>(entity, default));
    }


    [Fact]
    public void Apply_Deferred_Despawn()
    {
        using var world = new World();
        var entity = world.Spawn().Add(666).Add("hallo");
        var worldLock = world.Lock();
        world.Despawn(entity);
        Assert.True(world.IsAlive(entity));
        worldLock.Dispose();
        Assert.False(world.IsAlive(entity));
    }


    [Fact]
    public void Apply_Deferred_Relation()
    {
        using var world = new World();
        var entity = world.Spawn();
        var target = world.Spawn();

        var worldLock = world.Lock();
        entity.Add(666, target);
        Assert.False(entity.Has<int>(target));
        worldLock.Dispose();
        Assert.True(entity.Has<int>(target));
    }


    [Fact]
    public void Apply_Deferred_Relation_Remove()
    {
        using var world = new World();
        var entity = world.Spawn();
        var target = world.Spawn();
        using var worldLock = world.Lock();
        entity.Add(666, target);
        entity.Remove<int>(target);
        Assert.False(world.HasComponent<int>(entity, default), default);
        Assert.False(world.HasComponent<int>(target, default));

        Assert.False(world.HasComponent<int>(entity, default));
        Assert.False(world.HasComponent<int>(target, default));
    }


    [Fact]
    private void Can_Remove_Components_in_Reverse_Order()
    {
        using var world = new World();
        var entity = world.Spawn().Add(666).Add("hallo");
        entity.Remove<int>();
        Assert.False(world.HasComponent<int>(entity, default));
        entity.Remove<string>();
        Assert.False(world.HasComponent<string>(entity, default));
    }


    [Fact]
    private void Can_Test_for_Entity_Relation_Component_Presence()
    {
        using var world = new World();
        var entity = world.Spawn();
        var target = world.Spawn();
        entity.Add(666, target);
        Assert.True(entity.Has<int>(target));
    }


    [Fact]
    private void Can_Test_for_Type_Relation_Component_Presence()
    {
        using var world = new World();
        var entity = world.Spawn();
        object target = new { };
        entity.Add(Link.With(target));
        Assert.True(entity.Has<object>(target.Key())); //TODO: Probably wrong.
    }


    [Fact]
    private void Can_Add_Component_with_T_new()
    {
        using var world = new World();
        var entity = world.Spawn();
        entity.Add<NewableStruct>();
        Assert.True(world.HasComponent<NewableStruct>(entity, default));
    }


    [Fact]
    private void Can_Remove_Component_with_Object_and_Entity_Target()
    {
        using var world = new World();
        var entity = world.Spawn();
        object target = new { };
        entity.Add(Link.With(target));
        var typeExpression = TypeExpression.Of(target.GetType(), target.Key());
        world.RemoveComponent(entity, typeExpression);
        Assert.False(world.HasComponent(entity, typeExpression));
    }


    [Fact]
    private void Can_Relate_Over_Entity()
    {
        using var world = new World();
        var entity = world.Spawn();
        var other = world.Spawn();

        entity.Add<int>(other);
        Assert.True(entity.Has<int>(other));
    }


    [Fact]
    private void Cannot_Add_null_Component_Data()
    {
        using var world = new World();
        var entity = world.Spawn();
        Assert.Throws<ArgumentNullException>(() => entity.Add<string>(null!));
    }


    [Fact]
    private void Can_Despawn_With_Entity()
    {
        using var world = new World();
        var entity = world.Spawn();
#pragma warning restore CS0618 // Type or member is obsolete
        world.Despawn(entity);
        Assert.False(world.IsAlive(entity));
    }


    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(69)]
    [InlineData(420)]
    private void Can_Despawn_With_Entity_Span(int entityCount)
    {
        using var world = new World();
        var entities = new Entity[entityCount];
        for (var i = 0; i < entityCount; i++) entities[i] = world.Spawn();
        world.Despawn(entities.AsSpan());
        for (var i = 0; i < entityCount; i++) Assert.False(world.IsAlive(entities[i]));
    }


    [Fact]
    private void Can_Despawn_All_With_Plain()
    {
        using var world = new World();
        var target = world.Spawn();
        var entity1 = world.Spawn().Add("hallo");
        var entity2 = world.Spawn().Add(Link.With("to the past"));
        var entity3 = world.Spawn().Add<string>("to the future", target);
        var entity4 = world.Spawn().Add(666);
        world.DespawnAllWith<string>(default(Key));
        Assert.False(world.IsAlive(entity1));
        Assert.True(world.IsAlive(entity2));
        Assert.True(world.IsAlive(entity3));
        Assert.True(world.IsAlive(entity4));
    }


    [Fact]
    private void Can_Despawn_All_With_Any()
    {
        using var world = new World();
        var target = world.Spawn();
        var entity1 = world.Spawn().Add("hallo");
        var entity2 = world.Spawn().Add(Link.With("to the past"));
        var entity3 = world.Spawn().Add<string>("to the future", target);
        var entity4 = world.Spawn().Add(666);
        world.DespawnAllWith<string>(Match.Any);
        Assert.False(world.IsAlive(entity1));
        Assert.False(world.IsAlive(entity2));
        Assert.False(world.IsAlive(entity3));
        Assert.True(world.IsAlive(entity4));
    }


    [Fact]
    private void Can_Despawn_All_With_Object()
    {
        using var world = new World();
        var target = world.Spawn();
        var entity1 = world.Spawn().Add("hallo");
        var entity2 = world.Spawn().Add(Link.With("to the past"));
        var entity3 = world.Spawn().Add<string>("to the future", target);
        var entity4 = world.Spawn().Add(666);
        world.DespawnAllWith<string>(Match.Link);
        Assert.True(world.IsAlive(entity1));
        Assert.False(world.IsAlive(entity2));
        Assert.True(world.IsAlive(entity3));
        Assert.True(world.IsAlive(entity4));
    }


    [Fact]
    private void Can_Despawn_All_With_Relation()
    {
        using var world = new World();
        var target = world.Spawn();
        var entity1 = world.Spawn().Add("hallo");
        var entity2 = world.Spawn().Add(Link.With("to the past"));
        var entity3 = world.Spawn().Add<string>("to the future", target);
        var entity4 = world.Spawn().Add(666);
        world.DespawnAllWith<string>(Match.Target);
        Assert.True(world.IsAlive(entity1));
        Assert.False(world.IsAlive(entity2));
        Assert.False(world.IsAlive(entity3));
        Assert.True(world.IsAlive(entity4));
    }


    [Fact]
    private void Can_Take_Out_Multiple_Locks()
    {
        using var world = new World();
        var lock1 = world.Lock();
        var lock2 = world.Lock();

        var e = world.Spawn();
        e.Add<float>();

        Assert.False(e.Has<float>());
        lock1.Dispose();
        Assert.False(e.Has<float>());
        lock2.Dispose();
        Assert.True(e.Has<float>());
    }


    [Fact]
    private void Can_Garbage_Collect()
    {
        using var world = new World();

        var e = world.Spawn();
        e.Add<float>(world.Spawn());

        var stream = world.Query<float>(Match.Any).Stream();
        Assert.Single(stream);
        e.Despawn();
        Assert.Single(stream.Query.Archetypes);
        world.GC();
        Assert.Empty(stream.Query.Archetypes);
    }


    [Fact]
    private void Cannot_Garbage_Collect_in_Locked_World()
    {
        using var world = new World();
        using var worldLock = world.Lock();
        Assert.Throws<InvalidOperationException>(() => world.GC());
    }


    [Fact]
    private void Has_Name()
    {
        using var world = new World
        {
            Name = "hallo",
        };
        Assert.Equal("hallo", world.Name);
    }


    [Fact]
    private void Has_GCBehaviour()
    {
        using var world = new World
        {
            GCBehaviour = World.GCAction.DefaultBeta,
        };
        Assert.Equal(World.GCAction.DefaultBeta, world.GCBehaviour);
    }

    [Fact]
    private void Provides_Universal_Query()
    {
        using var world = new World();
        var query = world.All;
        Assert.NotNull(query);
        Assert.Equal(0, query.Count);
        var entity = world.Spawn();
        Assert.Equal(1, query.Count);
        entity.Add<float>();
        Assert.Equal(1, query.Count);
        Assert.Equal(1, world.Count);
    }

    struct Predicted;

    [Fact]
    private void Has_Correct_Count()
    {
        using var world = new World();
        var quickStream = world.Stream<Predicted>();
        var queryStream = world.Query<Predicted>().Stream();
        Assert.Equal(queryStream.Count, quickStream.Count);

        world.Spawn(); // unrelated entity
        world.Spawn().Add(new Predicted());
        world.Spawn().Add(new Predicted());

        Assert.Equal(3, world.Count);
    }

    [Fact]
    private void Stream_Has_Same_Count_As_QueryStream()
    {
        using var world = new World();
        var quickStream = world.Stream<Predicted>();
        var queryStream = world.Query<Predicted>().Stream();
        Assert.Equal(queryStream.Count, quickStream.Count);

        world.Spawn(); // unrelated entity
        world.Spawn().Add(new Predicted());
        world.Spawn().Add(new Predicted());

        var quickCount = 0;
        quickStream.For(
            (_, _) => { quickCount++; }
        );

        var queryCount = 0;
        queryStream.For(
            (_, _) => { queryCount++; }
        );

        Assert.Equal(2, quickCount);
        Assert.Equal(2, queryCount);
        Assert.Equal(quickCount, quickStream.Count);
        Assert.Equal(queryCount, queryStream.Count);
    }

    [Fact]
    private void Stream_Has_Same_Count_As_QueryStream_Relations()
    {
        using var world = new World();
        var quickStream = world.Stream<Predicted>(Match.Any);
        var queryStream = world.Query<Predicted>(Match.Any).Stream();
        Assert.Equal(queryStream.Count, quickStream.Count);

        var target1 = world.Spawn();
        var target2 = world.Spawn();
        world.Spawn().Add(new Predicted(), target1).Add(new Predicted(), target2);

        var quickCount = 0;
        quickStream.For(
            (_, _) => { quickCount++; }
        );

        var queryCount = 0;
        queryStream.For(
            (_, _) => { queryCount++; }
        );

        Assert.Equal(2, quickCount);
        Assert.Equal(2, queryCount);
        Assert.Equal(1, quickStream.Count);
        Assert.Equal(1, queryStream.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(69)]
    [InlineData(420)]
    [InlineData(1_000_000)]
    private void Can_Create_With_Capacity(int capacity)
    {
        using var world = new World(capacity);
        Assert.NotNull(world);

        var entity = world.Spawn();
        Assert.True(world.IsAlive(entity));
    }

    private class NewableClass;

    private struct NewableStruct;

    
    private class KeyGenerator : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return [default(Key)];
            yield return [Key.Of(123)];
            yield return [Key.Of("dieter")];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}
