﻿using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using fennecs;
using fennecs_Components;
using fennecs.pools;
using fennecs.storage;

namespace Benchmark.ECS;

// JIT prefers non-compound assignments in .NET 8
// ReSharper disable ConvertToCompoundAssignment


[ShortRunJob]
//[TailCallDiagnoser]
[ThreadingDiagnoser]
[MemoryDiagnoser]
//[InliningDiagnoser(true, true)]
//[HardwareCounters(HardwareCounter.CacheMisses)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[AnyCategoriesFilter("fennecs", "fennecs2")]
// ReSharper disable once IdentifierTypo
public class TensorBenchmarks
{
    private Stream<Component1, Component2> _query = null!;
    private World _world = null!;

    // ReSharper disable once MemberCanBePrivate.Global
    [Params(100_000)] public int entityCount { get; set; } = 100_000;

    // ReSharper disable once MemberCanBePrivate.Global
    [Params(10)] public int entityPadding { get; set; } = 10;

    [GlobalSetup]
    public void Setup()
    {
        PooledList<UniformWork<Component1, Component2, Component3>>.Rent().Dispose();

        _world = new World();
        _query = _world.Query<Component1, Component2>().Stream();
        for (var i = 0; i < entityCount; ++i)
        {
            for (var j = 0; j < entityPadding; ++j)
            {
                var padding = _world.Spawn();
                switch (j % 3)
                {
                    case 0:
                        padding.Add<Component1>();
                        break;
                    case 1:
                        padding.Add<Component2>();
                        break;
                }
            }

            _world.Spawn().Add<Component1>()
                .Add(new Component2 {Value = 9.81f})
                .Add(new Component3 {Value = 1});
        }

        _query.Query.Warmup();
        _query.Job(Workload);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _world.Dispose();
    }

    /// <summary>
    /// This could be a static anonymous delegate, but this way, we don't need to repeat ourselves
    /// and reduce the risk of errors when refactoring or unit testing.
    /// </summary>
    private static void Workload(RW<Component1> c1, RW<Component2> c2)
    {
        const float dt = 1f / 60.0f;
        c1.write = new Component1(c1.read.Value + c2.read.Value * dt);
    }

    [BenchmarkCategory("fennecs")]
    [Benchmark(Description = "fennecs (For)", Baseline = true)]
    public void fennecs_For()
    {
        _query.For(static(c1, c2) =>
        {
            const float dt = 1f / 60.0f;
            c1.write = new Component1(c1.read.Value + c2.read.Value * dt);
        });
    }


    [BenchmarkCategory("fennecs")]
    //[Benchmark(Description = "fennecs (For WL)")]
    public void fennecs_For_WL()
    {
        _query.For(Workload);
    }


    [BenchmarkCategory("fennecs")]
    //[Benchmark(Description = $"fennecs (Job)")]
    public void fennecs_Job()
    {
        _query.Job(static delegate(ref Component1 c1, ref Component2 c2)
        {
            const float dt = 1f / 60.0f;
            c1.Value = c1.Value + c2.Value * dt;
        });
    }

    [BenchmarkCategory("fennecs")]
    [Benchmark(Description = "fennecs (Raw)")]
    public void fennecs_Raw()
    {
        _query.Raw(Raw_Workload_Unoptimized);
    }

    [BenchmarkCategory("fennecs")]
    //[Benchmark(Description = "fennecs (Raw U4)")]
    public void fennecs_Raw_Unroll4()
    {
        // fennecs guarantees contiguous memory access in the form of Query<>.Raw(MemoryAction<>)
        // Raw runners are intended to process data or transfer it via the fastest available means,
        // Example use cases:
        //  - transfer buffers to/from GPUs or Game Engines
        //  - Disk, Database, or Network I/O
        //  - SIMD calculations
        //  - snapshotting / copying / rollback / compression / decompression / diffing / permutation

        // As example / reference & benchmark, we calculate using an unrolled loop
        _query.Raw(Raw_Workload_Unroll4);
    }

    [BenchmarkCategory("fennecs")]
    //[Benchmark(Description = "fennecs (Raw U8)")]
    public void fennecs_Raw_Unroll8()
    {
        // fennecs guarantees contiguous memory access in the form of Query<>.Raw(MemoryAction<>)
        // Raw runners are intended to process data or transfer it via the fastest available means,
        // Example use cases:
        //  - transfer buffers to/from GPUs or Game Engines
        //  - Disk, Database, or Network I/O
        //  - SIMD calculations
        //  - snapshotting / copying / rollback / compression / decompression / diffing / permutation

        // As example / reference & benchmark, we calculate using an unrolled loop
        _query.Raw(Raw_Workload_Unroll8);
    }

    [BenchmarkCategory("fennecs", nameof(Avx2))]
    [Benchmark(Description = "fennecs (Raw AVX2)")]
    public void fennecs_Raw_AVX2()
    {
        // fennecs guarantees contiguous memory access in the form of Query<>.Raw(MemoryAction<>)
        // Raw runners are intended to process data or transfer it via the fastest available means,
        // Example use cases:
        //  - transfer buffers to/from GPUs or Game Engines
        //  - Disk, Database, or Network I/O
        //  - SIMD calculations
        //  - snapshotting / copying / rollback / compression / decompression / diffing / permutation

        // As example / reference & benchmark, we vectorized our calculation here using AVX2
        _query.Raw(Raw_Workload_AVX2);
    }


    [BenchmarkCategory("fennecs", "Tensor")]
    [Benchmark(Description = "fennecs (Raw Tensor)")]
    public void fennecs_Raw_Tenor()
    {
        // fennecs guarantees contiguous memory access in the form of Query<>.Raw(MemoryAction<>)
        // Raw runners are intended to process data or transfer it via the fastest available means,
        // Example use cases:
        //  - transfer buffers to/from GPUs or Game Engines
        //  - Disk, Database, or Network I/O
        //  - SIMD calculations
        //  - snapshotting / copying / rollback / compression / decompression / diffing / permutation

        // As example / reference & benchmark, we vectorized our calculation here using AVX2
        _query.Raw(Raw_Workload_Tensor);
    }

    [BenchmarkCategory("fennecs", nameof(Sse2))]
    [Benchmark(Description = "fennecs (Raw SSE2)")]
    public void fennecs_Raw_SSE2()
    {
        // fennecs guarantees contiguous memory access in the form of Query<>.Raw(MemoryAction<>)
        // Raw runners are intended to process data or transfer it via the fastest available means,
        // Example use cases:
        //  - transfer buffers to/from GPUs or Game Engines
        //  - Disk, Database, or Network I/O
        //  - SIMD calculations
        //  - snapshotting / copying / rollback / compression / decompression / diffing / permutation

        // As example / reference & benchmark, we vectorized our calculation here using SSE2
        _query.Raw(Raw_Workload_SSE2);
    }

    [BenchmarkCategory("fennecs", nameof(AdvSimd))]
    [Benchmark(Description = "fennecs (Raw AdvSIMD)")]
    public void fennecs_Raw_AdvSIMD()
    {
        // fennecs guarantees contiguous memory access in the form of Query<>.Raw(MemoryAction<>)
        // Raw runners are intended to process data or transfer it via the fastest available means,
        // Example use cases:
        //  - transfer buffers to/from GPUs or Game Engines
        //  - Disk, Database, or Network I/O
        //  - SIMD calculations
        //  - snapshotting / copying / rollback / compression / decompression / diffing / permutation

        // As example / reference & benchmark, we vectorized our calculation here using Arm AdvSIMD
        _query.Raw(Raw_Workload_AdvSIMD);
    }

    private static void Raw_Workload_Unoptimized(Memory<Component1> c1V, Memory<Component2> c2V)
    {
        var c1S = c1V.Span;
        var c2S = c2V.Span;

        const float dt = 1f / 60.0f;

        for (var i = 0; i < c1S.Length; i++)
        {
            c1S[i].Value = c1S[i].Value + c2S[i].Value * dt;
        }
    }

    private static void Raw_Workload_Unroll4(Memory<Component1> c1V, Memory<Component2> c2V)
    {
        var c1 = c1V.Span;
        var c2 = c2V.Span;
        const float dt = 1f / 60.0f;

        var i = 0;
        for (; i < c1.Length; i += 4)
        {
            var j = i + 1;
            var k = j + 1;
            var l = k + 1;
            c1[i].Value = c1[i].Value + c2[i].Value * dt;
            c1[j].Value = c1[j].Value + c2[j].Value * dt;
            c1[k].Value = c1[k].Value + c2[k].Value * dt;
            c1[l].Value = c1[l].Value + c2[l].Value * dt;
        }

        for (; i < c1.Length; i += 1)
        {
            c1[i].Value = c1[i].Value + c2[i].Value * dt;
        }
    }

    private static void Raw_Workload_Unroll8(Memory<Component1> c1V, Memory<Component2> c2V)
    {
        var c1 = c1V.Span;
        var c2 = c2V.Span;

        const float dt = 1f / 60.0f;
        
        var i = 0;
        for (; i < c1.Length; i += 8)
        {
            var j = i + 1;
            var k = i + 2;
            var l = i + 3;
            var m = i + 4;
            var n = i + 5;
            var o = i + 6;
            var p = i + 7;
            
            // ReSharper disable ConvertToCompoundAssignment
            c1[i].Value = c1[i].Value + c2[i].Value * dt;
            c1[j].Value = c1[j].Value + c2[j].Value * dt;
            c1[k].Value = c1[k].Value + c2[k].Value * dt;
            c1[l].Value = c1[l].Value + c2[l].Value * dt;
            c1[m].Value = c1[m].Value + c2[m].Value * dt;
            c1[n].Value = c1[n].Value + c2[n].Value * dt;
            c1[o].Value = c1[o].Value + c2[o].Value * dt;
            c1[p].Value = c1[p].Value + c2[p].Value * dt;
        }

        for (; i < c1.Length; i += 1)
        {
            c1[i].Value = c1[i].Value + c2[i].Value * dt;
        }
    }

    private static void Raw_Workload_AVX2(Memory<Component1> c1V, Memory<Component2> c2V)
    {
        var count = c1V.Length;

        using var mem1 = c1V.Pin();
        using var mem2 = c2V.Pin();

        var dt1 = 1f / 60.0f;
        var dt = Vector256.Create(dt1);

        unsafe
        {
            var p1 = (float*) mem1.Pointer;
            var p2 = (float*) mem2.Pointer;

            var vectorSize = Vector256<float>.Count;
            var vectorEnd = count - count % vectorSize;
            for (var i = 0; i <= vectorEnd; i += vectorSize)
            {
                var v1 = Avx.LoadVector256(p1 + i);
                var v2 = Avx.LoadVector256(p2 + i);
                
                var sum = Avx.Add(v1, Avx.Multiply(v2, dt));

                Avx.Store(p1 + i, sum);
            }

            for (var i = vectorEnd; i < count; i++) // remaining elements
            {
                p1[i] = p1[i] + p2[i] * dt1;
            }
        }
    }

    private static void Raw_Workload_Tensor(Memory<Component1> c1V, Memory<Component2> c2V)
    {
        var c1I = MemoryMarshal.Cast<Component1, float>(c1V.Span);
        var c2I = MemoryMarshal.Cast<Component2, float>(c2V.Span);

        TensorPrimitives.MultiplyAdd(c2I, 1f/60.0f, c1I, c1I);
    }

    private static void Raw_Workload_SSE2(Memory<Component1> c1V, Memory<Component2> c2V)
    {
        (int Item1, int Item2) range = (0, c1V.Length);

        using var mem1 = c1V.Pin();
        using var mem2 = c2V.Pin();

        var dt1 = 1f / 60.0f;
        var dt = Vector128.Create(dt1);

        unsafe
        {
            var p1 = (float*) mem1.Pointer;
            var p2 = (float*) mem2.Pointer;

            var vectorSize = Vector128<float>.Count;
            var i = range.Item1;
            var vectorEnd = range.Item2 - vectorSize;
            for (; i <= vectorEnd; i += vectorSize)
            {
                var v1 = Sse.LoadVector128(p1 + i);
                var v2 = Sse.LoadVector128(p2 + i);
                
                var sum = Sse.Add(v1, Sse.Multiply(v2, dt));    

                Sse.Store(p1 + i, sum);
            }

            for (; i < range.Item2; i++) // remaining elements
            {
                p1[i] = p1[i] + p2[i] * dt1;
            }
        }
    }

    private static void Raw_Workload_AdvSIMD(Memory<Component1> c1V, Memory<Component2> c2V)
    {
        (int Item1, int Item2) range = (0, c1V.Length);

        using var mem1 = c1V.Pin();
        using var mem2 = c2V.Pin();

        var dt1 = 1f / 60.0f;
        var dt = Vector128.Create(dt1);

        unsafe
        {
            var p1 = (float*) mem1.Pointer;
            var p2 = (float*) mem2.Pointer;

            var vectorSize = Vector128<float>.Count;
            var i = range.Item1;
            var vectorEnd = range.Item2 - vectorSize;
            for (; i <= vectorEnd; i += vectorSize)
            {
                var v1 = AdvSimd.LoadVector128(p1 + i);
                var v2 = AdvSimd.LoadVector128(p2 + i);
                
                var sum = AdvSimd.Add(v1, AdvSimd.Multiply(v2, dt));

                AdvSimd.Store(p1 + i, sum);
            }

            for (; i < range.Item2; i++) // remaining elements
            {
                p1[i] = p1[i] + p2[i] * dt1;
            }
        }
    }
}