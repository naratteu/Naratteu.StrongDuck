using System.Text.Encodings.Web;
using Zoo;
using Zoo.Helpers;

// [Duck(..)] 같은 선언이 없다. 여기 이 호출들이 곧 선언이다.
IDuck[] ducks = [
    IDuck.From(new Chick()),
    IDuck.From(new Dog<int>()),
    IDuck.From(new WhatThe.Fox()),
    IDuck.From(HtmlEncoder.Create()),
    new Chick().ToDuck<IDuck>(),        // 옛 형태도 그대로 산다
];

Console.WriteLine($"Here are {ducks.Length} ducks....");
Console.WriteLine(new string('-', 10));
foreach (var c in ducks) Console.WriteLine(c.Quack());
Console.WriteLine(new string('-', 10));
foreach (var c in ducks) Console.WriteLine(c.Flap());
Console.WriteLine(new string('-', 10));

// 프로퍼티도 이어붙는다. 남의 타입 두 개를 같은 취급으로.
INamed[] named = [
    INamed.From(typeof(int)),
    INamed.From(new DirectoryInfo(".")),
];
foreach (var n in named) Console.WriteLine($"{n.Name}");

namespace Zoo
{
    public interface IDuck
    {
        string Quack();
        string Flap();
    }

    public interface INamed
    {
        string Name { get; }
    }

    public class Chick
    {
        public string Quack() => "🐤: peek peek!";
        public string Flap() => "🐥: flap flap!";
    }

    public class Dog<T>
    {
        public string Quack() => "🐶: bark bark!";
        public string Flap() => "🐕? 🐾 🐾 .. ~";
    }
}

namespace WhatThe
{
    public class Fox
    {
        public string Say(string msg) => $"🦊: {msg}";
        public string Quack() => Say("quack quack!");
    }
}

// 고칠 수 없는 타입은 확장함수로 메꾼다.
// 이 네임스페이스는 생성코드가 using 하지 않지만, 호출지점에서 보이므로 찾아내 완전정규화로 부른다.
namespace Zoo.Helpers
{
    using System.Text.Encodings.Web;

    public static class FoxExtensions
    {
        public static string Flap(this WhatThe.Fox fox) => fox.Say("fak fak!");
    }

    public static class HtmlExtensions
    {
        public static string Quack(this HtmlEncoder _) => "html is programming language.";
        public static string Flap(this HtmlEncoder _) => "<flap>👐</flap>";
    }
}
