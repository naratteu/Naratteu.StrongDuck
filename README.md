# Naratteu.StrongDuck

[![nuget](https://img.shields.io/nuget/v/Naratteu.StrongDuck)](https://www.nuget.org/packages/Naratteu.StrongDuck)

DuckTyping SourceGenerator for C#

![](https://64.media.tumblr.com/6c8b1ca3567c6b1581e8a8ccd5cae1bf/ac794dc23ce58daa-f5/s500x750/ccd5aca19e5785791d428963df7919e7e810e58a.png)

## How to use

선언해둘 게 없습니다. **부르는 자리가 곧 선언**입니다.

```cs
IDuck[] ducks = [
    IDuck.From(new Chick()),
    IDuck.From(new Dog<int>()),
    IDuck.From(HtmlEncoder.Create()),
];

interface IDuck
{
    string Quack();
    string Flap();
}
```

`IDuck.From(x)` 라고 쓴 자리를 생성기가 찾아, `x` 의 타입에 선언된 `Quack()` 과 `Flap()` 을
`IDuck` 의 멤버와 직결하는 `class` 를 만들어냅니다. 인터페이스에 `[Duck(typeof(..))]` 를
미리 붙여둘 필요가 없습니다 — 어차피 호출지점이 "이 타입을 이 인터페이스로 본다" 고
이미 말하고 있으니까요.

대응하는 멤버가 없으면 **그 호출지점에** 컴파일에러(`DUCK002`)가 납니다. 그래야 Strong 이죠.

```
error DUCK002: 'Cat' 에는 'IDuck.Quack' 에 대응하는 멤버가 없습니다. 확장멤버를 덧붙이면 메꿀 수 있습니다.
```

코드수정이 불가한 타입은 확장멤버로 메꿉니다. **호출지점에서 보이는** 확장멤버를 찾으므로,
그게 어느 네임스페이스에 있든 상관없습니다. (확장함수는 완전정규화해서 부르고,
정적호출 문법이 없는 확장 프로퍼티는 그 네임스페이스를 생성파일에 들여옵니다)

```cs
static class HtmlExtensions
{
    public static string Quack(this HtmlEncoder _) => "html is programming language.";
    public static string Flap(this HtmlEncoder _) => "<flap>👐</flap>";
}
```

[Playground](./Playground) 에서 전부 돌려보실 수 있습니다.

### 두 가지 형태

```cs
IDuck a = IDuck.From(obj);        // C# 14 (.NET 10 SDK) 필요
IDuck b = obj.ToDuck<IDuck>();    // 그 아래에서도 된다
```

`From` 은 C# 14 의 확장 정적 멤버라 .NET 10 SDK 가 필요합니다. 그 아래 버전에서는 `From` 을
아예 만들지 않고 `DUCK003` 으로 알려주며, 예전부터 있던 `ToDuck<T>()` 는 그대로 돕니다.
둘 다 호출지점 스캔으로 도는 같은 물건이라, 섞어 써도 어댑터는 하나만 생깁니다.

## 되는 것

메서드(제네릭 메서드 포함) · 프로퍼티 · 인덱서 · 이벤트 · `out`/`ref`/`ref readonly` ·
상속받은 인터페이스 멤버. 기본구현(DIM)이 있는 멤버는 건드리지 않습니다.

인터페이스의 프로퍼티는 소스의 **필드로도** 이어집니다. C# 인터페이스는 필드를 담을 수
없으니, POCO 의 `public string Name;` 을 `string Name { get; }` 으로 보는 게 자연스럽습니다.

메꾸기는 확장함수와 **확장 프로퍼티** 둘 다 됩니다.

타입이나 접근자가 안 맞으면(`int` 를 `string` 으로, `get` 만 있는데 `set` 이 필요하면)
생성코드가 아니라 **호출지점에** `DUCK002` 가 뜹니다.

| 진단 | 뜻 |
| --- | --- |
| `DUCK001` | 대상이 인터페이스가 아님 |
| `DUCK002` | 소스 타입에 대응하는 멤버가 없음 |
| `DUCK003` | `From` 형태는 C# 14 부터. `ToDuck<T>()` 를 쓰세요 |
| `DUCK004` | 익명 형식·`dynamic` 처럼 이름으로 가리킬 수 없는 타입 |
| `DUCK005` | 연산자·비공개 멤버라 옮길 수 없음 |

### 익명객체는 안 됩니다

```cs
IDuck d = IDuck.From(new { Quack = "꽥" });   // DUCK004
```

익명형식은 소스에서 **이름으로 가리킬 수 없어서**, 감싸는 클래스의 필드 타입을 적을 수가
없습니다. 제네릭으로 담는 것(`From<T>(T obj)`)까지는 되지만, 그 `T` 위에서 멤버에 손댈
방법이 없습니다 — C# 에 구조적 제약이 없으니까요.

리플렉션 없이 뚫어보려고 `[UnsafeAccessor]` 도 찔러봤습니다. (.NET 10 기준)

| 시도 | 결과 |
| --- | --- |
| 수신자를 `T` 로 — `Acc<T>.Get(T obj)` | `BadImageFormatException: Invalid usage of UnsafeAccessorAttribute` |
| 메서드 제네릭 — `GetI<T>(T obj)` | 동일 |
| ``[UnsafeAccessorType("<>f__AnonymousType0`1[..], asm")]`` | 타입은 풀리지만 게터 시그니처가 `int get_i()` 가 아니라 `<i>j__TPar get_i()` 라 `MissingMethodException` |

마지막 건 더 맞춰보면 뚫릴 여지가 있어 보이지만 어차피 못 씁니다. `AnonymousType0` 의 그
**서수를 생성기가 알 수 없기 때문**입니다. 저 번호는 Roslyn 이 emit 단계에서 컴파일 전체를
훑으며 발견 순서대로 매기는 거라, 그보다 먼저 도는 소스제너레이터는 예측할 수 없습니다.
생성기가 코드를 한 줄 얹는 순간 번호가 밀릴 수도 있고요.

대신 [Naratteu.Anonymous](https://github.com/naratteu/Naratteu.Anonymous) 가 **괄호 두 개
차이**로 같은 일을 합니다.

```cs
ii i = ii.New(new() { i = 1 });   // new { } 가 아니라 new() { }

interface ii { int i { get; } }
```

익명형식이 아니라 생성기가 만든 명명 타입에 대한 대상타입추론이라, 컴파일타임에 전부
확정됩니다. 리플렉션 0, AOT 호환, 멤버를 빠뜨리면 `required` 가 컴파일에러로 잡습니다.

경계는 이렇습니다 — **이미 있는 객체**를 인터페이스로 보는 건 StrongDuck 이고,
**그 자리에서 만들어** 인터페이스로 내놓는 건 Anonymous 입니다. 익명객체는 "이미 있는
객체" 처럼 생겼지만 이름이 없어서 전자에 못 들어가고, 후자가 그 자리를 대신합니다.

## 2.0 에서 바뀐 것

- **`[Duck(typeof(..))]` 를 걷어냈습니다.** 호출지점 스캔이 대신합니다. (breaking)
- `IDuck.From(obj)` 형태가 생겼습니다. `ToDuck<T>()` 는 그대로입니다.
- 프로퍼티 · 인덱서 · 이벤트를 지원합니다. (기존 Todo)
  프로퍼티는 소스의 필드로도, 확장 프로퍼티로도 메꿔집니다.
- 확장함수로 메꾼 멤버가 네임스페이스를 넘어가도 제대로 불립니다.
  예전에는 생성코드에 그 `using` 이 없어서 깨졌습니다.
- 못 잇는 멤버를 생성코드의 `CS1061` 대신 호출지점의 `DUCK002` 로 알려줍니다.

## See Also

- https://docs.elementscompiler.com/Concepts/DuckTyping/
- [Naratteu.Anonymous](https://github.com/naratteu/Naratteu.Anonymous) — 같은 수법으로 인터페이스를 그 자리에서 구현하게 해주는 자매품. [익명객체는 안 됩니다](#익명객체는-안-됩니다) 참고
