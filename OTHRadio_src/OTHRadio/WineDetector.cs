using System;
using System.Runtime.InteropServices;

namespace OTHRadio;

internal static class WineDetector
{
	private static bool? _cached;

	// Wine and Proton export wine_get_version from ntdll; Windows does not.
	// NativeLibrary (net6+) instead of DllImport("kernel32") so this answers
	// "false" instead of throwing DllNotFoundException on a non-Wine Linux host.
	public static bool IsRunningUnderWine()
	{
		if (!_cached.HasValue)
		{
			bool found = false;
			try
			{
				if (NativeLibrary.TryLoad("ntdll", out IntPtr h) || NativeLibrary.TryLoad("ntdll.dll", out h))
				{
					found = NativeLibrary.TryGetExport(h, "wine_get_version", out _);
				}
			}
			catch
			{
				found = false;
			}
			_cached = found;
		}
		return _cached.Value;
	}
}
