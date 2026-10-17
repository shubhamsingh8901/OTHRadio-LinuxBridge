using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace OTHRadio;

internal static class MenuInput
{
	internal static void Tick()
	{
		try
		{
			EventSystem current = EventSystem.current;
			GameObject val = (((Object)(object)current != (Object)null) ? current.currentSelectedGameObject : null);
			if ((Object)(object)val == (Object)null)
			{
				return;
			}
			Mouse current2 = Mouse.current;
			if (current2 != null && current2.leftButton.isPressed)
			{
				return;
			}
			Transform val2 = val.transform;
			while ((Object)(object)val2 != (Object)null)
			{
				if (((Object)val2).name.StartsWith("OTH_"))
				{
					current.SetSelectedGameObject((GameObject)null);
					break;
				}
				val2 = val2.parent;
			}
		}
		catch
		{
		}
	}
}
