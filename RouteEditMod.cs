using System.Runtime.InteropServices;
using Utilities;

namespace RouteEdit;


public static class ModEntry
{
    public static readonly string ModName = nameof(RouteEdit);

    [UnmanagedCallersOnly]
    public static int InitializeMod()
    {
        _ = ModInit.InitializeMod(ModName);
        return 0;
    }
}
