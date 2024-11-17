﻿// ReSharper disable file RedundantUsingDirective
using System;
using System.Collections.Generic;
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
file class StreamsForGenerator
{
    // ReSharper disable once UnusedMember.Local
    public void Main(ICodegenContext context)
    {
        var source = new StringBuilder();
        
        source.AppendLine(FileHeader());
        
        foreach (var width in Enumerable.Range(1, 5))
        {
            source.AppendLine(ClassHeader(width));

            var top = (1 << width) - 1;
            for (var bits = top; bits >= 0; bits--)
            {
                source.AppendLine(GenerateFor(false, false, width, bits));
                source.AppendLine(GenerateFor(true, false, width, bits));
                source.AppendLine(GenerateFor(false, true, width, bits));
                source.AppendLine(GenerateFor(true, true, width, bits));
            }

            source.AppendLine(ClassFooter());                        
        }                           
        context["Streams.For.g.cs"].Write($"{source}");
    }

    private static string ActionParams(int width, bool entity, bool uniform, string pattern)
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

    private static string TypeParams(int width)
    {
        var typeParams = new StringBuilder();

        //language=C#
        for (var i = 0; i < width; i++)
        {
            typeParams.Append($"C{i}");
            if (i < width - 1) typeParams.Append(", ");
        }
        return typeParams.ToString();
    }

    private static string Select(int width)
    {
        var select = new StringBuilder();
        if (width > 1) select.Append("(");
        //language=C#
        for (var i = 0; i < width; i++)
        {
            select.Append($"s{i}");
            if (i < width - 1) select.Append(", ");
        }
        if (width > 1) select.Append(")");
        return select.ToString();
    }

    private static string Deconstruct(int width, string pattern)
    {
        var deconstruct = new StringBuilder();
        //language=C#
        for (var i = 0; i < width; i++)
        {
            deconstruct.Append($"var span{i} = s{i}.Span; ");
            if (pattern[i] == 'W') deconstruct.Append($"var type{i} = s{i}.Expression; ");
        }
        return deconstruct.ToString();
    }

    private static string InvocationParameters(bool entity, bool uniform, string pattern)
    {
        var parameters = new StringBuilder();

        //language=C#
        if (entity) parameters.Append("new(in entity), ");

        //language=C#
        if (uniform) parameters.Append("uniform, ");

        var index = 0;
        foreach (var p in pattern)
        {
            if (index != 0) parameters.Append(", ");
            parameters.Append(
                //language=C#
                p switch
                {
                    'W' => $"new(ref span{index}[i], ref writes[i], in entity, in type{index})",
                    'R' => $"new(ref span{index}[i])",
                    _ => throw new NotImplementedException(),
                }
            );
            index++;
        }
        return parameters.ToString();
    }

    private static string ClassHeader(int width)
    {
        //language=C#
        return $$"""               
               public partial record Stream<{{TypeParams(width)}}>
               {
               """;
    }
    private static string ClassFooter()
    {
        //language=C#
        return $$"""
                 }
                 
                 
                 """;
    }
    
    private static string FileHeader()
    {
        return 
            $"""
            // <auto-generated/>
            using System.Runtime.CompilerServices;
            using fennecs.pools;
            using fennecs.storage;
            
            namespace fennecs;
            
            """;
}
    
    private static string GenerateFor(bool entity, bool uniform, int width, int bits)
    {
        var pattern = $"{bits:b16}".Substring(16 - width).Replace("0", "W").Replace("1", "R");
        
        return //Language=C#
            $$"""        
              
                      /// <include file='../_docs.xml' path='members/member[@name="T:For{{(entity ? "E" : "")}}{{(uniform ? "U" : "")}}"]'/>
                      [OverloadResolutionPriority(0b_{{(!entity ? 1 << width : 0)&255:b8}}_{{bits&255:b8}})]
                      public void For{{(uniform ? "<U>(U uniform, " : "(")}}Action<{{ActionParams(width, entity, uniform, pattern)}}> action)
                      {
                         using var worldLock = World.Lock();
              
                         foreach (var table in Filtered)
                         {
                             using var join = table.CrossJoin<{{TypeParams(width)}}>(StreamTypes.AsSpan());
                             if (join.Empty) continue;
                             
                             Span<bool> writes = stackalloc bool[{{width}}];
              
                             var count = table.Count;
                             do
                             {
                                 var {{Select(width)}} = join.Select;
                                 {{Deconstruct(width, pattern)}}
                                 for (var i = 0; i < count; i++)
                                 {   
                                     var entity = table[i];
                                     action({{InvocationParameters(entity, uniform, pattern)}}); 
                                 }
                             } while (join.Iterate());
                         }
                      }
                      
                      
              """;
    }

}