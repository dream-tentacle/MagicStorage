using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using HarmonyLib;
using Verse;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: AssemblyTitle("MendingChangeDresserPatch")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("MendingChangeDresserPatch")]
[assembly: AssemblyCopyright("Copyright ©  2017")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("6613fcb9-676e-4b03-9338-46dd61167bf4")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.7.2", FrameworkDisplayName = "")]
[assembly: AssemblyVersion("1.0.0.0")]
namespace MendingChangeDresserPatch;

[StaticConstructorOnStartup]
internal class HarmonyPatches
{
	static HarmonyPatches()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		if (ModsConfig.ActiveModsInLoadOrder.Any((ModMetaData m) => "MendAndRecycle".Equals(m.Name)))
		{
			try
			{
				Harmony val = new Harmony("com.mendingchangedresserpatch.rimworld.mod");
				val.PatchAll(Assembly.GetExecutingAssembly());
				Log.Message("MendingChangeDresserPatch Harmony Patches:" + Environment.NewLine + "  Postfix:" + Environment.NewLine + "    WorkGiver_DoBill.TryFindBestBillIngredients - Priority Last");
				return;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to patch Mending & Recycling." + Environment.NewLine + ex.Message);
				return;
			}
		}
		Log.Message("MendingChangeDresserPatch did not find MendAndRecycle. Will not load patch.");
	}
}
