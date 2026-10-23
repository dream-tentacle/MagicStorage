// Engine boundaries: loaded metadata, Def database, deferred callbacks, UI and record I/O.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Verse
{
    public class Def
    {
        public virtual IEnumerable<string> ConfigErrors() { yield break; }
    }
    public class Game { }
    public abstract class GameComponent
    {
        public virtual void LoadedGame() { }
        public virtual void StartedNewGame() { }
    }
    public static class GenCollection
    {
        public static bool NullOrEmpty<T>(this List<T> list) => list == null || list.Count == 0;
        public static bool NullOrEmpty(this string text) => string.IsNullOrEmpty(text);
    }
    public sealed class ModMetaData { public string ModVersion; }
    public sealed class ModAssemblyHandler { public List<Assembly> loadedAssemblies = new List<Assembly>(); }
    public sealed class ModContentPack
    {
        public ModAssemblyHandler assemblies = new ModAssemblyHandler();
        public ModMetaData ModMetaData = new ModMetaData();
    }
    public static class LoadedModManager
    {
        public static List<ModContentPack> RunningModsListForReading = new List<ModContentPack>();
    }
    public static class DefDatabase<T>
    {
        public static List<T> AllDefsListForReading = new List<T>();
    }
    public static class LongEventHandler
    {
        public static readonly Queue<Action> Pending = new Queue<Action>();
        public static void ExecuteWhenFinished(Action action) => Pending.Enqueue(action);
        public static void FinishLoading()
        {
            while (Pending.Count > 0) Pending.Dequeue()();
        }
    }
}

namespace MagicStorage
{
    public sealed class UpdateNoticeSettings
    {
        public static UpdateNoticeSettings Current = new UpdateNoticeSettings();
        public bool neverShowUpdateLog;
        public string LastSeenVersion;
        public int Saves;
        public void Save() => Saves++;
    }
    internal static class Dialog_UpdateNotice
    {
        internal static readonly List<List<UpdateNoticeDef>> Opened = new List<List<UpdateNoticeDef>>();
        internal static void Open(List<UpdateNoticeDef> notices)
        {
            if (notices != null && notices.Count > 0) Opened.Add(notices);
        }
    }
}
