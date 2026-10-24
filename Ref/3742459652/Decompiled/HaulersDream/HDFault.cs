using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using HaulersDream.Core;
using Verse;

namespace HaulersDream;

public static class HDFault
{
	private const int MaxChainDepth = 8;

	private const string MonoFramePrefix = "\n  at ";

	private const string NetFramePrefix = "\n   at ";

	private static Assembly CoreAssembly => typeof(BlamePolicy).Assembly;

	public static bool InvolvesHaulersDream(Exception ex)
	{
		if (ex == null)
		{
			return false;
		}
		List<MethodBase> list = ResolvedMethods(ex);
		for (int i = 0; i < list.Count; i++)
		{
			if (IsHaulersDream(list[i].DeclaringType?.Assembly))
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsHaulersDream(Assembly asm)
	{
		if (asm != null)
		{
			if (!(asm == typeof(HDFault).Assembly))
			{
				return asm == CoreAssembly;
			}
			return true;
		}
		return false;
	}

	public static bool IsModOwned(Assembly asm)
	{
		if (asm != null && asm != typeof(Pawn).Assembly && !IsHarmony(asm))
		{
			return OwningModName(asm) != null;
		}
		return false;
	}

	private static bool IsHarmony(Assembly asm)
	{
		if (asm == null)
		{
			return false;
		}
		if (asm == typeof(Harmony).Assembly)
		{
			return true;
		}
		string text = SafeAssemblyName(asm);
		if (!(text == "0Harmony"))
		{
			return text == "HarmonyMod";
		}
		return true;
	}

	public static bool OriginUnknown(Exception ex)
	{
		if (ex != null)
		{
			return ResolvedMethods(ex).Count == 0;
		}
		return true;
	}

	public static string ExceptionTypeName(Exception ex)
	{
		if (ex == null)
		{
			return "none";
		}
		try
		{
			return ex.GetType().Name;
		}
		catch
		{
			return "unknown";
		}
	}

	public static string DescribeOrigin(Exception ex)
	{
		if (ex == null)
		{
			return null;
		}
		List<MethodBase> list = ResolvedMethods(ex);
		for (int i = 0; i < list.Count; i++)
		{
			Type declaringType = list[i].DeclaringType;
			Assembly assembly = declaringType?.Assembly;
			if (!(assembly == null) && !IsHaulersDream(assembly) && IsModOwned(assembly))
			{
				return OwningModName(assembly) + " (" + declaringType.FullName + "." + list[i].Name + ")";
			}
		}
		return null;
	}

	public static string DescribeOwnFrame(Exception ex)
	{
		if (ex == null)
		{
			return null;
		}
		List<MethodBase> list = ResolvedMethods(ex);
		for (int i = 0; i < list.Count; i++)
		{
			Type declaringType = list[i].DeclaringType;
			if (declaringType != null && IsHaulersDream(declaringType.Assembly))
			{
				return declaringType.FullName + "." + list[i].Name;
			}
		}
		return null;
	}

	public static string DescribeInnermostFrame(Exception ex)
	{
		if (ex == null)
		{
			return null;
		}
		List<MethodBase> list = ResolvedMethods(ex);
		for (int i = 0; i < list.Count; i++)
		{
			Type declaringType = list[i].DeclaringType;
			if (declaringType != null)
			{
				return declaringType.FullName + "." + list[i].Name;
			}
		}
		return null;
	}

	public static string OwningModName(Assembly asm)
	{
		if (asm == null)
		{
			return null;
		}
		try
		{
			List<ModContentPack> runningModsListForReading = LoadedModManager.RunningModsListForReading;
			if (runningModsListForReading == null)
			{
				return null;
			}
			for (int i = 0; i < runningModsListForReading.Count; i++)
			{
				List<Assembly> list = runningModsListForReading[i]?.assemblies?.loadedAssemblies;
				if (list == null)
				{
					continue;
				}
				for (int j = 0; j < list.Count; j++)
				{
					if (list[j] == asm)
					{
						return runningModsListForReading[i].Name;
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static Type FirstFrameTypeAssignableTo(Exception ex, Type baseType)
	{
		if (ex == null || baseType == null)
		{
			return null;
		}
		List<MethodBase> list = ResolvedMethods(ex);
		for (int i = 0; i < list.Count; i++)
		{
			Type declaringType = list[i].DeclaringType;
			if (declaringType != null && baseType.IsAssignableFrom(declaringType))
			{
				return declaringType;
			}
		}
		return null;
	}

	public static string Render(Exception ex)
	{
		if (ex == null)
		{
			return "<no exception>";
		}
		string text = SafeToString(ex);
		if (text != null && (text.IndexOf("\n  at ", StringComparison.Ordinal) >= 0 || text.IndexOf("\n   at ", StringComparison.Ordinal) >= 0))
		{
			return text;
		}
		StringBuilder stringBuilder = new StringBuilder();
		RenderRebuilt(stringBuilder, ex, 0);
		return stringBuilder.ToString();
	}

	private static void RenderRebuilt(StringBuilder sb, Exception ex, int depth)
	{
		if (ex == null || depth > 8)
		{
			return;
		}
		sb.Append(SafeTypeName(ex)).Append(": ").Append(SafeMessage(ex));
		StackFrame[] array = FramesOf(ex);
		if (array != null)
		{
			for (int i = 0; i < array.Length; i++)
			{
				MethodBase methodBase = MethodOf(array[i]);
				if (methodBase == null)
				{
					sb.Append("\n  at ").Append("(wrapper dynamic-method) <unresolvable frame, typically a Harmony-patched method>");
				}
				else
				{
					sb.Append("\n  at ").Append(methodBase.DeclaringType?.FullName ?? "<unknown type>").Append('.')
						.Append(methodBase.Name)
						.Append(" ()");
				}
			}
		}
		Exception ex2 = InnerOf(ex);
		if (ex2 != null && ex2 != ex)
		{
			sb.Append("\n ---> inner exception: ");
			RenderRebuilt(sb, ex2, depth + 1);
		}
	}

	private static List<MethodBase> ResolvedMethods(Exception ex)
	{
		List<MethodBase> list = new List<MethodBase>();
		Collect(ex, list, 0);
		return list;
	}

	private static void Collect(Exception ex, List<MethodBase> into, int depth)
	{
		if (ex == null || depth > 8)
		{
			return;
		}
		Exception ex2 = InnerOf(ex);
		if (ex2 != null && ex2 != ex)
		{
			Collect(ex2, into, depth + 1);
		}
		StackFrame[] array = FramesOf(ex);
		if (array == null)
		{
			return;
		}
		for (int i = 0; i < array.Length; i++)
		{
			MethodBase methodBase = MethodOf(array[i]);
			if (methodBase != null)
			{
				into.Add(methodBase);
			}
		}
	}

	private static StackFrame[] FramesOf(Exception ex)
	{
		try
		{
			return new StackTrace(ex, fNeedFileInfo: false).GetFrames();
		}
		catch
		{
			return null;
		}
	}

	private static MethodBase MethodOf(StackFrame frame)
	{
		try
		{
			return frame?.GetMethod();
		}
		catch
		{
			return null;
		}
	}

	private static Exception InnerOf(Exception ex)
	{
		try
		{
			return ex.InnerException;
		}
		catch
		{
			return null;
		}
	}

	private static string SafeToString(Exception ex)
	{
		try
		{
			return ex.ToString();
		}
		catch
		{
			return null;
		}
	}

	private static string SafeTypeName(Exception ex)
	{
		try
		{
			return ex.GetType().FullName;
		}
		catch
		{
			return "<unknown exception type>";
		}
	}

	internal static string SafeMessage(Exception ex)
	{
		if (ex == null)
		{
			return "<no exception>";
		}
		try
		{
			return ex.Message;
		}
		catch (Exception ex2)
		{
			return "<Message threw " + ex2.GetType().Name + ">";
		}
	}

	private static string SafeAssemblyName(Assembly asm)
	{
		try
		{
			return asm.GetName().Name;
		}
		catch
		{
			return "an unidentified assembly";
		}
	}
}
