using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading;

namespace OTHRadio;

// Talks to the host through a bridge folder shared by Proton's container
// (default ~/.cache/othradio on the Linux side). now.txt carries one
// tab-separated line: status, player, artist, title, album, length_us,
// position_us, [volume]. "none" means no player. Commands are appended to cmd.
internal sealed class LinuxBridgeMedia : IPcBackend
{
	private volatile string _title = "";

	private volatile string _artist = "";

	private volatile string _app = "";

	private volatile bool _hasSession;

	private volatile bool _isPlaying;

	private volatile bool _available = true;

	private volatile bool _fast = true;

	private volatile string _error = "";

	private volatile float _volume = -1f;

	private double _length;

	private double _position;

	private long _stampUtcTicks;

	private string _dir;

	private float _pendingVolume = -1f;

	private Thread _thread;

	private volatile bool _run;

	private readonly ConcurrentQueue<PcCommand> _commands = new ConcurrentQueue<PcCommand>();

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

	public string UnavailableHint => "Linux bridge offline - run the othradio-bridge.sh script and start a player";

	public string BridgeFolder => _dir ?? "";

	public float Volume => _volume;

	public LinuxBridgeMedia(string configuredFolder)
	{
		_conf = configuredFolder;
	}

	private readonly string _conf;

	public void Start()
	{
		if (_thread != null)
		{
			return;
		}
		_dir = ResolveDir(_conf, out var why);
		if (_dir == null)
		{
			_available = false;
			_error = why;
			return;
		}
		try
		{
			Directory.CreateDirectory(_dir);
		}
		catch (Exception ex)
		{
			_available = false;
			_error = "can't create bridge folder " + _dir + ": " + ex.Message;
			return;
		}
		_run = true;
		_thread = new Thread(Worker)
		{
			IsBackground = true,
			Name = "OTHRadio Linux bridge"
		};
		_thread.Start();
	}

	public void Stop()
	{
		_run = false;
	}

	public void Send(PcCommand c)
	{
		if (_dir != null)
		{
			_commands.Enqueue(c);
		}
	}

	public void SetVolume(float volume)
	{
		float v = Math.Clamp(volume, 0f, 1f);
		Interlocked.Exchange(ref _pendingVolume, v);
		_commands.Enqueue(PcCommand.Volume);
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
			num += (double)(DateTime.UtcNow.Ticks - Interlocked.Read(ref _stampUtcTicks)) / 10000000.0;
		}
		return (pos: Math.Clamp(num, 0.0, length), len: length);
	}

	private void Worker()
	{
		while (_run)
		{
			try
			{
				Poll();
				DrainCommands();
			}
			catch (Exception ex)
			{
				_error = ex.Message;
			}
			Thread.Sleep(_fast ? 400 : 1600);
		}
	}

	private void Poll()
	{
		string path = Path.Combine(_dir, "now.txt");
		string raw;
		try
		{
			raw = File.Exists(path) ? File.ReadAllText(path) : "";
		}
		catch (IOException)
		{
			return;
		}
		Apply(ParseNow(raw));
	}

	private void Apply(ParseOutcome n)
	{
		_hasSession = n.HasSession;
		_isPlaying = n.IsPlaying;
		_app = n.App;
		_artist = n.Artist;
		_title = n.Title;
		_length = n.LengthSeconds;
		_position = n.PositionSeconds;
		_volume = n.Volume;
		Interlocked.Exchange(ref _stampUtcTicks, DateTime.UtcNow.Ticks);
	}

	private void DrainCommands()
	{
		while (_commands.TryDequeue(out PcCommand c))
		{
			if (c == PcCommand.Volume)
			{
				float pending = Interlocked.Exchange(ref _pendingVolume, -1f);
				if (pending >= 0f)
				{
					WriteCommand(VolumeCommand(pending));
				}
				continue;
			}
			WriteCommand(CommandText(c));
		}
	}

	private void WriteCommand(string line)
	{
		if (string.IsNullOrEmpty(_dir) || string.IsNullOrEmpty(line))
		{
			return;
		}
		string path = Path.Combine(_dir, "cmd");
		for (int i = 0; i < 5; i++)
		{
			try
			{
				File.AppendAllText(path, line + "\n");
				return;
			}
			catch (IOException)
			{
				Thread.Sleep(50);
			}
			catch (UnauthorizedAccessException)
			{
				Thread.Sleep(50);
			}
		}
	}

	internal readonly struct ParseOutcome
	{
		public readonly bool HasSession;

		public readonly bool IsPlaying;

		public readonly string App;

		public readonly string Artist;

		public readonly string Title;

		public readonly double LengthSeconds;

		public readonly double PositionSeconds;

		public readonly float Volume;

		public ParseOutcome(bool hasSession, bool isPlaying, string app, string artist, string title, double lengthSeconds, double positionSeconds, float volume)
		{
			HasSession = hasSession;
			IsPlaying = isPlaying;
			App = app;
			Artist = artist;
			Title = title;
			LengthSeconds = lengthSeconds;
			PositionSeconds = positionSeconds;
			Volume = volume;
		}
	}

	internal static ParseOutcome ParseNow(string raw)
	{
		if (raw != null)
		{
			raw = raw.TrimEnd('\r', '\n');
		}
		if (string.IsNullOrWhiteSpace(raw) || raw.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
		{
			return new ParseOutcome(false, false, "", "", "", -1.0, -1.0, -1f);
		}
		string[] f = raw.Split('\t');
		bool playing = f[0].Trim().Equals("Playing", StringComparison.OrdinalIgnoreCase);
		string app = Prettify(At(f, 1));
		string artist = At(f, 2).Trim();
		string title = At(f, 3).Trim();
		double length = Micros(At(f, 5));
		double position = Micros(At(f, 6));
		float volume = Vol(At(f, 7));
		return new ParseOutcome(true, playing, app, artist, title, length, position, volume);
	}

	private static string At(string[] f, int i)
	{
		if (f == null || i >= f.Length)
		{
			return "";
		}
		return f[i] ?? "";
	}

	private static double Micros(string us)
	{
		if (!long.TryParse(us.Trim().TrimEnd('u'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) || v <= 0)
		{
			return -1.0;
		}
		return (double)v / 1000000.0;
	}

	private static float Vol(string v)
	{
		if (string.IsNullOrWhiteSpace(v))
		{
			return -1f;
		}
		if (!float.TryParse(v.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
		{
			return -1f;
		}
		return Math.Clamp(f, 0f, 1f);
	}

	private static string Prettify(string player)
	{
		string text = (player ?? "").Trim();
		if (text.Length == 0)
		{
			return "";
		}
		return char.ToUpperInvariant(text[0]) + text.Substring(1);
	}

	internal static string CommandText(PcCommand c)
	{
		return c switch
		{
			PcCommand.Play => "play",
			PcCommand.Pause => "pause",
			PcCommand.Toggle => "play-pause",
			PcCommand.Next => "next",
			PcCommand.Previous => "previous",
			_ => "",
		};
	}

	internal static string VolumeCommand(float v)
	{
		if (float.IsNaN(v) || float.IsInfinity(v))
		{
			return null;
		}
		return "vol " + Math.Clamp(v, 0f, 1f).ToString("0.##", CultureInfo.InvariantCulture);
	}

	internal static string ResolveDir(string configured, out string why)
	{
		why = "";
		if (!string.IsNullOrWhiteSpace(configured))
		{
			return configured.Trim();
		}
		string home = Environment.GetEnvironmentVariable("HOME");
		if (string.IsNullOrWhiteSpace(home) || !home.StartsWith("/", StringComparison.Ordinal))
		{
			why = "no usable HOME env var - set the BridgeFolder preference";
			return null;
		}
		return "Z:" + home.Replace('/', '\\').TrimEnd('\\') + "\\.cache\\othradio";
	}
}
