using System;
using System.Runtime.InteropServices;

namespace OTHRadio;

internal static class WineDetector
{
	private static bool? _cached;

	// Wine and Proton export wine_get_version from ntdll; Windows does not.
	public static bool IsRunningUnderWine()
	{
		if (!_cached.HasValue)
		{
			_cached = GetProcAddress(LoadLibrary("ntdll"), "wine_get_version") != IntPtr.Zero;
		}
		return _cached.Value;
	}

	[DllImport("kernel32", SetLastError = true)]
	private static extern IntPtr LoadLibrary(string fileName);

	[DllImport("kernel32", SetLastError = true)]
	private static extern IntPtr GetProcAddress(IntPtr module, string procName);
}
