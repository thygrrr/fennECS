﻿// ReSharper disable file RedundantUsingDirective

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using CodegenCS;
namespace fennecs.generators;

/// <summary>
/// Generator class for CodegenCS https://github.com/Drizin/CodegenCS
/// </summary>
/// <remarks>
/// This is parsed as a CSX template in build target <b>"GenerateCode"</b>
/// </remarks>
// ReSharper disable once UnusedType.Local
file class JobsGenerator
{
    // ReSharper disable once UnusedMember.Local
    public void Main(ICodegenContext context)
    {
        var source = new StringBuilder();

        //source.Clear();
        source.AppendLine(FileHeader());


        foreach (var width in Enumerable.Range(1, 5))
        {
            var top = (1 << width) - 1;
            for (var bits = top; bits >= 0; bits--)
            {
                source.AppendLine(GenerateJobs(false, false, width, bits));
                source.AppendLine(GenerateJobs(true, false, width, bits));
                source.AppendLine(GenerateJobs(false, true, width, bits));
                source.AppendLine(GenerateJobs(true, true, width, bits));
            }
        }                           
        context[$"Jobs.g.cs"].Write($"{source}");
    }
    private FormattableString Memory(bool write, int index)
    {
        //var sb = 
        var w = write ? "W" : "";
        //return $"    internal Storage<C{index}> Storage{index} = default!;";
        return $"    internal MemoryR{w}<C{index}> Memory{index} = default!;";
    }
    
    private FormattableString Memories(int width, string pattern)
    {
        StringBuilder sb = new();
        
        for (var i = 0; i < width; i++)
        {
            sb.AppendLine(Memory(pattern[i] == 'W', i).ToString());
        }
        return FormattableStringFactory.Create(sb.ToString());
    }

    private FormattableString Types(int width)
    {
        StringBuilder sb = new();
        
        for (var i = 0; i < width; i++)
        {
            sb.AppendLine($"    internal TypeExpression Type{i} = default;");
        }
        return FormattableStringFactory.Create(sb.ToString());
    }

    
    private string ActionParams(int width, bool entity, bool uniform, string pattern)
    {
        var typeParams = new StringBuilder();

        //language=C#
        if (entity) typeParams.Append($"EntityRef, ");

        //language=C#
        if (uniform) typeParams.Append($"U, ");

        //language=C#
        for (var i = 0; i < width; i++)
        {
            var rw = pattern[i] == 'W' ? "RW" : "R";
            typeParams.Append($"{rw}<C{i}>");
            if (i < width - 1) typeParams.Append(", ");
        }
        return typeParams.ToString();
    }

    private string Deconstruct(int width)
    {
        var deconstruct = new StringBuilder();
        //language=C#
        for (var i = 0; i < width; i++)
        {
            deconstruct.Append($"var span{i} = Memory{i}.Span; ");
        }
        return deconstruct.ToString();
    }

    private string InvocationParameters(bool entity, bool uniform, string pattern)
    {
        var parameters = new StringBuilder();

        //language=C#
        if (entity) parameters.Append("new(in entity), ");

        //language=C#
        if (uniform) parameters.Append("Uniform, ");

        var index = 0;
        foreach (var p in pattern)
        {
            if (index != 0) parameters.Append(", ");
            parameters.Append(
                //language=C#
                p switch
                {
                    'W' => $"new(ref span{index}[i], in Type{index}, in entity)",
                    'R' => $"new(in span{index}[i])",
                    _ => throw new NotImplementedException(),
                }
            );
            index++;
        }
        return parameters.ToString();
    }
    private string FileHeader()
    {
        return 
            $"""
            // <auto-generated/>
            using System.Runtime.CompilerServices;
            using fennecs.pools;
            using fennecs.storage;
            
            namespace fennecs.jobs;
            
            #pragma warning disable CS0414 // Field is assigned but its value is never used
            // ReSharper disable file IdentifierTypo
            
            """;
}
    
    private  string JobParams(int width, bool uniform)
    {
        var typeParams = new StringBuilder();
        
        if (uniform) typeParams.Append($"U, ");
        
        //language=C#
        for (var i = 0; i < width; i++)
        {
            typeParams.Append($"C{i}");
            if (i < width - 1) typeParams.Append(", ");
        }
        return typeParams.ToString();
    }

    private  string JobConstraints(int width)
    {
        var constraints = new StringBuilder();
        
        //language=C#
        for (var i = 0; i < width; i++)
        {
            constraints.Append($"where C{i} : notnull ");
        }
        return constraints.ToString();
    }

    
    private string GenerateJobs(bool entity, bool uniform, int width, int bits)
    {
        var accessors = $"{bits:b16}"[(16 - width)..].Replace("0", "W").Replace("1", "R");
        var jobParams = JobParams(width, uniform);
        var constraints = JobConstraints(width);
        var actionParams = ActionParams(width, entity, uniform, accessors);
        var invocationParams = InvocationParameters(entity, uniform, accessors);
        var memories = Memories(width, accessors);
        var types = Types(width);
        var deconstruction = Deconstruct(width);
        var jobName = $"Job{(entity ? "E" : "")}{(uniform ? "U" : "")}{accessors}";
        var jobType = $"{jobName}<{jobParams}>";
        var uniforms = uniform ? "    internal U Uniform = default!;" : "";

        return 
            //Language=C#
            $$"""
              internal record {{jobType}} : IThreadPoolWorkItem 
                  {{constraints}}
              {
                  public MemoryR<Entity> MemoryE= default!;
                  public World World = null!;
              
              {{memories}}
              
              {{types}}
              
              {{uniforms}}
              
                  public Action<{{actionParams}}> Action = null!;
                  public CountdownEvent CountDown = null!;
                  public void Execute() 
                  {
                      var entities = MemoryE.Span;
                      var count = entities.Length;
                      
                      {{deconstruction}}
              
                      for (var i = 0; i < count; i++)
                      {
                         var entity = entities[i];
                         Action({{invocationParams}});
                      }
                      CountDown.Signal();
                  }
              }
              
              """;
    }
}