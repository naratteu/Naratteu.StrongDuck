using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Naratteu.StrongDuck;

namespace Naratteu.StrongDuck.Tests;

public class DuckTypeGeneratorTests
{
    [Fact]
    public async Task GeneratesDuckAdapterFromDuckAttribute()
    {
        var test = new CSharpSourceGeneratorTest<DuckTypeGenerator, DefaultVerifier>
        {
            TestCode = /*lang=C#*/"""
                using Naratteu.StrongDuck;

                [Duck(typeof(Chick))]
                [Duck(typeof(Dog))]
                interface IDuck
                {
                    string Quack();
                }

                class Chick
                {
                    public string Quack() => "chick";
                }

                class Dog
                {
                    public string Quack() => "dog";
                }
                """,
        };

        void Expect(string name, [StringSyntax("C#")] string code) => test.TestState.GeneratedSources.Add((typeof(DuckTypeGenerator), name, code));

        Expect("Microsoft.CodeAnalysis.EmbeddedAttribute.cs", """
            namespace Microsoft.CodeAnalysis
            {
                internal sealed partial class EmbeddedAttribute : global::System.Attribute
                {
                }
            }
            """);

        Expect("DuckAttribute.g.cs", """
            using System;
            namespace Naratteu.StrongDuck
            {
                [Microsoft.CodeAnalysis.Embedded]
                [AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
                public class DuckAttribute(Type type) : Attribute { }
            }
            """);

        Expect("IDuck.g.cs", """
            namespace Naratteu.StrongDuck { partial class Debug { } }
            public static class IDuckDuckExtensions
            {
                internal static IDuck ToDuck<_>(this Chick t) where _ : IDuck => new ChickDuck(t);
                class ChickDuck(Chick t) : IDuck
                {
                    string IDuck.Quack() => t.Quack();
                }
                internal static IDuck ToDuck<_>(this Dog t) where _ : IDuck => new DogDuck(t);
                class DogDuck(Dog t) : IDuck
                {
                    string IDuck.Quack() => t.Quack();
                }
            }
            """);

        await test.RunAsync();
    }
}
