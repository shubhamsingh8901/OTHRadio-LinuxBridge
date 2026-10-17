using System;

namespace OTHRadio;

// Facade over the active PC-media backend. Every existing call site
// (Radio, RadioMenu, NowPlayingPanel) keeps working unchanged; the backend is
// chosen once at startup: LinuxBridgeMedia under Wine (or when UseLinuxBridge
// is forced), WindowsMedia otherwise. If the bridge can't set up its folder
// (missing HOME, unwritable path) the mod falls back to WindowsMedia and logs
// a warning.
internal static class PcMedia
{
	private static IPcBackend _backend;

	public static string Title => _backend?.Title ?? "";

	public static string Artist => _backend?.Artist ?? "";

	public static string App => _backend?.App ?? "";

	public static bool HasSession => _backend?.HasSession ?? false;

	public static bool IsPlaying => _backend?.IsPlaying ?? false;

	public static bool Available => _backend?.Available ?? true;

	public static bool Fast
	{
		get
		{
			return _backend?.Fast ?? true;
		}
		set
		{
			// set from Radio.OnUpdate every frame
			IPcBackend backend = _backend;
			if (backend != null)
			{
				backend.Fast = value;
			}
		}
	}

	public static string Error => _backend?.Error ?? "";

	public static string UnavailableHint => _backend?.UnavailableHint ?? "";

	public static string BridgeFolder => _backend?.BridgeFolder ?? "";

	public static bool IsBridge => _backend is LinuxBridgeMedia;

	public static float Volume => _backend?.Volume ?? -1f;

	public static void SetVolume(float v)
	{
		IPcBackend backend = _backend;
		if (backend != null)
		{
			backend.SetVolume(v);
		}
	}

	public static void Start()
	{
		if (_backend != null)
		{
			return;
		}
		bool forced = false;
		try
		{
			forced = Radio.UseLinuxBridge != null && Radio.UseLinuxBridge.Value;
		}
		catch
		{
		}
		if (forced || WineDetector.IsRunningUnderWine())
		{
			LinuxBridgeMedia linuxBridgeMedia = new LinuxBridgeMedia(BridgeFolderConfig());
			linuxBridgeMedia.Start();
			if (linuxBridgeMedia.Available)
			{
				_backend = linuxBridgeMedia;
				Radio.Log.Msg("PC station: Linux bridge folder " + linuxBridgeMedia.BridgeFolder);
				return;
			}
			Radio.Log.Warning("Linux bridge unavailable (" + linuxBridgeMedia.Error + ") - PC station falls back to Windows media controls");
		}
		backendWindows();
	}

	private static void backendWindows()
	{
		WindowsMedia windowsMedia = new WindowsMedia();
		windowsMedia.Start();
		_backend = windowsMedia;
		if (WineDetector.IsRunningUnderWine())
		{
			Radio.Log.Warning("Selected Windows media controls while running under Wine - GlobalSystemMediaTransportControlsSessionManager will likely fail (Error: " + windowsMedia.Error + ")");
		}
	}

	public static void Stop()
	{
		IPcBackend backend = _backend;
		_backend = null;
		if (backend != null)
		{
			backend.Stop();
		}
	}

	public static void Send(PcCommand c)
	{
		IPcBackend backend = _backend;
		if (backend != null)
		{
			backend.Send(c);
		}
	}

	public static (double pos, double len) Progress()
	{
		IPcBackend backend = _backend;
		if (backend != null)
		{
			return backend.Progress();
		}
		return (pos: -1.0, len: -1.0);
	}

	private static string BridgeFolderConfig()
	{
		try
		{
			return Radio.BridgeFolder?.Value;
		}
		catch
		{
			return null;
		}
	}
}
