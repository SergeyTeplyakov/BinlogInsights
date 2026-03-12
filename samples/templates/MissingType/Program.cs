using Newtonsoft.Json; // This namespace doesn't exist — no PackageReference

namespace MissingType;

public class Program
{
    public static void Main(string[] args)
    {
        // CS0246: The type or namespace name 'JsonConvert' could not be found
        var json = JsonConvert.SerializeObject(new { Hello = "World" });
        Console.WriteLine(json);

        // Also use a made-up type for a second error
        IMyCustomService service = null!;
        service.DoWork();
    }
}

// This interface is referenced above but the method signature uses a missing type
public interface IMyCustomService
{
    MissingReturnType DoWork(); // CS0246 — MissingReturnType doesn't exist
}
