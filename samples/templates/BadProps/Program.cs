namespace BadProps;

public class Program
{
    public static void Main(string[] args)
    {
        // CS8600 warning (null assigned to non-nullable) — becomes error due to TreatWarningsAsErrors
        string message = null;
        Console.WriteLine(message.Length);

        // CS0219 warning — variable assigned but never used — also becomes error
        int unused = 42;

        // Intentional nullable warning
        string? maybeNull = GetValue();
        Console.WriteLine(maybeNull.Length); // CS8602 — possible null dereference
    }

    static string? GetValue() => null;
}
