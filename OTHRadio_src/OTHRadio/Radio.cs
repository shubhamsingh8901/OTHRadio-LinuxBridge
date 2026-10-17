using System;
using System.IO;
using Il2Cpp;
using Il2CppFMOD.Studio;
using Il2CppFMODUnity;
using MelonLoader;
using MelonLoader.Preferences;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace OTHRadio;

public class Radio : MelonMod
{
	internal static MelonLogger.Instance Log;

	private static MelonPreferences_Category _cfg;

	internal static MelonPreferences_Entry<string> MenuKey;

	internal static MelonPreferences_Entry<string> PlayPauseKey;

	internal static MelonPreferences_Entry<string> NextKey;

	internal static MelonPreferences_Entry<string> PrevKey;

	internal static MelonPreferences_Entry<string> FolderEntry;

	internal static MelonPreferences_Entry<string> StationEntry;

	private static MelonPreferences_Entry<float> VolumeEntry;

	private static MelonPreferences_Entry<bool> ShuffleEntry;

	private static MelonPreferences_Entry<bool> PopupEntry;

	internal static MelonPreferences_Entry<string> PanelMode;

	internal static MelonPreferences_Entry<bool> UseLinuxBridge;

	internal static MelonPreferences_Entry<string> BridgeFolder;

	internal static Station Current = Station.Game;

	private static string _lastTitle = "";

	private static float _nextEnforce;

	private bool _started;

	private static Bus _musicBus;

	private static bool _busLooked;

	private static bool _busFound;

	public override void OnInitializeMelon()
	{
		Log = ((MelonBase)this).LoggerInstance;
		_cfg = MelonPreferences.CreateCategory("OTHRadio", "Radio");
		MenuKey = _cfg.CreateEntry<string>("MenuKey", "F6", (string)null, "Opens / closes the radio menu.", false, false, (ValueValidator)null, (string)null);
		PlayPauseKey = _cfg.CreateEntry<string>("PlayPauseKey", "", (string)null, "Optional key for play / pause without opening the menu (e.g. Numpad5). Empty = off.", false, false, (ValueValidator)null, (string)null);
		NextKey = _cfg.CreateEntry<string>("NextKey", "", (string)null, "Optional key for next song (e.g. Numpad6). Empty = off.", false, false, (ValueValidator)null, (string)null);
		PrevKey = _cfg.CreateEntry<string>("PreviousKey", "", (string)null, "Optional key for previous song (e.g. Numpad4). Empty = off.", false, false, (ValueValidator)null, (string)null);
		FolderEntry = _cfg.CreateEntry<string>("MusicFolder", "", (string)null, "Folder with your own music (mp3, ogg, flac, wav). Empty = UserData/OTHRadio/Music.", false, false, (ValueValidator)null, (string)null);
		StationEntry = _cfg.CreateEntry<string>("Station", "Game", (string)null, "Last station: Game, MyMusic or PC.", false, false, (ValueValidator)null, (string)null);
		VolumeEntry = _cfg.CreateEntry<float>("MyMusicVolume", 0.7f, (string)null, "Volume for your own music, 0 to 1 (also follows the game's music volume).", false, false, (ValueValidator)null, (string)null);
		ShuffleEntry = _cfg.CreateEntry<bool>("Shuffle", true, (string)null, "Shuffle your own music.", false, false, (ValueValidator)null, (string)null);
		PanelMode = _cfg.CreateEntry<string>("PlayerMode", "Always", (string)null, "On-screen player above the tilt gauges: Always, SongChange (shows for a few seconds when the song changes or you use the controls) or Off.", false, false, (ValueValidator)null, (string)null);
		PopupEntry = _cfg.CreateEntry<bool>("SongPopup", true, (string)null, "Show a popup when the song changes.", false, false, (ValueValidator)null, (string)null);
		BridgeFolder = _cfg.CreateEntry<string>("BridgeFolder", "", (string)null, "PC station: fixed bridge folder path, e.g. Z:\\home\\me\\.cache\\othradio. Empty = HOME/.cache/othradio. Only used when the Linux bridge is active.", false, false, (ValueValidator)null, (string)null);
		UseLinuxBridge = _cfg.CreateEntry<bool>("UseLinuxBridge", false, (string)null, "PC station: force the Linux bridge (playerctl). Off = auto-detect: the bridge is used automatically under Proton/Wine.", false, false, (ValueValidator)null, (string)null);
		UpdateCheck.Bind(_cfg);
		_cfg.SetFilePath(Path.Combine(MelonEnvironment.UserDataDirectory, "OTHRadio.cfg"), true, false);
		_cfg.SaveToFile(false);
		UpdateCheck.Start("radio", ((MelonBase)this).Info.Version, ((MelonBase)this).Info.Name, ((MelonBase)this).LoggerInstance);
		LocalPlayer.Folder = (string.IsNullOrWhiteSpace(FolderEntry.Value) ? Path.Combine(MelonEnvironment.UserDataDirectory, "OTHRadio", "Music") : FolderEntry.Value);
		LocalPlayer.Volume = Mathf.Clamp01(VolumeEntry.Value);
		LocalPlayer.Shuffle = ShuffleEntry.Value;
		LocalPlayer.Scan();
		Enum.TryParse<Station>(StationEntry.Value, ignoreCase: true, out Current);
		PcMedia.Start();
		Log.Msg("Radio loaded. " + MenuKey.Value + " opens the radio.");
	}

	public override void OnDeinitializeMelon()
	{
		PcMedia.Stop();
		LocalPlayer.StopSound();
	}

	public override void OnUpdate()
	{
		MenuInput.Tick();
		UpdateCheck.Update();
		PcMedia.Fast = Current == Station.PC || RadioMenu.IsOpen;
		try
		{
			InGameMusicManager instance = InGameMusicManager.Instance;
			if (!((Object)(object)TimeManager.Instance != (Object)null) || !((Object)(object)HUDManager.Instance != (Object)null) || !((Object)(object)instance != (Object)null))
			{
				RadioMenu.Close();
				_started = false;
				return;
			}
			if (!_started)
			{
				_started = true;
				if (Current != Station.Game)
				{
					SetStation(Current);
				}
			}
			Keyboard current = Keyboard.current;
			if (current != null)
			{
				if (Pressed(current, MenuKey.Value))
				{
					RadioMenu.Toggle();
				}
				if (Pressed(current, PlayPauseKey.Value))
				{
					PlayPause();
				}
				if (Pressed(current, NextKey.Value))
				{
					Next();
				}
				if (Pressed(current, PrevKey.Value))
				{
					Previous();
				}
			}
			LocalPlayer.Update();
			KeepGameMusicQuiet(instance);
			WatchTrack();
			RadioMenu.Refresh();
			NowPlayingPanel.Update();
		}
		catch (Exception ex)
		{
			Log.Error(ex.ToString());
		}
	}

	internal static void SetStation(Station s)
	{
		InGameMusicManager instance = InGameMusicManager.Instance;
		Current = s;
		StationEntry.Value = s.ToString();
		_cfg.SaveToFile(false);
		if (s != Station.Game && (Object)(object)instance != (Object)null && instance.IsMusicOn)
		{
			instance.Pause();
		}
		if (s != Station.MyMusic)
		{
			LocalPlayer.Pause();
		}
		if (s != Station.PC && PcMedia.IsPlaying)
		{
			PcMedia.Send(PcCommand.Pause);
		}
		switch (s)
		{
		case Station.Game:
			if ((Object)(object)instance != (Object)null && !instance.IsMusicOn)
			{
				instance.Play();
			}
			break;
		case Station.MyMusic:
			if (LocalPlayer.Files.Count == 0)
			{
				Notify("No music found - add songs to the My Music folder");
			}
			else
			{
				LocalPlayer.Play();
			}
			break;
		case Station.PC:
			if (!PcMedia.HasSession)
			{
				Notify("Nothing playing - start Spotify or another player on the host");
			}
			else
			{
				PcMedia.Send(PcCommand.Play);
			}
			break;
		}
		_lastTitle = "";
		NowPlayingPanel.Poke();
	}

	internal static void PlayPause()
	{
		NowPlayingPanel.Poke();
		PlayPauseInner();
	}

	private static void PlayPauseInner()
	{
		switch (Current)
		{
		case Station.Game:
		{
			InGameMusicManager instance = InGameMusicManager.Instance;
			if (instance != null)
			{
				instance.TogglePlay();
			}
			break;
		}
		case Station.MyMusic:
			LocalPlayer.Toggle();
			break;
		case Station.PC:
			PcMedia.Send(PcCommand.Toggle);
			break;
		}
	}

	internal static void Next()
	{
		NowPlayingPanel.Poke();
		NextInner();
	}

	private static void NextInner()
	{
		switch (Current)
		{
		case Station.Game:
			SkipGameTrack(1);
			break;
		case Station.MyMusic:
			LocalPlayer.Next();
			break;
		case Station.PC:
			PcMedia.Send(PcCommand.Next);
			break;
		}
	}

	internal static void Previous()
	{
		NowPlayingPanel.Poke();
		PreviousInner();
	}

	private static void PreviousInner()
	{
		switch (Current)
		{
		case Station.Game:
			SkipGameTrack(-1);
			break;
		case Station.MyMusic:
			LocalPlayer.Previous();
			break;
		case Station.PC:
			PcMedia.Send(PcCommand.Previous);
			break;
		}
	}

	private static void SkipGameTrack(int dir)
	{
		InGameMusicManager instance = InGameMusicManager.Instance;
		if (!((Object)(object)instance == (Object)null))
		{
			string value = SafeTrack(instance);
			if (dir > 0)
			{
				instance.NextTrack();
			}
			else
			{
				instance.PreviousTrack();
			}
			instance.ConfirmTrack();
			if (!instance.IsMusicOn)
			{
				instance.Play();
			}
			Log.Msg($"Game soundtrack: '{value}' -> '{SafeTrack(instance)}' (album {instance.CurrentAlbumIndex}, track {instance.CurrentTrackNumber})");
		}
	}

	private static string SafeTrack(InGameMusicManager m)
	{
		try
		{
			return m.GetCurrentTrack() ?? "";
		}
		catch
		{
			return "";
		}
	}

	internal static void SetVolume(float v)
	{
		LocalPlayer.Volume = Mathf.Clamp01(v);
		VolumeEntry.Value = LocalPlayer.Volume;
		_cfg.SaveToFile(false);
		LocalPlayer.ApplyVolume();
	}

	internal static void SetShuffle(bool on)
	{
		LocalPlayer.SetShuffle(on);
		ShuffleEntry.Value = on;
		_cfg.SaveToFile(false);
	}

	internal static float StationVolume()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		switch (Current)
		{
		case Station.MyMusic:
			return LocalPlayer.Volume;
		case Station.PC:
			return PcMedia.Volume;
		default:
			GameMusicVolume();
			if (!_busFound)
			{
				return -1f;
			}
			try
			{
				float num = default;
				return ((int)_musicBus.getVolume(ref num) == 0) ? Mathf.Clamp01(num) : (-1f);
			}
			catch
			{
				return -1f;
			}
		}
	}

	internal static void SetStationVolume(float v)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		v = Mathf.Clamp01(v);
		switch (Current)
		{
		case Station.MyMusic:
			SetVolume(v);
			return;
		case Station.PC:
			PcMedia.SetVolume(v);
			return;
		}
		GameMusicVolume();
		if (_busFound)
		{
			try
			{
				_musicBus.setVolume(v);
			}
			catch
			{
			}
		}
	}

	internal static (string title, string artist, bool playing) NowPlaying()
	{
		switch (Current)
		{
		case Station.MyMusic:
			return (title: LocalPlayer.HasTrack ? LocalPlayer.CurrentTitle : "", artist: LocalPlayer.CurrentArtist, playing: LocalPlayer.IsPlaying);
		case Station.PC:
			return (title: PcMedia.HasSession ? PcMedia.Title : "", artist: PcMedia.HasSession ? PcMedia.Artist : "", playing: PcMedia.IsPlaying);
		default:
		{
			InGameMusicManager instance = InGameMusicManager.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				return (title: "", artist: "", playing: false);
			}
			string item = "";
			string item2 = "";
			try
			{
				item = instance.GetCurrentTrack() ?? "";
			}
			catch
			{
			}
			try
			{
				Album currentAlbum = instance.GetCurrentAlbum();
				item2 = ((currentAlbum != null) ? (currentAlbum.Title ?? "") : "");
			}
			catch
			{
			}
			GameProgress.Get();
			if (!string.IsNullOrEmpty(GameProgress.Title))
			{
				return (title: GameProgress.Title, artist: GameProgress.Artist, playing: instance.IsMusicOn);
			}
			return (title: item, artist: item2, playing: instance.IsMusicOn);
		}
		}
	}

	internal static (double pos, double len) Progress()
	{
		return Current switch
		{
			Station.MyMusic => LocalPlayer.Progress(), 
			Station.PC => PcMedia.HasSession ? PcMedia.Progress() : (pos: -1.0, len: -1.0), 
			_ => GameProgress.Get(), 
		};
	}

	internal static string StationLabel(Station s)
	{
		return s switch
		{
			Station.MyMusic => "My Music", 
			Station.PC => string.IsNullOrEmpty(PcMedia.App) ? "PC / Spotify" : PcMedia.App, 
			_ => "Game Soundtrack", 
		};
	}

	private static void WatchTrack()
	{
		(string title, string artist, bool playing) tuple = NowPlaying();
		var (text, artist, _) = tuple;
		if (tuple.playing && !string.IsNullOrEmpty(text) && !(text == _lastTitle))
		{
			_lastTitle = text;
			TrackChanged(text, artist);
		}
	}

	internal static void SetPanelMode(string mode)
	{
		PanelMode.Value = mode;
		_cfg.SaveToFile(false);
		NowPlayingPanel.Poke();
	}

	internal static void TrackChanged(string title, string artist)
	{
		_lastTitle = title;
		NowPlayingPanel.Poke();
		if (PopupEntry.Value && !RadioMenu.IsOpen)
		{
			Notify(string.IsNullOrEmpty(artist) ? title : (title + "  -  " + artist));
		}
	}

	private static void KeepGameMusicQuiet(InGameMusicManager music)
	{
		if (Current != Station.Game && !(Time.unscaledTime < _nextEnforce))
		{
			_nextEnforce = Time.unscaledTime + 1f;
			if (music.IsMusicOn)
			{
				music.Pause();
			}
		}
	}

	internal static float GameMusicVolume()
	{
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		if (!_busLooked)
		{
			_busLooked = true;
			string[] array = new string[4] { "bus:/Music", "bus:/MUSIC", "bus:/Master/Music", "bus:/music" };
			foreach (string text in array)
			{
				try
				{
					System studioSystem = RuntimeManager.StudioSystem;
					if ((int)studioSystem.getBus(text, ref _musicBus) == 0)
					{
						_busFound = true;
						Log.Msg("Following game music volume via " + text);
						break;
					}
				}
				catch
				{
				}
			}
		}
		if (!_busFound)
		{
			return 1f;
		}
		try
		{
			float num = default;
			float num2 = default;
			return ((int)_musicBus.getVolume(ref num, ref num2) == 0) ? Mathf.Clamp01(num2) : 1f;
		}
		catch
		{
			return 1f;
		}
	}

	internal static void Notify(string text)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			HUDManager instance = HUDManager.Instance;
			if (instance != null)
			{
				instance.ShowNotification(new Color(1f, 0.976f, 0.933f, 1f), text, (string)null, 0.2f, 3f, 0.4f);
			}
		}
		catch (Exception ex)
		{
			Log.Warning("Notification failed: " + ex.Message);
		}
	}

	private static bool Pressed(Keyboard kb, string keyName)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrWhiteSpace(keyName) || !Enum.TryParse<Key>(keyName, true, out Key result) || (int)result == 0)
		{
			return false;
		}
		KeyControl val = kb[result];
		if (val != null)
		{
			return ((ButtonControl)val).wasPressedThisFrame;
		}
		return false;
	}
}
