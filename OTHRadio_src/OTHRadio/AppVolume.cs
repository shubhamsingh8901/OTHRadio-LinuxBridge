using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace OTHRadio;

internal static class AppVolume
{
	[ComImport]
	[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IMMDeviceEnumerator
	{
		[PreserveSig]
		int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);

		[PreserveSig]
		int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
	}

	[ComImport]
	[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IMMDeviceCollection
	{
		[PreserveSig]
		int GetCount(out uint count);

		[PreserveSig]
		int Item(uint index, out IMMDevice device);
	}

	[ComImport]
	[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IMMDevice
	{
		[PreserveSig]
		int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
	}

	[ComImport]
	[Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioSessionManager2
	{
		int NotImpl1();

		int NotImpl2();

		[PreserveSig]
		int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
	}

	[ComImport]
	[Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioSessionEnumerator
	{
		[PreserveSig]
		int GetCount(out int count);

		[PreserveSig]
		int GetSession(int index, out IAudioSessionControl session);
	}

	[ComImport]
	[Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioSessionControl
	{
	}

	[ComImport]
	[Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioSessionControl2
	{
		int NotImpl0();

		int NotImpl1();

		int NotImpl2();

		int NotImpl3();

		int NotImpl4();

		int NotImpl5();

		int NotImpl6();

		int NotImpl7();

		int NotImpl8();

		int NotImpl9();

		int NotImpl10();

		[PreserveSig]
		int GetProcessId(out uint pid);
	}

	[ComImport]
	[Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioMeterInformation
	{
		[PreserveSig]
		int GetPeakValue(out float peak);
	}

	[ComImport]
	[Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface ISimpleAudioVolume
	{
		[PreserveSig]
		int SetMasterVolume(float level, ref Guid eventContext);

		[PreserveSig]
		int GetMasterVolume(out float level);
	}

	private static readonly object _lock = new object();

	private static readonly AutoResetEvent _wake = new AutoResetEvent(initialState: false);

	private static readonly Dictionary<uint, string> _names = new Dictionary<uint, string>();

	private static Thread _thread;

	private static string _watch;

	private static string _lastAuto;

	private static float _volume = -1f;

	private static float _pendingSet = -1f;

	private static DateTime _watchedAt;

	public static float Get(string processName)
	{
		if (string.IsNullOrEmpty(processName))
		{
			return -1f;
		}
		lock (_lock)
		{
			if (!string.Equals(_watch, processName, StringComparison.OrdinalIgnoreCase))
			{
				_watch = processName;
				_volume = -1f;
				_wake.Set();
			}
			_watchedAt = DateTime.UtcNow;
			Start();
			return _volume;
		}
	}

	public static bool Set(string processName, float volume)
	{
		if (string.IsNullOrEmpty(processName))
		{
			return false;
		}
		lock (_lock)
		{
			_watch = processName;
			_watchedAt = DateTime.UtcNow;
			_pendingSet = (_volume = Math.Clamp(volume, 0f, 1f));
			Start();
		}
		_wake.Set();
		return true;
	}

	private static void Start()
	{
		if (_thread == null)
		{
			_thread = new Thread(Worker)
			{
				IsBackground = true,
				Name = "OTHRadio AppVolume",
				Priority = ThreadPriority.BelowNormal
			};
			_thread.Start();
		}
	}

	private static void Worker()
	{
		List<IAudioSessionManager2> list = null;
		DateTime dateTime = DateTime.MinValue;
		while (true)
		{
			_wake.WaitOne(1000);
			string watch;
			float pendingSet;
			lock (_lock)
			{
				if (string.IsNullOrEmpty(_watch) || (DateTime.UtcNow - _watchedAt).TotalSeconds > 3.0)
				{
					continue;
				}
				watch = _watch;
				pendingSet = _pendingSet;
				_pendingSet = -1f;
				goto IL_0081;
			}
			IL_0081:
			try
			{
				if (list == null || (DateTime.UtcNow - dateTime).TotalSeconds > 10.0)
				{
					Release(list);
					list = Managers();
					dateTime = DateTime.UtcNow;
				}
				float volume = Find(list, watch, (pendingSet >= 0f) ? new float?(pendingSet) : ((float?)null));
				lock (_lock)
				{
					if (_pendingSet < 0f && string.Equals(_watch, watch, StringComparison.OrdinalIgnoreCase))
					{
						_volume = volume;
					}
				}
			}
			catch
			{
				Release(list);
				list = null;
			}
			Thread.Sleep(100);
		}
	}

	private static List<IAudioSessionManager2> Managers()
	{
		List<IAudioSessionManager2> list = new List<IAudioSessionManager2>();
		IMMDeviceEnumerator iMMDeviceEnumerator = (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
		try
		{
			if (iMMDeviceEnumerator.EnumAudioEndpoints(0, 1, out var devices) != 0)
			{
				return list;
			}
			try
			{
				devices.GetCount(out var count);
				Guid iid = typeof(IAudioSessionManager2).GUID;
				for (uint num = 0u; num < count; num++)
				{
					if (devices.Item(num, out var device) != 0)
					{
						continue;
					}
					try
					{
						if (device.Activate(ref iid, 23, IntPtr.Zero, out var iface) == 0)
						{
							list.Add((IAudioSessionManager2)iface);
						}
					}
					finally
					{
						Marshal.ReleaseComObject(device);
					}
				}
				return list;
			}
			finally
			{
				Marshal.ReleaseComObject(devices);
			}
		}
		finally
		{
			Marshal.ReleaseComObject(iMMDeviceEnumerator);
		}
	}

	private static void Release(List<IAudioSessionManager2> mgrs)
	{
		if (mgrs == null)
		{
			return;
		}
		foreach (IAudioSessionManager2 mgr in mgrs)
		{
			try
			{
				Marshal.ReleaseComObject(mgr);
			}
			catch
			{
			}
		}
	}

	private static float Find(List<IAudioSessionManager2> mgrs, string processName, float? write)
	{
		List<(IAudioSessionControl, string, float)> list = new List<(IAudioSessionControl, string, float)>();
		try
		{
			uint processId = (uint)Environment.ProcessId;
			foreach (IAudioSessionManager2 mgr in mgrs)
			{
				if (mgr.GetSessionEnumerator(out var sessionEnum) != 0)
				{
					throw new InvalidOperationException();
				}
				try
				{
					sessionEnum.GetCount(out var count);
					for (int i = 0; i < count; i++)
					{
						sessionEnum.GetSession(i, out var session);
						((IAudioSessionControl2)session).GetProcessId(out var pid);
						string text = ((pid == 0 || pid == processId) ? "" : NameOf(pid));
						if (text.Length == 0)
						{
							Marshal.ReleaseComObject(session);
							continue;
						}
						float peak = 0f;
						try
						{
							((IAudioMeterInformation)session).GetPeakValue(out peak);
						}
						catch
						{
						}
						list.Add((session, text, peak));
					}
				}
				finally
				{
					Marshal.ReleaseComObject(sessionEnum);
				}
			}
			string text2 = processName;
			if (text2 == "*")
			{
				text2 = null;
				float num = 0.0005f;
				foreach (var item in list)
				{
					if (item.Item3 > num && !IsMirror(item.Item2))
					{
						num = item.Item3;
						text2 = item.Item2;
					}
				}
				if (text2 == null)
				{
					text2 = _lastAuto;
				}
				if (text2 == null)
				{
					return -1f;
				}
				_lastAuto = text2;
			}
			float level = -1f;
			float num2 = -1f;
			foreach (var item2 in list)
			{
				if (string.Equals(item2.Item2, text2, StringComparison.OrdinalIgnoreCase))
				{
					ISimpleAudioVolume simpleAudioVolume = (ISimpleAudioVolume)item2.Item1;
					Guid eventContext = Guid.Empty;
					if (write.HasValue)
					{
						simpleAudioVolume.SetMasterVolume(write.Value, ref eventContext);
						level = write.Value;
					}
					else if (item2.Item3 > num2)
					{
						num2 = item2.Item3;
						simpleAudioVolume.GetMasterVolume(out level);
					}
				}
			}
			return level;
		}
		finally
		{
			foreach (var item3 in list)
			{
				Marshal.ReleaseComObject(item3.Item1);
			}
		}
	}

	private static bool IsMirror(string proc)
	{
		if (!proc.Equals("audiodg", StringComparison.OrdinalIgnoreCase) && !proc.StartsWith("SteelSeries", StringComparison.OrdinalIgnoreCase) && !proc.StartsWith("nvcontainer", StringComparison.OrdinalIgnoreCase) && !proc.StartsWith("NVIDIA", StringComparison.OrdinalIgnoreCase) && !proc.StartsWith("obs", StringComparison.OrdinalIgnoreCase))
		{
			return proc.StartsWith("voicemeeter", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static string NameOf(uint pid)
	{
		if (!_names.TryGetValue(pid, out var value))
		{
			try
			{
				using Process process = Process.GetProcessById((int)pid);
				value = process.ProcessName;
			}
			catch
			{
				value = "";
			}
			if (_names.Count > 256)
			{
				_names.Clear();
			}
			_names[pid] = value;
		}
		return value;
	}

	public static string ProcessFor(string app)
	{
		switch (app)
		{
		case "Spotify":
			return "Spotify";
		case "Chrome":
			return "chrome";
		case "Edge":
			return "msedge";
		case "Firefox":
			return "firefox";
		case "Opera":
			return "opera";
		case "Media Player":
			return "Microsoft.Media.Player";
		case "":
		case null:
			return null;
		default:
			return "*";
		}
	}
}
