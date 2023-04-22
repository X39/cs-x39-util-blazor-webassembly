namespace X39.Util.Blazor.WebAssembly;

/// <summary>
/// Type to indicate a reference to the assembly.
/// </summary>
public class Assembly
{
    private Assembly()
    {
    }

    /// <summary>
    /// Returns the assembly.
    /// </summary>
    public static System.Reflection.Assembly Get => typeof(Assembly).Assembly;
}