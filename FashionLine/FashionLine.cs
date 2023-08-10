using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KKAPI;
using BepInEx;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using KKAPI.Chara;

namespace FashionLine
{
	// Specify this as a plugin that gets loaded by BepInEx
	[BepInPlugin(GUID, ModName, Version)]
	// Tell BepInEx that we need KKAPI to run, and that we need the latest version of it.
	// Check documentation of KoikatuAPI.VersionConst for more info.
	[BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
	// Tell BepInEx that we need ExtendedSave to run, and that we need the latest version of it.
	// Check documentation of KoikatuAPI.VersionConst for more info.
	[BepInDependency(ExtensibleSaveFormat.ExtendedSave.GUID, ExtensibleSaveFormat.ExtendedSave.Version)]
	public partial class FashionLine_Core : BaseUnityPlugin
	{
		public static FashionLine_Core Instance;
		public const string ModName = "Fashion Line";
		public const string GUID = "prolo.fashionline";//never change this
		public const string Version = "0.1.0";

		internal static new ManualLogSource Logger;


		public static FashionLineConfig cfg;
		public class FashionLineConfig { }

		void Awake()
		{
			Instance = this;
			Logger = base.Logger;



			CharacterApi.RegisterExtraBehaviour<FashionLineController>(GUID);
		}
	}
}
