using System;
using System.Collections.Generic;
using System.IO;
using Il2CppFMOD;
using Il2CppFMODUnity;

namespace OTHRadio;

internal static class LocalPlayer
{
	private static readonly string[] Extensions = new string[4] { ".mp3", ".ogg", ".flac", ".wav" };

	public static string Folder;

	public static List<string> Files = new List<string>();

	public static int Index = -1;

	public static bool Shuffle = true;

	public static float Volume = 0.7f;

	private static Sound _sound;

	private static Channel _channel;

	private static bool _hasSound;

	private static bool _paused;

	private static readonly Random _rng = new Random();

	private static List<int> _order = new List<int>();

	public static bool IsPlaying
	{
		get
		{
			if (_hasSound)
			{
				return !_paused;
			}
			return false;
		}
	}

	public static bool HasTrack => _hasSound;

	public static string CurrentTitle
	{
		get
		{
			if (Index < 0 || Index >= Files.Count)
			{
				return "";
			}
			return Split(Files[Index]).title;
		}
	}

	public static string CurrentArtist
	{
		get
		{
			if (Index < 0 || Index >= Files.Count)
			{
				return "";
			}
			return Split(Files[Index]).artist;
		}
	}

	public static void Scan()
	{
		Files.Clear();
		try
		{
			Directory.CreateDirectory(Folder);
			foreach (string item in Directory.EnumerateFiles(Folder, "*.*", SearchOption.AllDirectories))
			{
				if (Array.IndexOf(Extensions, Path.GetExtension(item).ToLowerInvariant()) >= 0)
				{
					Files.Add(item);
				}
			}
			Files.Sort(StringComparer.OrdinalIgnoreCase);
		}
		catch (Exception ex)
		{
			Radio.Log.Warning("Couldn't read music folder: " + ex.Message);
		}
		BuildOrder();
		Radio.Log.Msg($"My music: {Files.Count} song(s) in {Folder}");
	}

	private static void BuildOrder()
	{
		_order = new List<int>();
		for (int i = 0; i < Files.Count; i++)
		{
			_order.Add(i);
		}
		if (Shuffle)
		{
			for (int num = _order.Count - 1; num > 0; num--)
			{
				int num2 = _rng.Next(num + 1);
				List<int> order = _order;
				int index = num;
				List<int> order2 = _order;
				int index2 = num2;
				int value = _order[num2];
				int value2 = _order[num];
				order[index] = value;
				order2[index2] = value2;
			}
		}
	}

	public static void SetShuffle(bool on)
	{
		Shuffle = on;
		BuildOrder();
	}

	public static void Play()
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		if (Files.Count != 0)
		{
			if (_hasSound)
			{
				_paused = false;
				_channel.setPaused(false);
			}
			else
			{
				PlayIndex((Index >= 0) ? Index : _order[0]);
			}
		}
	}

	public static void Pause()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (_hasSound)
		{
			_paused = true;
			_channel.setPaused(true);
		}
	}

	public static void Toggle()
	{
		if (IsPlaying)
		{
			Pause();
		}
		else
		{
			Play();
		}
	}

	public static void Next()
	{
		Step(1);
	}

	public static void Previous()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		uint num = default;
		if (_hasSound && (int)_channel.getPosition(out num, (TIMEUNIT)1) == 0 && num > 4000)
		{
			_channel.setPosition(0u, (TIMEUNIT)1);
		}
		else
		{
			Step(-1);
		}
	}

	private static void Step(int dir)
	{
		if (Files.Count != 0)
		{
			int num = Math.Max(0, _order.IndexOf(Index));
			num = ((num + dir) % _order.Count + _order.Count) % _order.Count;
			PlayIndex(_order[num]);
		}
	}

	private static void PlayIndex(int i)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		StopSound();
		if (i < 0 || i >= Files.Count)
		{
			return;
		}
		Index = i;
		Il2CppFMOD.System coreSystem = RuntimeManager.CoreSystem;
		RESULT val = coreSystem.createStream(Files[i], (MODE)9, out _sound);
		if ((int)val != 0)
		{
			Radio.Log.Warning($"Can't play {Path.GetFileName(Files[i])}: {val}");
			return;
		}
		val = coreSystem.playSound(_sound, default(ChannelGroup), false, out _channel);
		if ((int)val != 0)
		{
			_sound.release();
			Radio.Log.Warning("FMOD playSound failed: " + ((object)val/*cast due to constrained. prefix*/).ToString());
			return;
		}
		_hasSound = true;
		_paused = false;
		ApplyVolume();
		Radio.TrackChanged(CurrentTitle ?? "", CurrentArtist);
	}

	public static (double pos, double len) Progress()
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		if (!_hasSound)
		{
			return (pos: -1.0, len: -1.0);
		}
		uint num = default;
		if ((int)_sound.getLength(out num, (TIMEUNIT)1) != 0 || num == 0)
		{
			return (pos: -1.0, len: -1.0);
		}
		uint num2 = default;
		_channel.getPosition(out num2, (TIMEUNIT)1);
		return (pos: (double)num2 / 1000.0, len: (double)num / 1000.0);
	}

	public static void ApplyVolume()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		if (_hasSound)
		{
			_channel.setVolume(Volume);
		}
	}

	public static void StopSound()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (_hasSound)
		{
			_channel.stop();
			_sound.release();
			_hasSound = false;
			_paused = false;
		}
	}

	public static void Update()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		if (_hasSound && !_paused)
		{
			bool flag = default;
			if ((int)_channel.isPlaying(out flag) != 0 || !flag)
			{
				Next();
			}
			else
			{
				ApplyVolume();
			}
		}
	}

	private static (string title, string artist) Split(string path)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
		int num = fileNameWithoutExtension.IndexOf(" - ", StringComparison.Ordinal);
		if (num > 0)
		{
			return (title: fileNameWithoutExtension.Substring(num + 3).Trim(), artist: fileNameWithoutExtension.Substring(0, num).Trim());
		}
		return (title: fileNameWithoutExtension, artist: "");
	}
}
