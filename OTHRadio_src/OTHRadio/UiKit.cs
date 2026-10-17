using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OTHRadio;

internal static class UiKit
{
	public class Btn
	{
		public GameObject Root;

		public GameObject Highlight;

		public TextMeshProUGUI Text;
	}

	public static readonly Color Cream = new Color(1f, 0.976f, 0.933f, 1f);

	public static readonly Color Dark = new Color(0.075f, 0.071f, 0.071f, 1f);

	public static readonly Color Idle = new Color(0.08f, 0.09f, 0.1f, 0.55f);

	private static TMP_Text _font;

	private static GameObject _pill;

	public static bool Init(HUDManager hud)
	{
		if ((Object)(object)hud == (Object)null || (Object)(object)hud.engineRPMDisplay == (Object)null || (Object)(object)hud.drivingModeSelector == (Object)null)
		{
			return false;
		}
		ContextUIDrivingModeSelector drivingModeSelector = hud.drivingModeSelector;
		_font = (TMP_Text)(object)(((Object)(object)drivingModeSelector.Text4L != (Object)null) ? drivingModeSelector.Text4L : drivingModeSelector.Text4H);
		_pill = (((Object)(object)drivingModeSelector.Selector4H != (Object)null) ? ((Component)drivingModeSelector.Selector4H).gameObject : null);
		return true;
	}

	public static void SetSelected(Btn b, bool on)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (b.Highlight.activeSelf != on)
		{
			b.Highlight.SetActive(on);
		}
		((Graphic)b.Text).color = (on ? Dark : Cream);
	}

	public static Btn Pill(Transform parent, string label, float size, float x, float y, float w, float h, Action onClick)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject("Pill");
		val.transform.SetParent(parent, false);
		val.AddComponent<RectTransform>();
		Place(val, x, y, w, h);
		Image component = Shape(val).GetComponent<Image>();
		((Graphic)component).raycastTarget = onClick != null;
		GameObject val2 = Shape(val);
		((Graphic)val2.GetComponent<Image>()).color = Cream;
		((Graphic)val2.GetComponent<Image>()).raycastTarget = false;
		val2.SetActive(false);
		TextMeshProUGUI text = Text(val.transform, label, size, 0f, 0f, 0f, 0f, (TextAlignmentOptions)514, 1f, 0f, stretch: true);
		if (onClick != null)
		{
			((Graphic)component).color = Color.white;
			Button val3 = val.AddComponent<Button>();
			((Selectable)val3).targetGraphic = (Graphic)(object)component;
			ColorBlock colors = ((Selectable)val3).colors;
			colors.normalColor = Idle;
			colors.highlightedColor = new Color(0.22f, 0.24f, 0.27f, 0.75f);
			colors.pressedColor = new Color(0.32f, 0.34f, 0.37f, 0.85f);
			colors.selectedColor = Idle;
			colors.colorMultiplier = 1f;
			colors.fadeDuration = 0.08f;
			((Selectable)val3).colors = colors;
			((UnityEvent)val3.onClick).AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Delegate)onClick));
		}
		else
		{
			((Graphic)component).color = Idle;
		}
		return new Btn
		{
			Root = val,
			Highlight = val2,
			Text = text
		};
	}

	public static GameObject Shape(GameObject parent)
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected Obj, but got Unknown
		GameObject val;
		if ((Object)(object)_pill != (Object)null)
		{
			val = Object.Instantiate<GameObject>(_pill, parent.transform);
			foreach (Transform componentsInChild in val.GetComponentsInChildren<Transform>(true))
			{
				if ((Object)(object)componentsInChild != (Object)(object)val.transform)
				{
					Object.DestroyImmediate((Object)(object)((Component)componentsInChild).gameObject);
				}
			}
			val.SetActive(true);
		}
		else
		{
			val = new GameObject("Shape");
			val.transform.SetParent(parent.transform, false);
			val.AddComponent<RectTransform>();
			val.AddComponent<Image>();
		}
		Stretch(val.GetComponent<RectTransform>());
		return val;
	}

	public static GameObject Glass(HUDManager hud, Transform parent)
	{
		ContextUIEngineRPMDisplay engineRPMDisplay = hud.engineRPMDisplay;
		HashSet<string> hashSet = new HashSet<string>();
		if (engineRPMDisplay.Pips != null)
		{
			for (int i = 0; i < engineRPMDisplay.Pips.Count; i++)
			{
				if ((Object)(object)engineRPMDisplay.Pips[i] != (Object)null)
				{
					hashSet.Add(((Object)((Component)engineRPMDisplay.Pips[i]).gameObject).name);
				}
			}
		}
		GameObject val = Object.Instantiate<GameObject>(((Component)engineRPMDisplay).gameObject, parent);
		((Object)val).name = "Glass";
		foreach (MonoBehaviour component in val.GetComponents<MonoBehaviour>())
		{
			if (!((Object)(object)component == (Object)null) && !((Object)(object)((Il2CppObjectBase)component).TryCast<Graphic>() != (Object)null) && !((Object)(object)((Il2CppObjectBase)component).TryCast<Mask>() != (Object)null) && !((Object)(object)((Il2CppObjectBase)component).TryCast<RectMask2D>() != (Object)null))
			{
				Object.DestroyImmediate((Object)(object)component);
			}
		}
		List<GameObject> list = new List<GameObject>();
		foreach (Transform componentsInChild in val.GetComponentsInChildren<Transform>(true))
		{
			if ((Object)(object)componentsInChild != (Object)(object)val.transform && (hashSet.Contains(((Object)((Component)componentsInChild).gameObject).name) || (Object)(object)((Component)componentsInChild).GetComponent<TMP_Text>() != (Object)null))
			{
				list.Add(((Component)componentsInChild).gameObject);
			}
		}
		foreach (GameObject item in list)
		{
			if ((Object)(object)item != (Object)null)
			{
				Object.DestroyImmediate((Object)(object)item);
			}
		}
		foreach (Graphic componentsInChild2 in val.GetComponentsInChildren<Graphic>(true))
		{
			componentsInChild2.raycastTarget = false;
		}
		val.SetActive(true);
		val.transform.SetAsFirstSibling();
		return val;
	}

	public static void Section(Transform parent, string title, float x, float y, float w)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Expected Obj, but got Unknown
		Text(parent, title, 15f, x, y, w, 26f, (TextAlignmentOptions)4097, 0.65f, 6f);
		GameObject val = new GameObject("Divider");
		val.transform.SetParent(parent, false);
		val.AddComponent<RectTransform>();
		Image val2 = val.AddComponent<Image>();
		Color cream = Cream;
		cream.a = 0.12f;
		((Graphic)val2).color = cream;
		((Graphic)val2).raycastTarget = false;
		Place(val, x, y + 28f, w, 2f);
	}

	public static TextMeshProUGUI Text(Transform parent, string text, float size, float x, float y, float w, float h, TextAlignmentOptions align, float alpha, float spacing, bool stretch = false)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject("Text");
		val.transform.SetParent(parent, false);
		RectTransform rt = val.AddComponent<RectTransform>();
		if (stretch)
		{
			Stretch(rt);
		}
		else
		{
			Place(val, x, y, w, h);
		}
		TextMeshProUGUI val2 = val.AddComponent<TextMeshProUGUI>();
		if ((Object)(object)_font != (Object)null)
		{
			((TMP_Text)val2).font = _font.font;
			((TMP_Text)val2).fontSharedMaterial = _font.fontSharedMaterial;
		}
		((TMP_Text)val2).enableWordWrapping = false;
		((TMP_Text)val2).overflowMode = (TextOverflowModes)1;
		((Graphic)val2).raycastTarget = false;
		((TMP_Text)val2).alignment = align;
		((TMP_Text)val2).characterSpacing = spacing;
		((TMP_Text)val2).enableAutoSizing = true;
		((TMP_Text)val2).fontSizeMin = Math.Min(12f, size);
		((TMP_Text)val2).fontSizeMax = size;
		((TMP_Text)val2).margin = (Vector4)(stretch ? new Vector4(10f, 0f, 10f, 0f) : Vector4.zero);
		Color cream = Cream;
		cream.a = alpha;
		((Graphic)val2).color = cream;
		((TMP_Text)val2).text = text;
		return val2;
	}

	public static GameObject Group(Transform parent, string name)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected Obj, but got Unknown
		GameObject val = new GameObject(name);
		val.transform.SetParent(parent, false);
		Stretch(val.AddComponent<RectTransform>());
		return val;
	}

	public static void Place(GameObject go, float x, float y, float w, float h)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		RectTransform component = go.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = new Vector2(0f, 1f));
		component.anchorMin = anchorMin;
		component.pivot = new Vector2(0f, 1f);
		component.sizeDelta = new Vector2(w, h);
		component.anchoredPosition = new Vector2(x, 0f - y);
		((Transform)component).localScale = Vector3.one;
		((Transform)component).localRotation = Quaternion.identity;
	}

	public static void Stretch(RectTransform rt)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		Vector2 offsetMin = (rt.offsetMax = Vector2.zero);
		rt.offsetMin = offsetMin;
		((Transform)rt).localScale = Vector3.one;
		((Transform)rt).localRotation = Quaternion.identity;
	}

	static UiKit()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
	}
}
