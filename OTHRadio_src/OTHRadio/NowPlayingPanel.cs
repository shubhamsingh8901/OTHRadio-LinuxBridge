using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OTHRadio;

internal static class NowPlayingPanel
{
	private const float W = 400f;

	private const float H = 126f;

	private const float Pad = 14f;

	private static GameObject _panel;

	private static GameObject _progress;

	private static TextMeshProUGUI _line1;

	private static TextMeshProUGUI _line2;

	private static TextMeshProUGUI _elapsed;

	private static TextMeshProUGUI _remaining;

	private static RectTransform _fill;

	private const float PopSeconds = 8f;

	private const float FadeSeconds = 0.25f;

	private static CanvasGroup _group;

	private static float _visibleUntil;

	private static UiKit.Btn _play;

	private static Slider _slider;

	private static CanvasGroup _sliderGroup;

	private static float _nextVolumeSync;

	public static void Poke()
	{
		_visibleUntil = Time.unscaledTime + 8f;
	}

	private static bool Hovered()
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_panel == (Object)null || !_panel.activeSelf)
		{
			return false;
		}
		Mouse current = Mouse.current;
		if (current == null)
		{
			return false;
		}
		return RectTransformUtility.RectangleContainsScreenPoint(_panel.GetComponent<RectTransform>(), ((InputControl<Vector2>)(object)((Pointer)current).position).ReadValue(), (Camera)null);
	}

	private static string Clock(double seconds)
	{
		int num = Math.Max(0, (int)Math.Round(seconds));
		if (num >= 3600)
		{
			return $"{num / 3600}:{num / 60 % 60:00}:{num % 60:00}";
		}
		return $"{num / 60}:{num % 60:00}";
	}

	public static void Update()
	{
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		HUDManager instance = HUDManager.Instance;
		if ((Object)(object)instance == (Object)null || ((Object)(object)_panel == (Object)null && !Build(instance)))
		{
			return;
		}
		(string title, string artist, bool playing) tuple = Radio.NowPlaying();
		string item = tuple.title;
		string item2 = tuple.artist;
		bool item3 = tuple.playing;
		string value = Radio.PanelMode.Value;
		bool flag = !value.Equals("Off", StringComparison.OrdinalIgnoreCase) && (!value.Equals("SongChange", StringComparison.OrdinalIgnoreCase) || Time.unscaledTime < _visibleUntil || Hovered()) && !string.IsNullOrEmpty(item) && !RadioMenu.IsOpen;
		if (flag && !_panel.activeSelf)
		{
			_group.alpha = 0f;
			_panel.SetActive(true);
		}
		_group.alpha = Mathf.MoveTowards(_group.alpha, flag ? 1f : 0f, Time.unscaledDeltaTime / 0.25f);
		CanvasGroup val = _group;
		bool blocksRaycasts = (_group.interactable = flag);
		val.blocksRaycasts = blocksRaycasts;
		if (!flag)
		{
			if (_group.alpha <= 0f && _panel.activeSelf)
			{
				_panel.SetActive(false);
			}
			return;
		}
		((TMP_Text)_line1).text = item;
		string text = Radio.StationLabel(Radio.Current);
		((TMP_Text)_line2).text = (string.IsNullOrEmpty(item2) ? text : (item2 + "   |   " + text));
		((TMP_Text)_play.Text).text = (item3 ? "Pause" : "Play");
		(double pos, double len) tuple2 = Radio.Progress();
		double item4 = tuple2.pos;
		double item5 = tuple2.len;
		bool flag3 = item5 > 0.0;
		if (_progress.activeSelf != flag3)
		{
			_progress.SetActive(flag3);
		}
		if (flag3)
		{
			_fill.anchorMax = new Vector2(Mathf.Clamp01((float)(item4 / item5)), 1f);
			((TMP_Text)_elapsed).text = Clock(item4);
			((TMP_Text)_remaining).text = "-" + Clock(item5 - item4);
		}
		if (Time.unscaledTime >= _nextVolumeSync)
		{
			_nextVolumeSync = Time.unscaledTime + 0.5f;
			float num = Radio.StationVolume();
			bool flag4 = num >= 0f;
			((Selectable)_slider).interactable = flag4;
			_sliderGroup.alpha = (flag4 ? 1f : 0.35f);
			if (flag4)
			{
				_slider.SetValueWithoutNotify(num);
			}
		}
	}

	private static bool Build(HUDManager hud)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected Obj, but got Unknown
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Expected Obj, but got Unknown
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Expected Obj, but got Unknown
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Expected Obj, but got Unknown
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		if (!UiKit.Init(hud))
		{
			return false;
		}
		_panel = new GameObject("OTH_NowPlaying");
		_panel.transform.SetParent(((Component)hud).transform, false);
		RectTransform val = _panel.AddComponent<RectTransform>();
		Vector2 anchorMin = (val.anchorMax = new Vector2(0f, 0f));
		val.anchorMin = anchorMin;
		val.pivot = new Vector2(0f, 0f);
		val.sizeDelta = new Vector2(400f, 126f);
		val.anchoredPosition = new Vector2(78f, 228f);
		CanvasGroup val3 = (_group = _panel.AddComponent<CanvasGroup>());
		val3.ignoreParentGroups = true;
		val3.interactable = true;
		val3.blocksRaycasts = true;
		UiKit.Place(UiKit.Glass(hud, _panel.transform), 0f, 0f, 400f, 126f);
		Transform transform = _panel.transform;
		_line1 = UiKit.Text(transform, "", 19f, 14f, 8f, 372f, 26f, (TextAlignmentOptions)4097, 1f, 0f);
		_line2 = UiKit.Text(transform, "", 14f, 14f, 33f, 372f, 20f, (TextAlignmentOptions)4097, 0.7f, 0f);
		_progress = UiKit.Group(transform, "Progress");
		float num = 58f;
		_elapsed = UiKit.Text(_progress.transform, "", 13f, 14f, num, 44f, 18f, (TextAlignmentOptions)4097, 0.75f, 0f);
		_remaining = UiKit.Text(_progress.transform, "", 13f, 338f, num, 48f, 18f, (TextAlignmentOptions)4100, 0.75f, 0f);
		GameObject val4 = new GameObject("Bar");
		val4.transform.SetParent(_progress.transform, false);
		val4.AddComponent<RectTransform>();
		UiKit.Place(val4, 62f, num + 6f, 272f, 6f);
		((Graphic)UiKit.Shape(val4).GetComponent<Image>()).color = UiKit.Idle;
		GameObject val5 = UiKit.Shape(val4);
		((Graphic)val5.GetComponent<Image>()).color = UiKit.Cream;
		_fill = val5.GetComponent<RectTransform>();
		float y = 84f;
		float h = 30f;
		float num2 = 6f;
		UiKit.Pill(transform, "<<", 15f, 14f, y, 44f, h, Radio.Previous);
		_play = UiKit.Pill(transform, "Pause", 14f, 58f + num2, y, 64f, h, Radio.PlayPause);
		UiKit.Pill(transform, ">>", 15f, 58f + num2 + 64f + num2, y, 44f, h, Radio.Next);
		float num3 = 58f + num2 + 64f + num2 + 44f + 16f;
		UiKit.Text(transform, "Vol", 13f, num3, y, 30f, h, (TextAlignmentOptions)4097, 0.7f, 0f);
		BuildSlider(transform, num3 + 34f, y, 386f - (num3 + 34f), h);
		_panel.SetActive(false);
		return true;
	}

	private static void BuildSlider(Transform parent, float x, float y, float w, float h)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Expected Obj, but got Unknown
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Expected Obj, but got Unknown
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject("Volume");
		val.transform.SetParent(parent, false);
		val.AddComponent<RectTransform>();
		UiKit.Place(val, x, y, w, h);
		_sliderGroup = val.AddComponent<CanvasGroup>();
		GameObject val2 = UiKit.Shape(val);
		RectTransform component = val2.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 0.5f);
		component.anchorMax = new Vector2(1f, 0.5f);
		component.sizeDelta = new Vector2(0f, 8f);
		component.anchoredPosition = Vector2.zero;
		((Graphic)val2.GetComponent<Image>()).color = UiKit.Idle;
		GameObject val3 = new GameObject("Fill Area");
		val3.transform.SetParent(val.transform, false);
		RectTransform val4 = val3.AddComponent<RectTransform>();
		val4.anchorMin = new Vector2(0f, 0.5f);
		val4.anchorMax = new Vector2(1f, 0.5f);
		val4.sizeDelta = new Vector2(0f, 8f);
		GameObject val5 = UiKit.Shape(val3);
		((Graphic)val5.GetComponent<Image>()).color = UiKit.Cream;
		((Graphic)val5.GetComponent<Image>()).raycastTarget = false;
		GameObject val6 = new GameObject("Handle Slide Area");
		val6.transform.SetParent(val.transform, false);
		RectTransform val7 = val6.AddComponent<RectTransform>();
		UiKit.Stretch(val7);
		val7.offsetMin = new Vector2(7f, 0f);
		val7.offsetMax = new Vector2(-7f, 0f);
		GameObject val8 = UiKit.Shape(val6);
		RectTransform component2 = val8.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 0f);
		component2.anchorMax = new Vector2(0f, 1f);
		component2.sizeDelta = new Vector2(14f, -6f);
		Image component3 = val8.GetComponent<Image>();
		((Graphic)component3).color = UiKit.Cream;
		((Graphic)component3).raycastTarget = true;
		((Graphic)val.AddComponent<Image>()).color = new Color(0f, 0f, 0f, 0.001f);
		_slider = val.AddComponent<Slider>();
		_slider.fillRect = val5.GetComponent<RectTransform>();
		_slider.handleRect = component2;
		((Selectable)_slider).targetGraphic = (Graphic)(object)component3;
		_slider.direction = (Direction)0;
		_slider.minValue = 0f;
		_slider.maxValue = 1f;
		((Selectable)_slider).transition = (Transition)0;
		((UnityEvent<float>)(object)_slider.onValueChanged).AddListener(DelegateSupport.ConvertDelegate<UnityAction<float>>((Delegate)new Action<float>(Radio.SetStationVolume)));
	}
}
