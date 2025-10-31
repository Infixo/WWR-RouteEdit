using HarmonyLib;
using System.Reflection;
using System.Runtime.InteropServices;
using Utilities;

namespace RouteEdit;


public static class ModEntry
{
    public static string ModName { get; private set; } = nameof(RouteEdit);
    public static string HarmonyId { get; private set; } = String.Empty;


    [UnmanagedCallersOnly]
    public static int InitializeMod()
    {
        DebugConsole.Show();

        // Gather environment info
        Assembly currentAssembly = Assembly.GetExecutingAssembly();
        ModName = currentAssembly.GetName().Name ?? nameof(RouteEdit);
        string? assemblyDirectory = Path.GetDirectoryName(currentAssembly.Location);
        HarmonyId = "Infixo." + ModName;
        Log.Write($"Mod {ModName} successfully started. HarmonyId is {HarmonyId}.");

        try
        {
            // Harmony
            var harmony = new Harmony(HarmonyId);
            //harmony.PatchAll(typeof(Mod).Assembly);
            harmony.PatchAll();
            var patchedMethods = harmony.GetPatchedMethods().ToArray();
            Log.Write($"Plugin {HarmonyId} made patches! Patched methods: " + patchedMethods.Length);
            foreach (var patchedMethod in patchedMethods)
                Log.Write($"Patched method: {patchedMethod.DeclaringType?.Name}.{patchedMethod.Name}");
        }
        catch (Exception ex)
        {
            Log.Write("EXCEPTION. ABORTING.");
            Log.Write(ex.ToString());
            return 1;
        }

        return 0;
    }
}
