using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace OTHRadio;

// Compiles against the real LinuxBridgeMedia/PcCommand/WineDetector sources.
internal static class Program
{
	private static int _fail;
	private static int _pass;

	private static long tickUs = 12000000;

	private static void Check(bool ok, string name)
	{
		if (ok)
		{
			_pass++;
			Console.WriteLine("  PASS  " + name);
		}
		else
		{
			_fail++;
			Console.WriteLine("  FAIL  " + name);
		}
	}

	private static void CheckEqual<T>(T expected, T actual, string name)
	{
		Check(Equals(expected, actual), name + $" (expected {Show(expected)}, got {Show(actual)})");
	}

	private static string Show(object o)
	{
		if (o is float f)
		{
			return f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
		}
		if (o is double d)
		{
			return d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
		}
		return "" + o;
	}

	private static int Main()
	{
		Console.WriteLine("== parse protocol (LinuxBridgeMedia.ParseNow) ==");
		var none = LinuxBridgeMedia.ParseNow("none\n");
		CheckEqual(false, none.HasSession, "none -> no session");
		var empty = LinuxBridgeMedia.ParseNow("");
		CheckEqual(false, empty.HasSession, "empty -> no session");
		CheckEqual(-1.0, empty.LengthSeconds, "empty -> len -1");

		var full = LinuxBridgeMedia.ParseNow("Playing\tSpotify\tArtist A\tSong 1\tAlbum X\t180000000\t12000000\t0.25\n");
		CheckEqual(true, full.HasSession, "full line -> session");
		CheckEqual(true, full.IsPlaying, "full line -> playing");
		CheckEqual("Spotify", full.App, "full line -> app");
		CheckEqual("Artist A", full.Artist, "full line -> artist");
		CheckEqual("Song 1", full.Title, "full line -> title");
		CheckEqual(180.0, full.LengthSeconds, "us length -> seconds");
		CheckEqual(12.0, full.PositionSeconds, "us position -> seconds");
		CheckEqual(0.25f, full.Volume, "volume parsed");

		var paused = LinuxBridgeMedia.ParseNow("Paused\tspotify\t\t\t\t\t\t\n");
		CheckEqual(true, paused.HasSession, "paused -> session");
		CheckEqual(false, paused.IsPlaying, "paused -> not playing");

		var stopped = LinuxBridgeMedia.ParseNow("Stopped\tfirefox\tArtist\tSong\t\t90000000\t0\t\n");
		CheckEqual(true, stopped.HasSession, "stopped -> session");
		CheckEqual(false, stopped.IsPlaying, "stopped -> not playing");
		CheckEqual(90.0, stopped.LengthSeconds, "stopped -> length");
		CheckEqual(-1f, stopped.Volume, "empty volume -> -1 (slider off)");

		var comma = LinuxBridgeMedia.ParseNow("Playing\tspotify\tA\tT\t\t300000000\t0\t0,5\n");
		CheckEqual(0.5f, comma.Volume, "comma decimal volume parsed");

		var weird = LinuxBridgeMedia.ParseNow("playing\tchromium\t\tSong\t\t\t\t2.5\n");
		CheckEqual(true, weird.IsPlaying, "lowercase status ok");
		CheckEqual(1f, weird.Volume, "volume clamped to 1");

		var stream = LinuxBridgeMedia.ParseNow("Playing\tmpv\t\tLive Radio Stream\t\t\t\t\n");
		CheckEqual(true, stream.HasSession, "no length -> session exists");
		CheckEqual(-1.0, stream.LengthSeconds, "unknown length -> -1");

		Console.WriteLine("== command + volume texts ==");
		CheckEqual("play", LinuxBridgeMedia.CommandText(PcCommand.Play), "Play -> play");
		CheckEqual("pause", LinuxBridgeMedia.CommandText(PcCommand.Pause), "Pause -> pause");
		CheckEqual("play-pause", LinuxBridgeMedia.CommandText(PcCommand.Toggle), "Toggle -> play-pause");
		CheckEqual("next", LinuxBridgeMedia.CommandText(PcCommand.Next), "Next -> next");
		CheckEqual("previous", LinuxBridgeMedia.CommandText(PcCommand.Previous), "Previous -> previous");
		CheckEqual("vol 0.3", LinuxBridgeMedia.VolumeCommand(0.3f), "volume 0.3");
		CheckEqual("vol 1", LinuxBridgeMedia.VolumeCommand(1.5f), "volume clamped up");
		CheckEqual("vol 0", LinuxBridgeMedia.VolumeCommand(-3f), "volume clamped down");
		Check(Regex.IsMatch(LinuxBridgeMedia.VolumeCommand(0.755f) ?? "", "^vol [0-9]+(\\.[0-9]+)?$"), "volume text matches script grammar");

		Console.WriteLine("== bridge folder resolution ==");
		string dir = LinuxBridgeMedia.ResolveDir("Z:\\home\\me\\.cache\\othradio", out var why);
		CheckEqual("Z:\\home\\me\\.cache\\othradio", dir, "configured folder wins");
		string oldHome = Environment.GetEnvironmentVariable("HOME");
		try
		{
			Environment.SetEnvironmentVariable("HOME", "/home/you");
			CheckEqual("Z:\\home\\you\\.cache\\othradio", LinuxBridgeMedia.ResolveDir(null, out _), "HOME -> Z: path");
			Environment.SetEnvironmentVariable("HOME", "");
			string noHome = LinuxBridgeMedia.ResolveDir(null, out why);
			Check(noHome == null && why.Length > 0, "missing HOME -> null + reason");
		}
		finally
		{
			Environment.SetEnvironmentVariable("HOME", oldHome);
		}

		Console.WriteLine("== live file-protocol round trip (real thread, real files) ==");
		string tmp = Path.Combine(Path.GetTempPath(), "othradio_test_" + Guid.NewGuid().ToString("N"));
		var mb = new LinuxBridgeMedia(tmp);
		mb.Start();
		Check(mb.Available, "Start() with configured folder -> Available");
		Check(mb.BridgeFolder.Equals(tmp, StringComparison.OrdinalIgnoreCase), "BridgeFolder echoed");

		CheckEqual(-1.0, mb.Progress().len, "no now.txt yet -> progress -1,-1");
		File.WriteAllText(Path.Combine(tmp, "now.txt"), "Playing\tSpotify\tPink Floyd\tBreathe\tDSOTM\t372000000\t12000000\t0.4\n");
		WaitUntil(() => "Breathe".Equals(mb.Title, StringComparison.Ordinal), 6000, "poll picks up now.txt");
		CheckEqual(true, mb.HasSession, "session true");
		CheckEqual("Spotify", mb.App, "app");
		var p1 = mb.Progress();
		Check(p1.len == 372.0, "progress length 372s");
		double basePos = p1.pos;
		Check(basePos > 11.0 && basePos < 13.5, "position ~12s, got " + Show(basePos));
		// like the live bridge: the host script advances now.txt's position every half second
		using var ticker = new Timer(delegate
		{
			try
			{
				Interlocked.Add(ref tickUs, 500000);
				File.WriteAllText(Path.Combine(tmp, "now.txt"), "Playing\tSpotify\tPink Floyd\tBreathe\tDSOTM\t372000000\t" + tickUs + "\t0.4\n");
			}
			catch
			{
			}
		});
		ticker.Change(200, 300);
		Thread.Sleep(1200);
		ticker.Change(-1, -1);
		var p2 = mb.Progress();
		Check(p2.pos > p1.pos + 1.0 && p2.pos < p1.pos + 3.5, "position advances with live script, " + Show(p1.pos) + " -> " + Show(p2.pos));

		File.WriteAllText(Path.Combine(tmp, "now.txt"), "Paused\tspotify\t\t\t\t\t\t\n");
		WaitUntil(() => !mb.IsPlaying, 6000, "paused state picked up");
		CheckEqual(-1.0, mb.Progress().len, "paused, no length -> progress -1,-1");
		CheckEqual(-1f, mb.Volume, "paused line has no volume -> slider off");

		mb.Send(PcCommand.Toggle);
		mb.Send(PcCommand.Next);
		mb.SetVolume(0.3f);
		WaitCmd(tmp, "play-pause\nnext\nvol 0.3\n");

		mb.Send(PcCommand.Previous);
		WaitCmd(tmp, "previous\n");

		mb.Stop();

		Console.WriteLine("== WineDetector ==");
		bool wine = WineDetector.IsRunningUnderWine();
		Check(WineDetector.IsRunningUnderWine() == wine, "detection is cached and repeatable");
		Console.WriteLine("  (informational) wine_get_version found on ntdll: " + wine + (Environment.OSVersion.Platform == PlatformID.Win32NT && !wine ? " - expected false on bare Windows" : ""));

		Console.WriteLine("");
		Console.WriteLine(_fail == 0 ? $"ALL GREEN: {_pass} passed, 0 failed" : $"FAILURES: {_fail} failed, {_pass} passed");
		return _fail == 0 ? 0 : 1;
	}

	private static void WaitUntil(Func<bool> cond, int timeoutMs, string name)
	{
		int waited = 0;
		while (waited < timeoutMs)
		{
			try
			{
				if (cond())
				{
					Check(true, name);
					return;
				}
			}
			catch
			{
			}
			Thread.Sleep(200);
			waited += 200;
		}
		Check(false, name + " (timed out after " + timeoutMs + "ms)");
	}

	private static void WaitCmd(string dir, string expected)
	{
		int waited = 0;
		string got = "";
		while (waited < 6000)
		{
			got = CmdFile(dir);
			if (got.Replace("\r", "").Contains(expected))
			{
				break;
			}
			Thread.Sleep(200);
			waited += 200;
		}
		Check(got.Replace("\r", "").Contains(expected), "cmd file got [" + got.Replace("\n", " ") + "] contains [" + expected.Replace("\n", " ") + "]");
	}

	private static string CmdFile(string dir)
	{
		string p = Path.Combine(dir, "cmd");
		return File.Exists(p) ? File.ReadAllText(p) : "";
	}
}
