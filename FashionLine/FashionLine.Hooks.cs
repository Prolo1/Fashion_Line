using System;
using System.Collections.Generic;
using System.Text;

#if HONEY_API
using AIChara;
using CharaCustom;
#else
using ChaCustom;
#endif
using Manager;

using HarmonyLib;
using System.IO;

namespace FashionLine
{
	public partial class FashionLine_Core
	{
		public static string LastCoordSaveLocation { get; private set; }
			= FashionLine_GUI.DefaultCoordDirectory;

		private static class Hooks
		{
			public static void Init()
			{
				Harmony.CreateAndPatchAll(typeof(Hooks), GUID);
				
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(ChaFileCoordinate), nameof(ChaFileCoordinate.SaveFile))]
#if HONEY_API
			[HarmonyPatch(typeof(CvsC_CreateCoordinateFile), nameof(CvsC_CreateCoordinateFile.CreateCoordinateFile))]
#endif
			static void OnCoordSave(string __0) =>
			GetLastSaveLocation(__0);

			static void GetLastSaveLocation(string path) =>
				LastCoordSaveLocation = path ?? LastCoordSaveLocation;

		}
	}
}
