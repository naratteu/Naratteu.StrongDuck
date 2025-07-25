# Naratteu.StrongDuck
DuckTyping SourceGenerator for C#

![](https://64.media.tumblr.com/6c8b1ca3567c6b1581e8a8ccd5cae1bf/ac794dc23ce58daa-f5/s500x750/ccd5aca19e5785791d428963df7919e7e810e58a.png)

## How to use

This is a style inspired by [`[JsonSerializable(typeof(T))]`](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation)

```cs
[Duck(typeof(A))]
[Duck(typeof(B))]
[Duck(typeof(..))]
interface IDuck
{
    string Quack();
    string Flap();
}
```

위 코드만 입력하면, `A`와 `B`에 선언된 `public string Quack()` 과 `public string Flap()` 함수를 `IDuck`의 맴버와 직결하는 `class`를 생성합니다.

만일 대응하는 함수가 없다면 컴파일에러가 발생하며, 코드수정이 불가한 타입일경우 확장함수를 덧붙이는것으로 해결합니다.

```cs
IDuck[] ducks = [
    new A().ToDuck<IDuck>(),
    new B().ToDuck<IDuck>(),
    ..
];
```

이후, 위와같이 관계없는 타입을 동일타입으로 변환해 취급할수 있게됩니다. [Playground](./Playground) 에서 테스트해보실 수 있습니다.

## Todo

- Support Property, Indexer, etc..
