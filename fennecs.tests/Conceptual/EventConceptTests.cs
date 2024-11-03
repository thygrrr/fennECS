namespace fennecs.tests.Conceptual;

public class EventConceptTests
{
    public EventConceptTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static ITestOutputHelper _output = null!;

    public interface IAdded<T>
    {
        public delegate void SingleAdded(Entity entity, T value);
        public delegate void BulkAdded(Span<Entity> entities, Span<T> values);

        public static event SingleAdded? Subscribers;
        public static event BulkAdded? Bulk;

        static void Added(Entity entity, T value)
        {
            Subscribers?.Invoke(entity, value);
            Bulk?.Invoke(new[] { entity }, new[] { value });
        }
    }

    public interface IModified<in C>
    {
        static void Modified(C value)
        {
            _output.WriteLine($"Modified {value}");
        }
    }

    public interface IRemoved<in T>
    {
        static void Removed(Entity entity, T value)
        {
            _output.WriteLine($"Removed {value}");
        }
    }

    public record struct TestComponent(int Value) :
        IAdded<TestComponent>,
        IModified<TestComponent>,
        IRemoved<TestComponent>
    {
        private int _value = Value;

        public int Value
        {
            readonly get => _value;
            set
            {
                _value = value;
                IModified<TestComponent>.Modified(this);
            }
        }
    }

    [Fact]
    public void Test()
    {
        IAdded<TestComponent>.Subscribers += (entity, value) =>
        {
            _output.WriteLine($"Single Add {value} to {entity}");
        };
        
        IAdded<TestComponent>.Bulk += (entities, values) =>
        {
            _output.WriteLine($"Bulk Add {values.ToArray()} to {entities.ToArray()}");
        };
        
        using var world = new World();
        var entity = world.Spawn();

        var component = new TestComponent(1);
        entity.TestAdd(component);

        component.Value = 2;
        component.Value = 3;
        component.Value = 4;

        entity.TestRemove<TestComponent>();
    }
}

public static class EntityExtensions
{
    public static void TestAdd<T>(this Entity entity, T component) where T : notnull
    {
        entity.Add(component);
        if (component is EventConceptTests.IAdded<T>)
        {
            EventConceptTests.IAdded<T>.Added(entity, component);
        }
    }

    public static void TestRemove<T>(this Entity entity) where T : notnull
    {
        entity.Remove<T>();
        if (typeof(T).IsAssignableTo(typeof(EventConceptTests.IRemoved<T>)))
        {
            EventConceptTests.IRemoved<T>.Removed(entity, default!);
        }
    }

    
    public class LinkRef<T> where T : notnull
    {
        public T? Value { 
            get;
            set;
        }
        
        public static implicit operator LinkRef<T>(T value) => new() { Value = value };

        public static implicit operator T(LinkRef<T> value)
        {
            if (value.Value is null) throw new InvalidOperationException("LinkRef is null");
            return value.Value;
        }
    }
}