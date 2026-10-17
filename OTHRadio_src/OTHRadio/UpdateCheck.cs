using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OTHRadio;

internal static class UpdateCheck
{
	private const string Url = "https://raw.githubusercontent.com/SquidFromWish007/OverTheHill-Mods/main/versions.json";

	private static MelonPreferences_Entry<bool> _enabled;

	private static string _name;

	private static volatile string _important;

	private static volatile string _page;

	internal static volatile string Latest;

	private static bool _shown;

	private static float _showAt = -1f;

	private static GameObject _popup;

	private static bool _prevVisible;

	private static CursorLockMode _prevLock;

	internal static string TitleSuffix
	{
		get
		{
			if (Latest != null)
			{
				return "   <size=60%><color=#FFD452>Update " + Latest + " on Nexus</color>";
			}
			return "";
		}
	}

	internal static void Bind(MelonPreferences_Category cfg)
	{
		_enabled = cfg.CreateEntry<bool>("CheckForUpdates", true, (string)null, "Once per launch, check if a newer version is on Nexus and show a note in the menu (and a popup for important fixes). Only reads a small version file from GitHub.", false, false, (ValueValidator)null, (string)null);
	}

	internal static void Start(string key, string current, string name, Instance log)
	{
		_name = name.Replace("Over the Hill ", "");
		if (_enabled != null && !_enabled.Value)
		{
			return;
		}
		Thread thread = new Thread(() =>
		{
			try
			{
				using HttpClient httpClient = new HttpClient
				{
					Timeout = TimeSpan.FromSeconds(8.0)
				};
				httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OTH-mods-update-check");
				string result = httpClient.GetStringAsync("https://raw.githubusercontent.com/SquidFromWish007/OverTheHill-Mods/main/versions.json").GetAwaiter().GetResult();
				string text = Field(result, key);
				if (text != null && Newer(text, current))
				{
					_important = Field(result, key + "_important");
					_page = Field(result, key + "_url");
					Latest = text;
					log.Msg($"Update {Latest} is available on Nexus (you have {current}).{((_important != null) ? (" Important: " + _important) : "")}");
				}
			}
			catch
			{
			}
		});
		thread.IsBackground = true;
		thread.Name = "OTH update check";
		thread.Start();
	}

	private static string Field(string json, string name)
	{
		Match match = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
		if (!match.Success)
		{
			return null;
		}
		return Regex.Unescape(match.Groups[1].Value);
	}

	private static bool Newer(string latest, string current)
	{
		if (Version.TryParse(Fix(latest), out Version result) && Version.TryParse(Fix(current), out Version result2))
		{
			return result > result2;
		}
		return false;
	}

	private static string Fix(string v)
	{
		if (!v.Contains('.'))
		{
			return v + ".0";
		}
		return v;
	}

	internal static void Update()
	{
		if ((Object)(object)_popup != (Object)null)
		{
			Cursor.visible = true;
			Cursor.lockState = (CursorLockMode)0;
		}
		else
		{
			if (_shown || Latest == null || _important == null)
			{
				return;
			}
			HUDManager instance = HUDManager.Instance;
			if ((Object)(object)instance == (Object)null || !instance.playerSpawned)
			{
				_showAt = -1f;
			}
			else if (_showAt < 0f)
			{
				_showAt = Time.unscaledTime + 4f;
			}
			else if (!(Time.unscaledTime < _showAt))
			{
				_shown = true;
				try
				{
					Show(instance);
				}
				catch (Exception ex)
				{
					MelonLogger.Warning("Update popup failed: " + ex.Message);
				}
			}
		}
	}

	private static void Show(HUDManager hud)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected Obj, but got Unknown
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		TMP_Text componentInChildren = ((Component)hud).GetComponentInChildren<TMP_Text>(true);
		_popup = new GameObject("OTH_UpdatePopup");
		_popup.transform.SetParent(((Component)hud).transform, false);
		RectTransform val = _popup.AddComponent<RectTransform>();
		Vector2 val2 = (val.pivot = new Vector2(0.5f, 0.5f));
		Vector2 anchorMin = (val.anchorMax = val2);
		val.anchorMin = anchorMin;
		val.sizeDelta = new Vector2(560f, 230f);
		_popup.AddComponent<CanvasGroup>().ignoreParentGroups = true;
		((Graphic)_popup.AddComponent<Image>()).color = new Color(0.075f, 0.071f, 0.071f, 0.94f);
		Label(_popup.transform, componentInChildren, _name + " update " + Latest, 26f, new Vector2(0f, 72f), new Vector2(520f, 40f), (FontStyles)1);
		Label(_popup.transform, componentInChildren, _important, 18f, new Vector2(0f, 10f), new Vector2(500f, 80f), (FontStyles)0);
		bool flag = !string.IsNullOrEmpty(_page);
		if (flag)
		{
			Button(_popup.transform, componentInChildren, "Open Nexus", new Vector2(-120f, -78f), () =>
			{
				Application.OpenURL(_page);
				Close();
			});
		}
		Button(_popup.transform, componentInChildren, "OK", new Vector2(flag ? 120f : 0f, -78f), Close);
		_prevVisible = Cursor.visible;
		_prevLock = Cursor.lockState;
	}

	private static void Close()
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_popup != (Object)null)
		{
			Object.Destroy((Object)(object)_popup);
		}
		_popup = null;
		Cursor.visible = _prevVisible;
		Cursor.lockState = _prevLock;
	}

	private static TextMeshProUGUI Label(Transform parent, TMP_Text font, string text, float size, Vector2 pos, Vector2 box, FontStyles style)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject("Text");
		val.transform.SetParent(parent, false);
		RectTransform val2 = val.AddComponent<RectTransform>();
		val2.anchoredPosition = pos;
		val2.sizeDelta = box;
		TextMeshProUGUI val3 = val.AddComponent<TextMeshProUGUI>();
		if ((Object)(object)font != (Object)null)
		{
			((TMP_Text)val3).font = font.font;
			((TMP_Text)val3).fontSharedMaterial = font.fontSharedMaterial;
		}
		((TMP_Text)val3).text = text;
		((TMP_Text)val3).fontSize = size;
		((TMP_Text)val3).fontStyle = style;
		((Graphic)val3).color = new Color(1f, 0.976f, 0.933f, 1f);
		((TMP_Text)val3).alignment = (TextAlignmentOptions)514;
		((TMP_Text)val3).enableWordWrapping = true;
		((Graphic)val3).raycastTarget = false;
		return val3;
	}

	private static void Button(Transform parent, TMP_Text font, string label, Vector2 pos, Action onClick)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject("Button");
		val.transform.SetParent(parent, false);
		RectTransform val2 = val.AddComponent<RectTransform>();
		val2.anchoredPosition = pos;
		val2.sizeDelta = new Vector2(200f, 44f);
		Image val3 = val.AddComponent<Image>();
		((Graphic)val3).color = new Color(1f, 0.976f, 0.933f, 0.14f);
		Button val4 = val.AddComponent<Button>();
		((Selectable)val4).targetGraphic = (Graphic)(object)val3;
		((UnityEvent)val4.onClick).AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Delegate)onClick));
		TextMeshProUGUI val5 = Label(val.transform, font, label, 18f, Vector2.zero, val2.sizeDelta, (FontStyles)1);
		((TMP_Text)val5).rectTransform.anchorMin = Vector2.zero;
		((TMP_Text)val5).rectTransform.anchorMax = Vector2.one;
		((TMP_Text)val5).rectTransform.sizeDelta = Vector2.zero;
	}
}
