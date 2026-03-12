using System;
using System.Linq;
using System.Reflection;

var asm = Assembly.LoadFrom(@"src\BinlogInsights\bin\Release\net8.0\StructuredLogger.dll");
var buildType = asm.GetType("Microsoft.Build.Logging.StructuredLogger.Build");
var sfProp = buildType.GetProperty("SourceFiles");
Console.WriteLine("SourceFiles type: " + sfProp.PropertyType);
Console.WriteLine("SourceFiles type full: " + sfProp.PropertyType.FullName);

// Check generic args 
var genArgs = sfProp.PropertyType.GenericTypeArguments;
if (genArgs.Length > 0) {
    var elemType = genArgs[0];
    Console.WriteLine("Element type: " + elemType.FullName);
    foreach (var p in elemType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        Console.WriteLine("  Property: " + p.Name + " : " + p.PropertyType.Name);
    foreach (var f in elemType.GetFields(BindingFlags.Public | BindingFlags.Instance))
        Console.WriteLine("  Field: " + f.Name + " : " + f.FieldType.Name);
} else {
    // Maybe it implements IEnumerable<T>
    foreach (var iface in sfProp.PropertyType.GetInterfaces()) {
        if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IEnumerable<>)) {
            var elemType = iface.GenericTypeArguments[0];
            Console.WriteLine("IEnumerable element type: " + elemType.FullName);
            foreach (var p in elemType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Console.WriteLine("  Property: " + p.Name + " : " + p.PropertyType.Name);
        }
    }
}

// Also check SourceFilesArchive
var sfaProp = buildType.GetProperty("SourceFilesArchive");
if (sfaProp != null) Console.WriteLine("SourceFilesArchive type: " + sfaProp.PropertyType);
