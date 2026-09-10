using Microsoft.CodeAnalysis;

namespace Naratteu.StrongDuck;

/// <summary>인터페이스 하나와 소스타입 하나의 짝. 이 짝마다 래퍼 클래스가 하나 나온다.</summary>
sealed record DuckPair(
    string Target,                  // global::Zoo.IDuck
    string Source,                  // global::Zoo.Chick
    string Wrapper,                 // __Duck_Zoo_IDuck__Zoo_Chick
    EquatableArray<string> Usings,  // 확장 프로퍼티처럼 정적호출 문법이 없는 것들 때문에 필요한 using
    EquatableArray<string> Body);   // 명시적 인터페이스 구현문들

/// <summary>호출지점 하나에서 뽑아낸 것.</summary>
sealed record Call(
    bool FromForm,                  // IDuck.From(x) 인가, x.ToDuck<IDuck>() 인가
    string Target,
    string TargetHolder,            // 진입점을 담을 정적클래스 이름 (인터페이스마다 하나)
    DuckPair? Pair,
    LocationInfo? At,
    EquatableArray<DiagInfo> Diagnostics);

/// <summary>인터페이스 멤버 하나하나를 소스타입의 멤버로 이어붙인다.</summary>
static class WrapperBuilder
{
    public static DuckPair? Build(INamedTypeSymbol target, ITypeSymbol source, SemanticModel model, int pos, LocationInfo? at, List<DiagInfo> diags)
    {
        if (source.TypeKind is TypeKind.Dynamic || source.IsAnonymousType || !source.CanBeReferencedByName)
        {
            diags.Add(new(Diags.CannotWrap.Id, source.Fq(), "", at));
            return null;
        }

        List<string> body = [];
        SortedSet<string> usings = [];
        var faces = new List<INamedTypeSymbol>(target.AllInterfaces.Length + 1) { target };
        faces.AddRange(target.AllInterfaces);

        foreach (var face in faces)
            foreach (var m in face.GetMembers())
            {
                if (!m.IsAbstract) continue;                                        // 기본구현이 있으면 건드리지 않는다
                if (m is IMethodSymbol { MethodKind: not MethodKind.Ordinary }) continue;   // 접근자는 프로퍼티/이벤트 쪽에서

                if (m.DeclaredAccessibility is not Accessibility.Public
                    || m is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator or MethodKind.Conversion })
                {
                    diags.Add(new(Diags.UnsupportedMember.Id, face.ToDisplayString(Formats.Short), m.Name, at));
                    continue;
                }

                if (Forward(m, face, source, model, pos, usings) is { } line) body.Add(line);
                else
                {
                    diags.Add(new(Diags.NoMatchingMember.Id, source.ToDisplayString(Formats.Short), $"{face.ToDisplayString(Formats.Short)}.{m.Name}", at));
                    body.Add(Throwing(m, face));
                }
            }

        return new(target.Fq(), source.Fq(), Name(target, source), usings.ToEquatable(), body.ToEquatable());
    }

    public static string Name(ITypeSymbol target, ITypeSymbol source) => $"__Duck_{Id(target)}__{Id(source)}";

    public static string Holder(ITypeSymbol target) => $"__Duck_{Id(target)}_Ext";

    static string Id(ITypeSymbol t) => t.Fq().Replace("global::", "").Sanitize();

    // ===== 멤버 잇기 =====

    static string? Forward(ISymbol m, INamedTypeSymbol face, ITypeSymbol source, SemanticModel model, int pos, SortedSet<string> usings)
    {
        var f = face.Fq();
        switch (m)
        {
            case IMethodSymbol x:
            {
                var recv = x.IsStatic ? source.Fq() : "t";
                if (Call(x, source, model, pos, recv) is not { } call) return null;
                var tp = x.IsGenericMethod ? $"<{x.TypeParameters.Select(t => t.Name).Join()}>" : "";
                return $"{(x.IsStatic ? "static " : "")}{Ret(x)} {f}.{x.Name.Esc()}{tp}({Parms(x.Parameters)}) => {call};";
            }

            case IPropertySymbol { IsIndexer: true } x:
            {
                if (Indexers(source).FirstOrDefault(p => Same(p.Parameters, x.Parameters, model) && Fits(p, x, model)) is not { } ix) return null;
                Import(ix, source, usings);
                return $"{x.Type.Fq()} {f}.this[{Parms(x.Parameters)}] {{ {Accessors(x, $"t[{Args(x.Parameters)}]")} }}";
            }

            case IPropertySymbol x:
            {
                var recv = x.IsStatic ? source.Fq() : "t";
                if (Members(source, model, pos, x.Name).FirstOrDefault(c => Fits(c, x, model)) is not { } got) return null;
                Import(got, source, usings);
                return $"{(x.IsStatic ? "static " : "")}{x.Type.Fq()} {f}.{x.Name.Esc()} {{ {Accessors(x, $"{recv}.{x.Name.Esc()}")} }}";
            }

            case IEventSymbol x:
            {
                var recv = x.IsStatic ? source.Fq() : "t";
                if (Members(source, model, pos, x.Name).OfType<IEventSymbol>()
                        .FirstOrDefault(e => e.IsStatic == x.IsStatic && SymbolEqualityComparer.Default.Equals(e.Type, x.Type)) is not { } ev) return null;
                Import(ev, source, usings);
                return $"{(x.IsStatic ? "static " : "")}event {x.Type.Fq()} {f}.{x.Name.Esc()} {{ add => {recv}.{x.Name.Esc()} += value; remove => {recv}.{x.Name.Esc()} -= value; }}";
            }

            default: return null;
        }
    }

    /// <summary>소스의 멤버가 인터페이스의 프로퍼티/인덱서를 받아줄 수 있는지. 필드도 받는다.</summary>
    static bool Fits(ISymbol got, IPropertySymbol want, SemanticModel model) => got switch
    {
        IPropertySymbol p => p.IsStatic == want.IsStatic
            && (want.GetMethod is null || p.GetMethod is not null && Assignable(p.Type, want.Type, model))
            && (want.SetMethod is null || p.SetMethod is not null && Assignable(want.Type, p.Type, model)),

        // C# 인터페이스는 필드를 못 담으니, 소스가 필드로 갖고 있으면 그걸로 잇는게 맞다
        IFieldSymbol f => f.IsStatic == want.IsStatic
            && (want.GetMethod is null || Assignable(f.Type, want.Type, model))
            && (want.SetMethod is null || !f.IsReadOnly && !f.IsConst && Assignable(want.Type, f.Type, model)),

        _ => false,
    };

    /// <summary>확장멤버로 찾아졌으면, 확장 프로퍼티처럼 정적호출 문법이 없는 것도 있으니 그 네임스페이스를 생성파일에 들여온다.</summary>
    static void Import(ISymbol got, ITypeSymbol source, SortedSet<string> usings)
    {
        if (IsOwn(source, got.ContainingType)) return;
        var outer = got.ContainingType;
        while (outer?.ContainingType is { } up) outer = up;
        if (outer?.ContainingNamespace is { IsGlobalNamespace: false } ns) usings.Add(ns.ToDisplayString());
    }

    static bool IsOwn(ITypeSymbol source, INamedTypeSymbol? owner)
    {
        for (var t = source; t is not null; t = t.BaseType)
            if (SymbolEqualityComparer.Default.Equals(t, owner)) return true;
        return source.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, owner));
    }

    /// <summary>인덱서는 이름으로 조회되지 않으니 타입을 직접 훑는다.</summary>
    static IEnumerable<IPropertySymbol> Indexers(ITypeSymbol source)
    {
        for (var t = source; t is not null; t = t.BaseType)
            foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
                if (p.IsIndexer) yield return p;
        foreach (var i in source.AllInterfaces)
            foreach (var p in i.GetMembers().OfType<IPropertySymbol>())
                if (p.IsIndexer) yield return p;
    }

    /// <summary>소스타입에서 이름이 맞는 멤버를 찾는다. 호출지점 위치를 넘겨서 그 자리에서 보이는 확장멤버까지 같이 본다.</summary>
    static IEnumerable<ISymbol> Members(ITypeSymbol source, SemanticModel model, int pos, string? name = null) =>
        name is null
            ? model.LookupSymbols(pos, (INamespaceOrTypeSymbol)source, includeReducedExtensionMethods: true)
            : model.LookupSymbols(pos, (INamespaceOrTypeSymbol)source, name, includeReducedExtensionMethods: true);

    /// <summary>인터페이스 메서드 하나를 받아줄 호출식을 만든다. 확장함수로 찾아졌으면 완전정규화 정적호출로 적는다.</summary>
    static string? Call(IMethodSymbol want, ITypeSymbol source, SemanticModel model, int pos, string recv)
    {
        var args = Args(want.Parameters);
        var tp = want.IsGenericMethod ? $"<{want.TypeParameters.Select(t => t.Name).Join()}>" : "";

        foreach (var c in Members(source, model, pos, want.Name).OfType<IMethodSymbol>())
        {
            if (c.Arity != want.Arity || c.Parameters.Length != want.Parameters.Length) continue;
            if (c.ReducedFrom is null && c.IsStatic != want.IsStatic) continue;
            if (!Same(c.Parameters, want.Parameters, model)) continue;
            if (want.Arity is 0 && !Assignable(c.ReturnType, want.ReturnType, model)) continue;

            return c.ReducedFrom is { } red
                ? $"{red.ContainingType.Fq()}.{red.Name.Esc()}{tp}({(args is "" ? recv : $"{recv}, {args}")})"
                : $"{recv}.{c.Name.Esc()}{tp}({args})";
        }
        return null;
    }

    static bool Same(IEnumerable<IParameterSymbol> have, IEnumerable<IParameterSymbol> want, SemanticModel model) =>
        have.Zip(want, (h, w) => h.RefKind == w.RefKind && Assignable(w.Type, h.Type, model)).All(ok => ok);

    /// <summary>제네릭 타입파라미터가 끼면 판정을 컴파일러에 맡긴다.</summary>
    static bool Assignable(ITypeSymbol from, ITypeSymbol to, SemanticModel model) =>
        from is ITypeParameterSymbol || to is ITypeParameterSymbol
        || SymbolEqualityComparer.Default.Equals(from, to)
        || ((Microsoft.CodeAnalysis.CSharp.CSharpCompilation)model.Compilation).ClassifyConversion(from, to).IsImplicit;

    // ===== 조각들 =====

    static string Accessors(IPropertySymbol p, string expr) => new[]
    {
        p.GetMethod is null ? null : $"get => {expr};",
        p.SetMethod is null ? null : $"{(p.SetMethod.IsInitOnly ? "init" : "set")} => {expr} = value;",
    }.Where(a => a is not null).Join(" ");

    static string Throwing(ISymbol m, INamedTypeSymbol face) => m switch
    {
        IMethodSymbol x => $"{(x.IsStatic ? "static " : "")}{Ret(x)} {face.Fq()}.{x.Name.Esc()}{(x.IsGenericMethod ? $"<{x.TypeParameters.Select(t => t.Name).Join()}>" : "")}({Parms(x.Parameters)}) => throw new global::System.NotSupportedException();",
        IPropertySymbol { IsIndexer: true } x => $"{x.Type.Fq()} {face.Fq()}.this[{Parms(x.Parameters)}] {{ {ThrowAccessors(x)} }}",
        IPropertySymbol x => $"{x.Type.Fq()} {face.Fq()}.{x.Name.Esc()} {{ {ThrowAccessors(x)} }}",
        IEventSymbol x => $"event {x.Type.Fq()} {face.Fq()}.{x.Name.Esc()} {{ add => throw new global::System.NotSupportedException(); remove => throw new global::System.NotSupportedException(); }}",
        _ => "",
    };

    static string ThrowAccessors(IPropertySymbol p) => new[]
    {
        p.GetMethod is null ? null : "get => throw new global::System.NotSupportedException();",
        p.SetMethod is null ? null : $"{(p.SetMethod.IsInitOnly ? "init" : "set")} => throw new global::System.NotSupportedException();",
    }.Where(a => a is not null).Join(" ");

    static string Ret(IMethodSymbol m) => m.ReturnsVoid ? "void"
        : $"{(m.ReturnsByRefReadonly ? "ref readonly " : m.ReturnsByRef ? "ref " : "")}{m.ReturnType.Fq()}";

    static string Parms(IEnumerable<IParameterSymbol> ps) => ps.Select(p => $"{Mod(p.RefKind)}{p.Type.Fq()} {p.Name.Esc()}").Join();
    static string Args(IEnumerable<IParameterSymbol> ps) => ps.Select(p => $"{ArgMod(p.RefKind)}{p.Name.Esc()}").Join();

    static string Mod(RefKind r) => r switch
    {
        RefKind.Ref => "ref ", RefKind.Out => "out ", RefKind.In => "in ",
        RefKind.RefReadOnlyParameter => "ref readonly ", _ => "",
    };

    static string ArgMod(RefKind r) => r switch
    {
        RefKind.Ref => "ref ", RefKind.Out => "out ",
        RefKind.In or RefKind.RefReadOnlyParameter => "in ", _ => "",
    };
}
