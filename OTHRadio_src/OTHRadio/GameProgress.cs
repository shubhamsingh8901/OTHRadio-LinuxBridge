using System.Text.RegularExpressions;
using Il2Cpp;
using Il2CppFMOD;
using Il2CppFMOD.Studio;
using UnityEngine;

namespace OTHRadio;

internal static class GameProgress
{
	private const uint MinSongMs = 20000u;

	private static string _lastName = "";

	public static string Title = "";

	public static string Artist = "";

	private static int _frame = -1;

	private static (double pos, double len) _cached = (pos: -1.0, len: -1.0);

	private static (string title, string artist) ParseName(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return (title: "", artist: "");
		}
		Match match = Regex.Match(name, "^(?<artist>.+?)_Track_\\d+_(?<title>.+)$");
		if (!match.Success)
		{
			return (title: name.Replace('_', ' ').Trim(), artist: "");
		}
		string text = Regex.Replace(match.Groups["artist"].Value.Replace('_', ' '), "(?<=[a-z])(?=[A-Z])", " ");
		return (title: match.Groups["title"].Value.Replace('_', ' ').Trim(), artist: text.Trim());
	}

	public static (double pos, double len) Get()
	{
		int frameCount = Time.frameCount;
		if (frameCount == _frame)
		{
			return _cached;
		}
		_frame = frameCount;
		return _cached = Read();
	}

	private static (double pos, double len) Read()
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		InGameMusicManager instance = InGameMusicManager.Instance;
		if ((Object)(object)instance == (Object)null || (Object)(object)instance.musicEmitter == (Object)null)
		{
			return (pos: -1.0, len: -1.0);
		}
		try
		{
			EventInstance eventInstance = instance.musicEmitter.EventInstance;
			ChannelGroup val = default;
			if ((int)eventInstance.getChannelGroup(out val) != 0)
			{
				return (pos: -1.0, len: -1.0);
			}
			uint bestLen = 0u;
			uint bestPos = 0u;
			Sound bestSound = default;
			Walk(val, 0, ref bestLen, ref bestPos, ref bestSound);
			if (bestLen < 20000)
			{
				return (pos: -1.0, len: -1.0);
			}
			string text = default;
			bestSound.getName(out text, 256);
			if (text != _lastName)
			{
				_lastName = text;
				(Title, Artist) = ParseName(text);
				int num = default;
				eventInstance.getTimelinePosition(out num);
				Radio.Log.Msg($"Game soundtrack file: '{text}' {(double)bestLen / 1000.0:0.0}s (at {(double)bestPos / 1000.0:0.0}s, event timeline {(double)num / 1000.0:0.0}s)");
			}
			return (pos: (double)bestPos / 1000.0, len: (double)bestLen / 1000.0);
		}
		catch
		{
			return (pos: -1.0, len: -1.0);
		}
	}

	private static void Walk(ChannelGroup group, int depth, ref uint bestLen, ref uint bestPos, ref Sound bestSound)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if (depth > 8)
		{
			return;
		}
		int num = default;
		if ((int)group.getNumChannels(out num) == 0)
		{
			Channel val = default;
			bool flag = default;
			Sound val2 = default;
			uint num2 = default;
			uint num3 = default;
			for (int i = 0; i < num; i++)
			{
				if ((int)group.getChannel(i, out val) == 0 && (int)val.isPlaying(out flag) == 0 && flag && (int)val.getCurrentSound(out val2) == 0 && (int)val2.getLength(out num2, (TIMEUNIT)1) == 0 && num2 > bestLen)
				{
					val.getPosition(out num3, (TIMEUNIT)1);
					bestLen = num2;
					bestPos = num3;
					bestSound = val2;
				}
			}
		}
		int num4 = default;
		if ((int)group.getNumGroups(out num4) != 0)
		{
			return;
		}
		ChannelGroup val3 = default;
		for (int j = 0; j < num4; j++)
		{
			if ((int)group.getGroup(j, out val3) == 0)
			{
				Walk(val3, depth + 1, ref bestLen, ref bestPos, ref bestSound);
			}
		}
	}
}
