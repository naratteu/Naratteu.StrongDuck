using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Naratteu.StrongDuck;

[Generator]
public class DuckTypeGenerator : IIncrementalGenerator
{
    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("DuckAttribute.g.cs", SourceText.From("""
        using System;
        namespace Naratteu.StrongDuck
        {
            [AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
            public class DuckAttribute(Type type) : Attribute { }
        }
        """, Encoding.UTF8)));
        var attr = context.SyntaxProvider.ForAttributeWithMetadataName("Naratteu.StrongDuck.DuckAttribute", (_, _) => true, (c, _) => c);
        context.RegisterSourceOutput(attr.Collect(), (spc, sources) =>
        {
            foreach (var s in sources)
            {
                if (s is not { TargetSymbol: INamedTypeSymbol ITarget }) continue;
                var lines = Lines("""
                namespace Naratteu.StrongDuck { partial class Debug { } }
                """).Concat(Lines(ITarget.ContainingNamespace is { } ns ? $$"""
                namespace {{ns}}
                {
                    //<inner>
                }
                """ : """
                //<inner>
                """)).Inject("//<inner>", Lines($$"""
                public static class {{ITarget.Name}}DuckExtensions
                {
                    //<inner>
                }
                """)).Inject("//<inner>", s.Attributes.SelectMany(attr => attr switch
                {
                    { ConstructorArguments: [TypedConstant { Value: INamedTypeSymbol type }] }
                        when Sanitize(type) is { } Target => Lines($$"""
                        internal static {{ITarget}} ToDuck<_>(this {{type}} t) where _ : {{ITarget}} => new {{Target}}Duck(t);
                        class {{Target}}Duck({{type}} t) : {{ITarget}}
                        {
                            //<inner>
                        }
                        """).Inject("//<inner>", ITarget.GetMembers().SelectMany(mem => mem switch
                        {
                            IMethodSymbol { ReturnType: { } r, Name: { } n, Parameters: { } p } => Lines($"""
                            {r} {ITarget}.{n}({string.Join(", ", p)}) => t.{n}({string.Join(", ", p.Select(p => p.Name))});
                            """),
                            _ => []
                        })),
                    _ => []
                }));
                spc.AddSource($"{s.TargetSymbol}.g.cs", SourceText.From(string.Join("\r\n", lines), Encoding.UTF8));

                static string Sanitize(INamedTypeSymbol nts) => new StringBuilder()
                    .Append(nts)
                    .Replace('.', 'ㅇ')
                    .Replace('<', 'ㅓ')
                    .Replace('>', 'ㅏ')
                    .ToString();
                static IEnumerable<string> Lines(string code) => code.Split(["\r\n", "\n", "\r"], default);
            }
        });
    }
}

file static class Exts
{
    public static IEnumerable<string> Inject(this IEnumerable<string> lines, string key, IEnumerable<string> replace)
    {
        foreach (var line in lines)
        {
            if (line.Split([key], default) is [var tab, _])
                foreach (var l in replace)
                    yield return tab + l;
            else yield return line;
        }
    }
}
