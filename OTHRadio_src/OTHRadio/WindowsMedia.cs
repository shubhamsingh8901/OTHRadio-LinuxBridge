using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace OTHRadio;

// Windows-only backend: WinRT SMTC via combase.dll. Never touched when the
// Linux bridge is selected, so nothing throws under Wine/Proton.
internal sealed class WindowsMedia : IPcBackend
{
	private volatile string _title = "";

	private volatile string _artist = "";

	private volatile string _app = "";

	private volatile bool _hasSession;

	private volatile bool _isPlaying;

	private volatile bool _available = true;

	private volatile bool _fast = true;

	private volatile string _error = "";

	private double _length;

	private double _position;

	private long _positionStampUtcTicks;

	private readonly ConcurrentQueue<PcCommand> _commands = new ConcurrentQueue<PcCommand>();

	private Thread _thread;

	private volatile bool _run;

	private static Guid _IIDStatics = new Guid("2050C4EE-11A0-57DE-AED7-C97C70338245");

	private static Guid _IIDIAsyncInfo = new Guid("00000036-0000-0000-C000-000000000046");

	public string Title => _title;

	public string Artist => _artist;

	public string App => _app;

	public bool HasSession => _hasSession;

	public bool IsPlaying => _isPlaying;

	public bool Available => _available;

	public bool Fast
	{
		get
		{
			return _fast;
		}
		set
		{
			_fast = value;
		}
	}

	public string Error => _error;

	public string UnavailableHint => "Windows media controls aren't available on this PC";

	public string BridgeFolder => "";

	public float Volume => AppVolume.Get(AppVolume.ProcessFor(_app));

	public void SetVolume(float volume)
	{
		string proc = AppVolume.ProcessFor(_app);
		if (!string.IsNullOrEmpty(proc))
		{
			AppVolume.Set(proc, Math.Clamp(volume, 0f, 1f));
		}
	}

	public WindowsMedia()
	{
		_thread = null;
	}

	public (double pos, double len) Progress()
	{
		double length = _length;
		double num = _position;
		if (length <= 0.0)
		{
			return (pos: -1.0, len: -1.0);
		}
		if (_isPlaying)
		{
			num += (double)(DateTime.UtcNow.Ticks - Interlocked.Read(in _positionStampUtcTicks)) / 10000000.0;
		}
		return (pos: Math.Clamp(num, 0.0, length), len: length);
	}

	public void Start()
	{
		if (_thread == null)
		{
			_run = true;
			_thread = new Thread(Worker)
			{
				IsBackground = true,
				Name = "OTHRadio PC media"
			};
			_thread.Start();
		}
	}

	public void Stop()
	{
		_run = false;
	}

	public void Send(PcCommand c)
	{
		_commands.Enqueue(c);
	}

	private void Worker()
	{
		IntPtr intPtr = IntPtr.Zero;
		try
		{
			RoInitialize(1);
			intPtr = CreateManager();
			if (intPtr == IntPtr.Zero)
			{
				_available = false;
				return;
			}
			while (_run)
			{
				IntPtr result = IntPtr.Zero;
				try
				{
					Call(intPtr, 6, out result);
					if (result == IntPtr.Zero)
					{
						_hasSession = false;
						_isPlaying = false;
						_title = (_artist = (_app = ""));
					}
					else
					{
						_hasSession = true;
						PcCommand result2;
						while (_commands.TryDequeue(out result2))
						{
							Execute(result, result2);
						}
						Read(result);
					}
				}
				catch (Exception ex)
				{
					_error = ex.Message;
				}
				finally
				{
					Release(result);
				}
				Thread.Sleep(_fast ? 400 : 2000);
			}
		}
		catch (Exception ex2)
		{
			_error = ex2.Message;
			_available = false;
		}
		finally
		{
			Release(intPtr);
		}
	}

	private IntPtr CreateManager()
	{
		string cls = "Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager";
		if (WindowsCreateString(cls, cls.Length, out var hstring) != 0)
		{
			return IntPtr.Zero;
		}
		try
		{
			if (RoGetActivationFactory(hstring, ref _IIDStatics, out var factory) != 0)
			{
				return IntPtr.Zero;
			}
			try
			{
				Call(factory, 6, out var result);
				return Await(result);
			}
			finally
			{
				Release(factory);
			}
		}
		finally
		{
			WindowsDeleteString(hstring);
		}
	}

	private unsafe void Read(IntPtr session)
	{
		Call(session, 6, out var result);
		_app = AppName(TakeString(result));
		Call(session, 9, out var result2);
		if (result2 != IntPtr.Zero)
		{
			int num = default;
			((delegate* unmanaged<IntPtr, int*, int>)(void*)Slot(result2, 7))(result2, &num);
			_isPlaying = num == 4;
			Release(result2);
		}
		Call(session, 8, out var result3);
		if (result3 != IntPtr.Zero)
		{
			long num2 = default;
			((delegate* unmanaged<IntPtr, long*, int>)(void*)Slot(result3, 6))(result3, &num2);
			long num3 = default;
			((delegate* unmanaged<IntPtr, long*, int>)(void*)Slot(result3, 7))(result3, &num3);
			long num4 = default;
			((delegate* unmanaged<IntPtr, long*, int>)(void*)Slot(result3, 10))(result3, &num4);
			long fileTime = default;
			((delegate* unmanaged<IntPtr, long*, int>)(void*)Slot(result3, 11))(result3, &fileTime);
			Release(result3);
			_length = (double)(num3 - num2) / 10000000.0;
			_position = (double)(num4 - num2) / 10000000.0;
			try
			{
				_positionStampUtcTicks = DateTime.FromFileTimeUtc(fileTime).Ticks;
			}
			catch
			{
				_positionStampUtcTicks = DateTime.UtcNow.Ticks;
			}
		}
		Call(session, 7, out var result4);
		IntPtr intPtr = Await(result4);
		if (intPtr != IntPtr.Zero)
		{
			Call(intPtr, 6, out var result5);
			Call(intPtr, 9, out var result6);
			_title = TakeString(result5);
			_artist = TakeString(result6);
			Release(intPtr);
		}
	}

	private static string AppName(string aumid)
	{
		if (string.IsNullOrEmpty(aumid))
		{
			return "";
		}
		string text = aumid.ToLowerInvariant();
		if (text.Contains("spotify"))
		{
			return "Spotify";
		}
		if (text.Contains("chrome"))
		{
			return "Chrome";
		}
		if (text.Contains("msedge") || text.Contains("microsoftedge"))
		{
			return "Edge";
		}
		if (text.Contains("firefox"))
		{
			return "Firefox";
		}
		if (text.Contains("opera"))
		{
			return "Opera";
		}
		if (text.Contains("zunemusic"))
		{
			return "Media Player";
		}
		bool flag = true;
		for (int i = 0; i < aumid.Length; i++)
		{
			if (!char.IsDigit(aumid[i]))
			{
				flag = false;
				break;
			}
		}
		if (flag)
		{
			return "Browser";
		}
		string text2 = aumid;
		int num = text2.LastIndexOf('!');
		if (num >= 0)
		{
			text2 = text2.Substring(num + 1);
		}
		if (text2.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			text2 = text2.Substring(0, text2.Length - 4);
		}
		int num2 = text2.LastIndexOf('.');
		if (num2 >= 0 && num2 < text2.Length - 1)
		{
			text2 = text2.Substring(num2 + 1);
		}
		if (text2.Length > 0)
		{
			text2 = char.ToUpperInvariant(text2[0]) + text2.Substring(1);
		}
		return text2;
	}

	private void Execute(IntPtr session, PcCommand c)
	{
		Call(session, c switch
		{
			PcCommand.Play => 10,
			PcCommand.Pause => 11,
			PcCommand.Next => 16,
			PcCommand.Previous => 17,
			_ => 20,
		}, out var result);
		AwaitBool(result);
	}

	private unsafe static IntPtr Slot(IntPtr obj, int index)
	{
		return *(IntPtr*)((nint)(*(IntPtr*)(void*)obj) + (nint)index * (nint)sizeof(IntPtr));
	}

	private unsafe static void Call(IntPtr obj, int slot, out IntPtr result)
	{
		IntPtr zero = IntPtr.Zero;
		int num = ((delegate* unmanaged<IntPtr, IntPtr*, int>)(void*)Slot(obj, slot))(obj, &zero);
		result = ((num == 0) ? zero : IntPtr.Zero);
	}

	private unsafe static void Release(IntPtr obj)
	{
		if (obj != IntPtr.Zero)
		{
			((delegate* unmanaged<IntPtr, uint>)(void*)Slot(obj, 2))(obj);
		}
	}

	private static IntPtr Await(IntPtr op)
	{
		if (op == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}
		try
		{
			if (!WaitDone(op))
			{
				return IntPtr.Zero;
			}
			Call(op, 8, out var result);
			return result;
		}
		finally
		{
			Release(op);
		}
	}

	private unsafe static bool AwaitBool(IntPtr op)
	{
		if (op == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			if (!WaitDone(op))
			{
				return false;
			}
			byte b = 0;
			((delegate* unmanaged<IntPtr, byte*, int>)(void*)Slot(op, 8))(op, &b);
			return b != 0;
		}
		finally
		{
			Release(op);
		}
	}

	private unsafe static bool WaitDone(IntPtr op)
	{
		IntPtr zero = IntPtr.Zero;
		Guid g = _IIDIAsyncInfo;
		if (((delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)(void*)Slot(op, 0))(op, &g, &zero) != 0)
		{
			return false;
		}
		try
		{
			int num = default;
			for (int i = 0; i < 300; i++)
			{
				((delegate* unmanaged<IntPtr, int*, int>)(void*)Slot(zero, 7))(zero, &num);
				if (num == 1)
				{
					return true;
				}
				if (num > 1)
				{
					return false;
				}
				Thread.Sleep(5);
			}
			return false;
		}
		finally
		{
			Release(zero);
		}
	}

	private static string TakeString(IntPtr hstring)
	{
		if (hstring == IntPtr.Zero)
		{
			return "";
		}
		try
		{
			IntPtr intPtr = WindowsGetStringRawBuffer(hstring, out var length);
			return (intPtr == IntPtr.Zero) ? "" : Marshal.PtrToStringUni(intPtr, (int)length);
		}
		finally
		{
			WindowsDeleteString(hstring);
		}
	}

	[DllImport("combase.dll")]
	private static extern int RoInitialize(int initType);

	[DllImport("combase.dll")]
	private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

	[DllImport("combase.dll", CharSet = CharSet.Unicode)]
	private static extern int WindowsCreateString(string sourceString, int length, out IntPtr hstring);

	[DllImport("combase.dll")]
	private static extern int WindowsDeleteString(IntPtr hstring);

	[DllImport("combase.dll")]
	private static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);
}
