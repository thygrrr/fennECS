using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace Benchmark.Conceptual;

[SimpleJob]
public class StructLayout
{
    [StructLayout(LayoutKind.Auto)]
    public record struct StructAuto(ulong raw)
    {
        public int index32
        {
            get => (int)(raw & 0xFFFFFFFFu);
            set => raw = (raw & 0xFFFFFFFF00000000u) | (uint)value;
        }
        
        public short header
        {
            get => (short)((raw & 0xFFFF000000000000u)>>48);
            set => raw = (raw & 0x0000FFFFFFFFFFFFu) | ((ulong)value << 48);
        }
        
        public int index64
        {
            get => (int)(raw & 0xFFFFFFFFu);
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
            set => raw = (raw & 0xFFFFFFFF00000000u) | (ulong) value;
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
        }
        
        public TypeKind kind
        {
            get => (TypeKind)((raw & 0xF0000000u) >> 28);
            set => raw = ((ulong) value & 0xF) << 28;
        }

        public override int GetHashCode()
        {
            return raw.GetHashCode();
        }
    }


    [StructLayout(LayoutKind.Explicit)]
    public record struct StructExplicit(ulong _raw)
    {
        [FieldOffset(0)]
        private ulong _raw = _raw;

        [FieldOffset(0)]
        public int index;

        [FieldOffset(6)]
        public short header;
        
        public TypeKind kind
        {
            get => (TypeKind)((_raw & 0xF0000000u) >> 28);
            set => _raw = ((ulong) value & 0xF) << 28;
        }

        public override int GetHashCode()
        {
            return _raw.GetHashCode();
        }
    }

    
    //[Benchmark]
    public StructAuto[] CreateAuto()
    {
        var rnd = new Random(1234);
        var array = new StructAuto[100_000];
        for (var i = 0; i < array.Length; i++)
        {
            array[i] = new((ulong) rnd.NextInt64());
        }
        return array;
    }
    
    //[Benchmark]
    public StructExplicit[] CreateExplicit()
    {
        var rnd = new Random(1234);
        var array = new StructExplicit[100_000];
        for (var i = 0; i < array.Length; i++)
        {
            array[i] = new((ulong) rnd.NextInt64());
        }
        return array;
    }
    

    //[Benchmark]
    public StructAuto[] SetIndexAuto32()
    {
        var rnd = new Random(1234);
        var array = new StructAuto[1_000_000];
        for (var i = 0; i < array.Length; i++)
        {
            array[i].index32 = rnd.Next();
        }
        return array;
    }
    
    //[Benchmark]
    public StructAuto[] SetIndexAuto64()
    {
        var rnd = new Random(1234);
        var array = new StructAuto[1_000_000];
        for (var i = 0; i < array.Length; i++)
        {
            array[i].index64 = rnd.Next();
        }
        return array;
    }
    
    //[Benchmark(Baseline = true)]
    public StructExplicit[] SetIndexExplicit()
    {
        var rnd = new Random(1234);
        var array = new StructExplicit[1_000_000];
        for (var i = 0; i < array.Length; i++)
        {
            array[i].index = rnd.Next();
        }
        return array;
    }
    

    //[Benchmark]
    public StructAuto[] SetHeaderAuto()
    {
        var rnd = new Random(1234);
        var array = new StructAuto[1_000_000];
        for (var i = 0; i < array.Length; i++)
        {
            array[i].header = (short) rnd.Next();
        }
        return array;
    }

    //[Benchmark(Baseline = true)]
    public StructExplicit[] SetHeaderExplicit()
    {
        var rnd = new Random(1234);
        var array = new StructExplicit[1_000_000];
        for (var i = 0; i < array.Length; i++)
        {
            array[i].header = (short) rnd.Next();
        }
        return array;
    }

    [Benchmark()]
    public int[] HashAuto()
    {
        var rnd = new Random(1234);
        var array = new int[10_000_000];
        for (var i = 0; i < array.Length; i++)
        {
            array[i] = new StructAuto((ulong)rnd.NextInt64()).GetHashCode();
        }
        return array;
    }

    [Benchmark(Baseline = true)]
    public int[] HashExplicit()
    {
        var rnd = new Random(1234);
        var array = new int[10_000_000];
        for (var i = 0; i < array.Length; i++)
        {
            array[i] = new StructExplicit((ulong)rnd.NextInt64()).GetHashCode();
        }
        return array;
    }


    public enum TypeKind : uint
    {
        Plain = 0xA,    // Plain Components (Comp<T>.Plain)
        Relation = 0xE, // Entity Relations (Comp<T>.Matching(Entity e))

        Link = 0xB,         // Object Links (Comp<T>.Link)
    
        Keyed = 0xC,        // Keyed Components (Comp<T>.Keyed<K>)

        Devoid = 0xD,    // Future: Tags and other 0-size components, saving storage and migration costs.
        Spatial1D = 0x1, // Future: 1D Spatial Components
        Spatial2D = 0x2, // Future: 2D Spatial Components
        Spatial3D = 0x3, // Future: 3D Spatial Components
    }

}
