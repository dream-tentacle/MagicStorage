using System;
using System.Linq;
using MagicStorage;
using Verse;

internal static class UpdateNoticeTests
{
    private static int checks;

    private static void Equal<T>(T expected, T actual, string message)
    {
        checks++;
        if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual);
    }

    public static int Main()
    {
        try
        {
            Run();
            Console.WriteLine("Update notice: " + checks + " checks passed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Run()
    {
        var unrelated = new ModContentPack();
        unrelated.ModMetaData.ModVersion = "9.9.9";
        LoadedModManager.RunningModsListForReading.Add(unrelated);
        var own = new ModContentPack();
        own.ModMetaData.ModVersion = "1.0.10";
        own.assemblies.loadedAssemblies.Add(typeof(UpdateNoticeUtility).Assembly);
        LoadedModManager.RunningModsListForReading.Add(own);

        var current = new UpdateNoticeDef { version = "1.0.10" };
        var older = new UpdateNoticeDef { version = "1.0.9" };
        var future = new UpdateNoticeDef { version = "1.0.11" };
        DefDatabase<UpdateNoticeDef>.AllDefsListForReading.AddRange(new[] { older, future, current });
        Equal(own, UpdateNoticeUtility.OwnMod, "metadata belongs to this assembly's mod");
        Equal(1, UpdateNoticeUtility.PendingNotices(null).Count, "first use shows only the current release");
        Equal(current, UpdateNoticeUtility.PendingNotices("1.0.9").Single(), "upgrade shows the installed release");
        Equal(current, UpdateNoticeUtility.PendingNotices("1.0.11").Single(), "downgrade shows the installed release");
        Equal(0, UpdateNoticeUtility.PendingNotices(" 1.0.10 ").Count, "unchanged version does not repeat");
        var history = UpdateNoticeUtility.AllNotices();
        Equal(2, history.Count, "history excludes future releases");
        Equal(current, history[0], "history sorts numeric versions newest first");
        Equal(older, history[1], "1.0.9 precedes 1.0.10 numerically");

        UpdateNoticeSettings settings = UpdateNoticeSettings.Current;
        var component = new GameComponent_UpdateNotice(new Game());
        component.StartedNewGame();
        Equal(0, LongEventHandler.Pending.Count, "new games do not enqueue introductory popups");
        component.LoadedGame();
        Equal(0, settings.Saves, "record I/O waits until save loading finishes");
        Equal(0, Dialog_UpdateNotice.Opened.Count, "popup waits until save loading finishes");
        LongEventHandler.FinishLoading();
        Equal(1, settings.Saves, "current version is persisted once");
        Equal("1.0.10", settings.LastSeenVersion, "persisted version follows About metadata");
        Equal(current, Dialog_UpdateNotice.Opened.Single().Single(), "automatic popup shows current release only");

        new GameComponent_UpdateNotice(new Game()).LoadedGame();
        LongEventHandler.FinishLoading();
        Equal(1, settings.Saves, "loading another save does not rewrite an unchanged version");
        Equal(1, Dialog_UpdateNotice.Opened.Count, "loading another save does not repeat the popup");

        settings.neverShowUpdateLog = true;
        settings.LastSeenVersion = "1.0.9";
        component.LoadedGame();
        LongEventHandler.FinishLoading();
        Equal("1.0.9", settings.LastSeenVersion, "disabled automatic display leaves the seen version alone");
        Equal(1, Dialog_UpdateNotice.Opened.Count, "disabled automatic display suppresses popups");
        Equal(2, UpdateNoticeUtility.AllNotices().Count, "history remains available while automatic display is off");

        settings.neverShowUpdateLog = false;
        component.LoadedGame();
        LongEventHandler.FinishLoading();
        Equal(2, Dialog_UpdateNotice.Opened.Count, "reenabling automatic display shows the unseen release");

        settings.LastSeenVersion = "1.0.11";
        component.LoadedGame();
        LongEventHandler.FinishLoading();
        Equal(3, Dialog_UpdateNotice.Opened.Count, "downgrading also triggers a release log");

        DefDatabase<UpdateNoticeDef>.AllDefsListForReading.Remove(current);
        settings.LastSeenVersion = "1.0.9";
        component.LoadedGame();
        LongEventHandler.FinishLoading();
        Equal("1.0.10", settings.LastSeenVersion, "releases without a log are still recorded");
        Equal(3, Dialog_UpdateNotice.Opened.Count, "missing log does not open an empty window");
    }
}
