using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using Verse;

namespace HaulersDream;

public static class HDDebugLog
{
	public const long ActiveCapBytes = 8388608L;

	public const int ReportTailBytes = 524288;

	private const int MaxPendingLines = 100000;

	private static readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();

	private static readonly AutoResetEvent signal = new AutoResetEvent(initialState: false);

	private static readonly object startLock = new object();

	private static readonly object flushLock = new object();

	private static volatile bool flushRequested;

	private static readonly ManualResetEventSlim flushCompleted = new ManualResetEventSlim(initialState: false);

	private static volatile bool disabled;

	private static volatile bool started;

	private static volatile bool stopRequested;

	private static Thread writerThread;

	private static string activePath;

	private static string prevPath;

	private static int pending;

	public static void ConfigureDirectory(string consoleLogPath)
	{
		lock (startLock)
		{
			if (!started && !disabled)
			{
				string text = null;
				if (!string.IsNullOrEmpty(consoleLogPath))
				{
					text = Path.GetDirectoryName(consoleLogPath);
				}
				if (string.IsNullOrEmpty(text))
				{
					text = GenFilePaths.SaveDataFolderPath;
				}
				activePath = Path.Combine(text, "HaulersDream-debug.log");
				prevPath = Path.Combine(text, "HaulersDream-debug.prev.log");
				try
				{
					Rotate();
				}
				catch (Exception ex)
				{
					Log.Warning("[Hauler's Dream] could not rotate the debug log at startup; this run's trace will be appended to the existing file instead of starting fresh. " + ex);
				}
				writerThread = new Thread(WriterLoop)
				{
					IsBackground = true,
					Name = "HaulersDream-DebugLog"
				};
				started = true;
				writerThread.Start();
			}
		}
	}

	public static void Enqueue(string line)
	{
		if (!disabled && line != null && Volatile.Read(ref pending) < 100000)
		{
			Interlocked.Increment(ref pending);
			queue.Enqueue(line);
			signal.Set();
		}
	}

	public static void FlushAndClose(int timeoutMs = 2000)
	{
		if (!started || disabled)
		{
			return;
		}
		stopRequested = true;
		signal.Set();
		try
		{
			writerThread?.Join(timeoutMs);
		}
		catch
		{
		}
	}

	public static void FlushBlocking(int timeoutMs = 2000)
	{
		if (!started || disabled)
		{
			return;
		}
		lock (flushLock)
		{
			flushCompleted.Reset();
			flushRequested = true;
			signal.Set();
			try
			{
				flushCompleted.Wait(timeoutMs);
			}
			catch
			{
			}
		}
	}

	public static string GetReportTail(int maxBytes)
	{
		if (string.IsNullOrEmpty(activePath))
		{
			return null;
		}
		byte[] array = ReadLastBytes(activePath, maxBytes);
		byte[] array2 = array;
		if (array.Length < maxBytes)
		{
			byte[] array3 = ReadLastBytes(prevPath, maxBytes - array.Length);
			if (array3.Length != 0)
			{
				array2 = new byte[array3.Length + array.Length];
				Buffer.BlockCopy(array3, 0, array2, 0, array3.Length);
				Buffer.BlockCopy(array, 0, array2, array3.Length, array.Length);
			}
		}
		if (array2.Length == 0)
		{
			return null;
		}
		string text = Encoding.UTF8.GetString(array2);
		if (array2.Length >= maxBytes)
		{
			int num = text.IndexOf('\n');
			if (num >= 0 && num + 1 < text.Length)
			{
				text = text.Substring(num + 1);
			}
		}
		text = text.Trim('\r', '\n');
		if (text.Length != 0)
		{
			return text;
		}
		return null;
	}

	private static byte[] ReadLastBytes(string path, int maxBytes)
	{
		if (maxBytes <= 0 || string.IsNullOrEmpty(path) || !File.Exists(path))
		{
			return Array.Empty<byte>();
		}
		using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		long length = fileStream.Length;
		int num = (int)Math.Min(length, maxBytes);
		if (num <= 0)
		{
			return Array.Empty<byte>();
		}
		fileStream.Seek(length - num, SeekOrigin.Begin);
		byte[] array = new byte[num];
		int i;
		int num2;
		for (i = 0; i < num; i += num2)
		{
			num2 = fileStream.Read(array, i, num - i);
			if (num2 <= 0)
			{
				break;
			}
		}
		if (i == num)
		{
			return array;
		}
		byte[] array2 = new byte[i];
		Buffer.BlockCopy(array, 0, array2, 0, i);
		return array2;
	}

	private static void WriterLoop()
	{
		FileStream fileStream = null;
		try
		{
			fileStream = OpenActive();
			long num = fileStream.Length;
			while (true)
			{
				signal.WaitOne(1000);
				bool flag = false;
				string result;
				while (queue.TryDequeue(out result))
				{
					Interlocked.Decrement(ref pending);
					byte[] bytes = Encoding.UTF8.GetBytes(result + "\n");
					fileStream.Write(bytes, 0, bytes.Length);
					num += bytes.Length;
					flag = true;
				}
				if (flag)
				{
					fileStream.Flush();
				}
				if (flushRequested)
				{
					flushRequested = false;
					flushCompleted.Set();
				}
				if (stopRequested)
				{
					break;
				}
				if (num >= 8388608)
				{
					fileStream.Dispose();
					fileStream = null;
					Rotate();
					fileStream = OpenActive();
					num = 0L;
				}
			}
			bool flag2 = false;
			string result2;
			while (queue.TryDequeue(out result2))
			{
				Interlocked.Decrement(ref pending);
				byte[] bytes2 = Encoding.UTF8.GetBytes(result2 + "\n");
				fileStream.Write(bytes2, 0, bytes2.Length);
				flag2 = true;
			}
			if (flag2)
			{
				fileStream.Flush();
			}
			flushRequested = false;
			flushCompleted.Set();
			fileStream.Dispose();
			fileStream = null;
		}
		catch (ThreadAbortException)
		{
			disabled = true;
		}
		catch (Exception ex2)
		{
			disabled = true;
			Log.Error("[Hauler's Dream] disk debug log writer stopped after an I/O error; disk logging is off for this session (in-game console logging is unaffected). " + ex2);
		}
	}

	private static FileStream OpenActive()
	{
		return new FileStream(activePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
	}

	private static void Rotate()
	{
		if (File.Exists(prevPath))
		{
			File.Delete(prevPath);
		}
		if (File.Exists(activePath))
		{
			File.Move(activePath, prevPath);
		}
	}
}
