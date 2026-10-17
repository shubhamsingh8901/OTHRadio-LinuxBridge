using System;
using System.Diagnostics;
using System.IO;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;

namespace OTHRadio;

internal static class RadioMenu
{
	private const float W = 760f;

	private const float Pad = 28f;

	private const float RowH = 42f;

	private const float Gap = 8f;

	private const float SectionGap = 26f;

	private static GameObject _root;

	private static GameObject _myMusic;

	private static GameObject _pc;

	private static GameObject _game;

	private static UiKit.Btn[] _stations;

	private static UiKit.Btn _play;

	private static UiKit.Btn _shuffle;

	private static UiKit.Btn[] _modes;

	private static readonly string[] ModeNames = new string[3] { "Always", "SongChange", "Off" };

	private static TextMeshProUGUI _title;

	private static TextMeshProUGUI _artist;

	private static TextMeshProUGUI _status;

	private static TextMeshProUGUI _volume;

	private static TextMeshProUGUI _songs;

	private static TextMeshProUGUI _pcHint;

	private static TextMeshProUGUI _gameHint;

	private static bool _open;

	private static bool _prevVisible;

	private static CursorLockMode _prevLock;

	public static bool IsOpen => _open;

	public static void Toggle()
	{
		if (_open)
		{
			Close();
		}
		else
		{
			Open();
		}
	}

	private static void Open()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)_root == (Object)null) || Build())
		{
			_prevVisible = Cursor.visible;
			_prevLock = Cursor.lockState;
			_root.SetActive(true);
			_open = true;
		}
	}

	public static void Close()
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_root != (Object)null)
		{
			_root.SetActive(false);
		}
		if (_open)
		{
			Cursor.visible = _prevVisible;
			Cursor.lockState = _prevLock;
		}
		_open = false;
	}

	public static void Refresh()
	{
		if (_open && !((Object)(object)_root == (Object)null))
		{
			Cursor.visible = true;
			Cursor.lockState = (CursorLockMode)0;
			Station current = Radio.Current;
			for (int i = 0; i < _stations.Length; i++)
			{
				UiKit.SetSelected(_stations[i], current == (Station)i);
			}
			((TMP_Text)_stations[2].Text).text = Radio.StationLabel(Station.PC);
			var (text, text2, flag) = Radio.NowPlaying();
			((TMP_Text)_title).text = (string.IsNullOrEmpty(text) ? "Nothing playing" : text);
			((TMP_Text)_artist).text = text2;
			((TMP_Text)_status).text = (flag ? "Playing" : "Paused");
			((TMP_Text)_play.Text).text = (flag ? "Pause" : "Play");
			Show(_game, current == Station.Game);
			Show(_myMusic, current == Station.MyMusic);
			Show(_pc, current == Station.PC);
			for (int j = 0; j < _modes.Length; j++)
			{
				UiKit.SetSelected(_modes[j], string.Equals(Radio.PanelMode.Value, ModeNames[j], StringComparison.OrdinalIgnoreCase));
			}
			UiKit.SetSelected(_shuffle, LocalPlayer.Shuffle);
			((TMP_Text)_shuffle.Text).text = (LocalPlayer.Shuffle ? "Shuffle on" : "Shuffle off");
			((TMP_Text)_volume).text = $"{Mathf.RoundToInt(LocalPlayer.Volume * 100f)}%";
			((TMP_Text)_songs).text = ((LocalPlayer.Files.Count == 0) ? "No songs yet - put mp3, ogg, flac or wav files in your music folder" : $"{LocalPlayer.Files.Count} songs in your music folder");
				_003F val = _pcHint;
			string text3;
			if (PcMedia.Available)
			{
				text3 = (PcMedia.HasSession ? ("Controlling " + Radio.StationLabel(Station.PC) + " on the host") : "Start music in Spotify (or any player) and it shows up here");
			}
			else
			{
				text3 = PcMedia.UnavailableHint;
			}
			((TMP_Text)val).text = text3;
		}
	}

	private static void Show(GameObject go, bool on)
	{
		if (go.activeSelf != on)
		{
			go.SetActive(on);
		}
	}

	private static bool Build()
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected Obj, but got Unknown
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0915: Unknown result type (might be due to invalid IL or missing references)
		//IL_092a: Unknown result type (might be due to invalid IL or missing references)
		HUDManager instance = HUDManager.Instance;
		if (!UiKit.Init(instance))
		{
			return false;
		}
		_root = new GameObject("OTH_RadioMenu");
		_root.transform.SetParent(((Component)instance).transform, false);
		RectTransform val = _root.AddComponent<RectTransform>();
		Vector2 anchorMin = (val.anchorMax = new Vector2(0.5f, 0.5f));
		val.anchorMin = anchorMin;
		val.pivot = new Vector2(0.5f, 0.5f);
		CanvasGroup val3 = _root.AddComponent<CanvasGroup>();
		val3.ignoreParentGroups = true;
		val3.interactable = true;
		val3.blocksRaycasts = true;
		GameObject go = UiKit.Glass(instance, _root.transform);
		Transform transform = _root.transform;
		float num = 704f;
		float num2 = 28f;
		UiKit.Text(transform, "Radio" + UpdateCheck.TitleSuffix, 28f, 28f, num2, 560f, 36f, (TextAlignmentOptions)4097, 1f, 0f);
		UiKit.Pill(transform, "Close  (" + Radio.MenuKey.Value + ")", 15f, 602f, num2 + 2f, 130f, 32f, Close);
		num2 += 54f;
		UiKit.Section(transform, "STATION", 28f, num2, num);
		num2 += 36f;
		float num3 = (num - 16f) / 3f;
		_stations = new UiKit.Btn[3]
		{
			UiKit.Pill(transform, "Game Soundtrack", 18f, 28f, num2, num3, 42f, () =>
			{
				Radio.SetStation(Station.Game);
			}),
			UiKit.Pill(transform, "My Music", 18f, 28f + num3 + 8f, num2, num3, 42f, () =>
			{
				Radio.SetStation(Station.MyMusic);
			}),
			UiKit.Pill(transform, "PC / Spotify", 18f, 28f + 2f * (num3 + 8f), num2, num3, 42f, () =>
			{
				Radio.SetStation(Station.PC);
			})
		};
		num2 += 68f;
		UiKit.Section(transform, "NOW PLAYING", 28f, num2, num);
		_status = UiKit.Text(transform, "", 15f, 532f, num2, 200f, 26f, (TextAlignmentOptions)4100, 0.65f, 0f);
		num2 += 38f;
		_title = UiKit.Text(transform, "", 26f, 28f, num2, num, 34f, (TextAlignmentOptions)4097, 1f, 0f);
		num2 += 34f;
		_artist = UiKit.Text(transform, "", 18f, 28f, num2, num, 26f, (TextAlignmentOptions)4097, 0.75f, 0f);
		num2 += 40f;
		float num4 = (num - 16f) / 3f;
		UiKit.Pill(transform, "Previous", 18f, 28f, num2, num4, 42f, Radio.Previous);
		_play = UiKit.Pill(transform, "Play", 18f, 28f + num4 + 8f, num2, num4, 42f, Radio.PlayPause);
		UiKit.Pill(transform, "Next", 18f, 28f + 2f * (num4 + 8f), num2, num4, 42f, Radio.Next);
		num2 += 68f;
		float num5 = num2;
		_myMusic = UiKit.Group(transform, "MyMusic");
		UiKit.Section(_myMusic.transform, "MY MUSIC", 28f, num5, num);
		float num6 = (num - 24f) / 4f;
		float num7 = num5 + 36f;
		_shuffle = UiKit.Pill(_myMusic.transform, "Shuffle on", 17f, 28f, num7, num6, 42f, () =>
		{
			Radio.SetShuffle(!LocalPlayer.Shuffle);
		});
		UiKit.Pill(_myMusic.transform, "Volume -", 17f, 28f + num6 + 8f, num7, num6, 42f, () =>
		{
			Radio.SetVolume(LocalPlayer.Volume - 0.1f);
		});
		_volume = UiKit.Pill(_myMusic.transform, "", 20f, 28f + 2f * (num6 + 8f), num7, num6, 42f, null).Text;
		UiKit.Pill(_myMusic.transform, "Volume +", 17f, 28f + 3f * (num6 + 8f), num7, num6, 42f, () =>
		{
			Radio.SetVolume(LocalPlayer.Volume + 0.1f);
		});
		num7 += 50f;
		_songs = UiKit.Text(_myMusic.transform, "", 16f, 28f, num7, num - 2f * (num6 + 8f), 42f, (TextAlignmentOptions)4097, 0.75f, 0f);
		UiKit.Pill(_myMusic.transform, "Open folder", 17f, 28f + 2f * (num6 + 8f), num7, num6, 42f, OpenFolder);
		UiKit.Pill(_myMusic.transform, "Rescan", 17f, 28f + 3f * (num6 + 8f), num7, num6, 42f, LocalPlayer.Scan);
		_pc = UiKit.Group(transform, "PC");
		UiKit.Section(_pc.transform, "PC MUSIC", 28f, num5, num);
		_pcHint = UiKit.Text(_pc.transform, "", 17f, 28f, num5 + 36f, num, 42f, (TextAlignmentOptions)4097, 0.85f, 0f);
		UiKit.Text(_pc.transform, "Works with Spotify (free or premium), YouTube Music, browsers and other players.", 15f, 28f, num5 + 36f + 42f, num, 30f, (TextAlignmentOptions)4097, 0.6f, 0f);
		_game = UiKit.Group(transform, "Game");
		UiKit.Section(_game.transform, "GAME SOUNDTRACK", 28f, num5, num);
		_gameHint = UiKit.Text(_game.transform, "The game's own music. Albums and the music menu work as usual.", 17f, 28f, num5 + 36f, num, 42f, (TextAlignmentOptions)4097, 0.85f, 0f);
		num2 = num5 + 36f + 84f + 8f + 26f;
		UiKit.Section(transform, "ON-SCREEN PLAYER", 28f, num2, num);
		num2 += 36f;
		UiKit.Text(transform, "Show the player above the gauges", 17f, 28f, num2, num - 414f, 42f, (TextAlignmentOptions)4097, 0.85f, 0f);
		_modes = new UiKit.Btn[3]
		{
			UiKit.Pill(transform, "Always", 17f, 326f, num2, 130f, 42f, () =>
			{
				Radio.SetPanelMode("Always");
			}),
			UiKit.Pill(transform, "Song change", 16f, 464f, num2, 130f, 42f, () =>
			{
				Radio.SetPanelMode("SongChange");
			}),
			UiKit.Pill(transform, "Off", 17f, 602f, num2, 130f, 42f, () =>
			{
				Radio.SetPanelMode("Off");
			})
		};
		num2 += 70f;
		val.sizeDelta = new Vector2(760f, num2);
		val.anchoredPosition = new Vector2(0f, 40f);
		UiKit.Place(go, 0f, 0f, 760f, num2);
		_root.SetActive(false);
		return true;
	}

	private static void OpenFolder()
	{
		try
		{
			Directory.CreateDirectory(LocalPlayer.Folder);
			if (WineDetector.IsRunningUnderWine())
			{
				Radio.Notify("Music folder: " + LocalPlayer.Folder);
				Radio.Log.Msg("Your music folder: " + LocalPlayer.Folder + " (no explorer under Proton - use your Linux file manager)");
				return;
			}
			Process.Start(new ProcessStartInfo("explorer.exe", "\"" + LocalPlayer.Folder + "\"")
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			Radio.Log.Warning("Couldn't open folder: " + ex.Message);
		}
	}
}
