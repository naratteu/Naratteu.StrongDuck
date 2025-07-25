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

위 코드만 입력하면, `A`와 `B`에 선언된 `public string Quack()` 과 `public string Flap()` 함수를 `IDuck`의 맴버와 직결하는 `class`를 생성합니다. 만일 대응하는 함수가 없다면, 확장함수로 선언하면 됩니다.

## Todo

- Support Property, Indexer, etc..
