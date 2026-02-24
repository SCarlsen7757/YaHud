using System.Reflection;

var dll = @"C:\Users\MarkCarlsen\.nuget\packages\tmds.dbus.protocol\0.90.3\lib\net8.0\Tmds.DBus.Protocol.dll";
var asm = Assembly.LoadFrom(dll);

// Check Struct<T1,T2,T3> and Struct<T1,T2,T3,T4> methods
var s3 = asm.GetType("Tmds.DBus.Protocol.Struct`3")!;
Console.WriteLine("=== Struct`3 methods ===");
foreach (var m in s3.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
{
    if (m.Name.StartsWith("get_") || m.Name.StartsWith("set_")) continue;
    Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
}
foreach (var p in s3.GetProperties())
    Console.WriteLine($"  prop {p.PropertyType.Name} {p.Name}");

var s4 = asm.GetType("Tmds.DBus.Protocol.Struct`4")!;
Console.WriteLine("\n=== Struct`4 methods ===");
foreach (var m in s4.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
{
    if (m.Name.StartsWith("get_") || m.Name.StartsWith("set_")) continue;
    Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
}

// Check if WriteDictionary overloads are generic
var mw = asm.GetType("Tmds.DBus.Protocol.MessageWriter")!;
Console.WriteLine("\n=== MessageWriter WriteDictionary details ===");
foreach (var m in mw.GetMethods().Where(m => m.Name == "WriteDictionary"))
    Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType + " " + p.Name))})");

// Check DBusConnection constructor
var dbc = asm.GetType("Tmds.DBus.Protocol.DBusConnection")!;
Console.WriteLine("\n=== DBusConnection constructors ===");
foreach (var c in dbc.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
    Console.WriteLine($"  ctor({string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
Console.WriteLine("=== DBusConnection static props ===");
foreach (var p in dbc.GetProperties(BindingFlags.Public | BindingFlags.Static))
    Console.WriteLine($"  {p.PropertyType.Name} {p.Name}");
