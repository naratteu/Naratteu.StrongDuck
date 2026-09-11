using Microsoft.CodeAnalysis;

namespace Naratteu.StrongDuck;

static class Diags
{
    const string Category = "Naratteu.StrongDuck";

    public static readonly DiagnosticDescriptor NotAnInterface = new("DUCK001",
        "덕타이핑 대상이 인터페이스가 아님",
        "'{0}' 은(는) 인터페이스가 아니라서 덕타이핑 대상이 될 수 없습니다",
        Category, DiagnosticSeverity.Warning, true);

    public static readonly DiagnosticDescriptor NoMatchingMember = new("DUCK002",
        "대응하는 멤버가 없음",
        "'{0}' 에는 '{1}' 에 대응하는 멤버가 없습니다. 확장멤버를 덧붙이면 메꿀 수 있습니다.",
        Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor ExtensionNeedsCSharp14 = new("DUCK003",
        "From 형태는 C# 14 부터",
        "'{0}.From(..)' 은 C# 14 가 필요합니다. LangVersion 을 올리거나 .ToDuck<{0}>() 로 부르세요.",
        Category, DiagnosticSeverity.Warning, true);

    public static readonly DiagnosticDescriptor CannotWrap = new("DUCK004",
        "감쌀 수 없는 타입",
        "'{0}' 은(는) 이름으로 가리킬 수 없어서(익명 형식, dynamic 등) 덕타이핑할 수 없습니다",
        Category, DiagnosticSeverity.Warning, true);

    public static readonly DiagnosticDescriptor UnsupportedMember = new("DUCK005",
        "옮길 수 없는 인터페이스 멤버",
        "'{0}' 의 '{1}' 은(는) 연산자거나 비공개 멤버라 덕타이핑으로 옮길 수 없습니다",
        Category, DiagnosticSeverity.Error, true);

    public static readonly Dictionary<string, DiagnosticDescriptor> ById = new()
    {
        [NotAnInterface.Id] = NotAnInterface,
        [NoMatchingMember.Id] = NoMatchingMember,
        [ExtensionNeedsCSharp14.Id] = ExtensionNeedsCSharp14,
        [CannotWrap.Id] = CannotWrap,
        [UnsupportedMember.Id] = UnsupportedMember,
    };
}

sealed record DiagInfo(string Id, string A, string B, LocationInfo? Where)
{
    public Diagnostic ToDiagnostic() => Diagnostic.Create(Diags.ById[Id], Where?.ToLocation(), A, B);
}

sealed record LocationInfo(string Path, int Start, int Length, int L1, int C1, int L2, int C2)
{
    public static LocationInfo? From(Location l) => l.SourceTree is null ? null : new(
        l.SourceTree.FilePath, l.SourceSpan.Start, l.SourceSpan.Length,
        l.GetLineSpan().StartLinePosition.Line, l.GetLineSpan().StartLinePosition.Character,
        l.GetLineSpan().EndLinePosition.Line, l.GetLineSpan().EndLinePosition.Character);

    public Location ToLocation() => Location.Create(Path, new(Start, Length), new(new(L1, C1), new(L2, C2)));
}
