using Naratteu.StrongDuck;
using System.Text.Encodings.Web;
using Playground;

IDuck[] ducks = [
    new Chick().ToDuck<IDuck>(),
    new Dog<int>().ToDuck<IDuck>(),
    new WhatThe.Fox().ToDuck<IDuck>(),
    HtmlEncoder.Create().ToDuck<IDuck>(),
];

Console.WriteLine($"Here are {ducks.Length} ducks....");
Console.WriteLine(new string('-', 10));
foreach (var c in ducks) Console.WriteLine(c.Quack());
Console.WriteLine(new string('-', 10));
foreach (var c in ducks) Console.WriteLine(c.Flap());
Console.WriteLine(new string('-', 10));

namespace Playground
{
    [Duck(typeof(Chick))]
    [Duck(typeof(Dog<int>))]
    [Duck(typeof(WhatThe.Fox))]
    [Duck(typeof(HtmlEncoder))]
    interface IDuck
    {
        string Quack();
        string Flap();
    }
}

class Chick
{
    public string Quack() => "🐤: peek peek!";
    public string Flap() => "🐥: flap flap!";
}

class Dog<T>
{
    public string Quack() => "🐶: bark bark!";
    public string Flap() => "🐕? 🐾 🐾 .. ~";
}

namespace WhatThe
{
    class Fox
    {
        public string Say(string msg) => $"🦊: {msg}";
        public string Quack() => Say("quack quack!");
    }
}

static class FoxExtensions
{
    public static string Flap(this WhatThe.Fox fox) => fox.Say("fak fak!");
}

static class HtmlExtensions
{
    public static string Quack(this HtmlEncoder _) => "html is programming language.";
    public static string Flap(this HtmlEncoder _) => "<flap>👐</flap>";
}