// Ignore Spelling: saveload

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KKAPI;
using KKAPI.Utilities;
using BepInEx;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using KKAPI.Chara;
using BepInEx.Configuration;
using KKAPI.Maker.UI;
using System.Collections;
using UnityEngine.Events;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine.UI;

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
		public const string Description = "Adds the ability to save coordinate cards to a character card and use them (Why was this not a thing?)";
		public const string Version = "0.1.0";

		internal static new ManualLogSource Logger;


		public static FashionLineConfig cfg;
		public struct FashionLineConfig
		{
			//Main
			public ConfigEntry<bool> enable;
			public ConfigEntry<KeyboardShortcut> prevInLine;
			public ConfigEntry<KeyboardShortcut> nextInLine;

			//Advanced
			public ConfigEntry<bool> resetOnLaunch;
			public ConfigEntry<bool> debug;
		}

		void Awake()
		{
			Instance = this;
			Logger = base.Logger;

			string main = "\0\0\0\0\0\0Main";
			string adv = "\0\0\0\0\0Advanced";

			int index = 0;
			cfg = new FashionLineConfig()
			{
				//main
				enable = Config.Bind(main, "Enable", true, new ConfigDescription("Alows the mod to do stuff", null,
				new ConfigurationManagerAttributes() { Order = index-- })),
				nextInLine = Config.Bind(main, "Next In Line", KeyboardShortcut.Empty,
				new ConfigDescription("Switch the current outfit with the next outfit in the list", null,
				new ConfigurationManagerAttributes() { Order = index--, })),
				prevInLine = Config.Bind(main, "Prev. In Line", KeyboardShortcut.Empty,
				new ConfigDescription("Switch the current outfit with the previous outfit in the list", null,
				new ConfigurationManagerAttributes() { Order = index--, })),

				//Advanced
				resetOnLaunch = Config.Bind(adv, "Reset On Launch", false, new ConfigDescription("", null,
				new ConfigurationManagerAttributes() { Order = index-- })),
			};

			//Advanced
			{
				cfg.debug = Config.Bind(adv, "Log Debug", false, new ConfigDescription("", null,
				new ConfigurationManagerAttributes() { Order = index-- })).ConfigDefaulter();
			}

			CharacterApi.RegisterExtraBehaviour<FashionLineController>(GUID);
			FashionLineGUI.Init();
		}
	}

	public static class FashionLine_Util
	{
		static FashionLine_Core Instance { get => FashionLine_Core.Instance; }
		static FashionLine_Core.FashionLineConfig cfg { get => FashionLine_Core.cfg; }
		public static readonly CurrentSaveLoadController saveload = new CurrentSaveLoadController();

		public static PluginData SaveExtData(this FashionLineController ctrl) => saveload.Save(ctrl);
		public static PluginData LoadExtData(this FashionLineController ctrl, PluginData data = null) => saveload.Load(ctrl, data);

		/// <summary>
		/// Crates Image Texture based on path
		/// </summary>
		/// <param name="path">directory path to image (i.e. C:/path/to/image.png)</param>
		/// <param name="data">raw image data that will be read instead of path if not null or empty</param>
		/// <returns>An Texture2D created from path if passed, else a black texture</returns>
		public static Texture2D CreateTexture(this string path, byte[] data = null, bool preferpath = false) =>
			(!data.IsNullOrEmpty() || !File.Exists(path)) ?
			data?.LoadTexture(TextureFormat.RGBA32) ?? Texture2D.blackTexture :
			File.ReadAllBytes(path)?.LoadTexture(TextureFormat.RGBA32) ??
			Texture2D.blackTexture;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gui"></param>
		/// <param name="act"></param>
		/// <returns></returns>
		public static BaseGuiEntry OnGUIExists(this BaseGuiEntry gui, UnityAction<BaseGuiEntry> act)
		{
			IEnumerator func(BaseGuiEntry gui1, UnityAction<BaseGuiEntry> act1)
			{
				if(!gui1.Exists)
					yield return new WaitUntil(() => gui1.Exists);//the thing neeeds to exist first

				act1(gui);

				yield break;
			}
			Instance.StartCoroutine(func(gui, act));

			return gui;
		}

		public static T FirstOrNull<T>(this IEnumerable<T> enu)
		{
			try
			{ return enu.Count() > 0 ? enu.First() : (T)(object)null; }
			catch { return (T)(object)null; }
		}     //I love loopholes 🤣
		public static T FirstOrNull<T>(this IEnumerable<T> enu, Func<T, bool> predicate)
		{
			try
			{ return enu.Count() > 0 ? enu.First(predicate) : (T)(object)null; }
			catch { return (T)(object)null; }
		}   //I love loopholes 🤣
		public static T LastOrNull<T>(this IEnumerable<T> enu)
		{
			try
			{ return enu.Count() > 0 ? enu.Last() : (T)(object)null; }
			catch { return (T)(object)null; }
		}     //I love loopholes 🤣
		public static T LastOrNull<T>(this IEnumerable<T> enu, Func<T, bool> predicate)
		{
			try
			{ return enu.Count() > 0 ? enu.Last(predicate) : (T)(object)null; }
			catch { return (T)(object)null; }
		}   //I love loopholes 🤣
		public static void ScaleToParent2D(this RectTransform rectTrans)
		{
			//var rectTrans = par.GetComponent<RectTransform>();
			rectTrans.anchorMin = Vector2.zero;
			rectTrans.anchorMax = Vector2.one;
			rectTrans.offsetMax = Vector2.zero;
			rectTrans.offsetMin = Vector2.zero;
			rectTrans.localPosition = Vector3.zero;
			rectTrans.pivot = new Vector2(0.5f, 0.5f);
		}
		public static IEnumerable<T> GetComponentsInChildren<T>(this GameObject obj, int depth) =>
			 obj.GetComponentsInChildren<T>().Attempt((v1) =>
			(((Component)(object)v1).transform.HierarchyLevelIndex() - obj.transform.HierarchyLevelIndex()) < (depth + 1) ?
			v1 : (T)(object)((T)(object)null).GetType());
		public static IEnumerable<T> GetComponentsInChildren<T>(this Component obj, int depth) =>
			obj.gameObject.GetComponentsInChildren<T>(depth);

		public static int HierarchyLevelIndex(this Transform obj) => obj.parent ? 1 + obj.parent.HierarchyLevelIndex() : 0;

		/// <summary>
		/// gets the text of the first Text or TMP_Text component in a game object or it's children.
		///  If no component return null. 
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public static string GetTextFromTextComponent(this GameObject obj) =>
			obj?.GetComponentInChildren<TMP_Text>()?.text ??
			obj?.GetComponentInChildren<Text>()?.text ?? null;

		/// <summary>
		/// Defaults the ConfigEntry on game launch
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="v1"></param>
		/// <param name="v2"></param>
		public static ConfigEntry<T> ConfigDefaulter<T>(this ConfigEntry<T> v1, T v2)
		{

			if(v1 == null || !FashionLine_Core.cfg.resetOnLaunch.Value) return v1;

			v1.Value = v2;
			v1.SettingChanged += (m, n) => { if(v2 != null) v2 = v1.Value; };
			return v1;
		}

		/// <summary>
		/// Defaults the ConfigEntry on game launch
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="v1"></param>
		/// <param name="v2"></param>
		public static ConfigEntry<T> ConfigDefaulter<T>(this ConfigEntry<T> v1) => v1.ConfigDefaulter((T)v1.DefaultValue);


	}
}
