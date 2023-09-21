using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

using KKAPI;
using KKAPI.Utilities;
using KKAPI.Maker.UI;
using KKAPI.Chara;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using ExtensibleSaveFormat;
using KoiClothesOverlayX;

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
	// Tell BepInEx that we need Overlay to run, and that we need the latest version of it.
	// Check documentation of KoikatuAPI.VersionConst for more info.
	[BepInDependency(KoiClothesOverlayX.KoiClothesOverlayMgr.GUID, BepInDependency.DependencyFlags.SoftDependency)]
	public partial class FashionLine_Core : BaseUnityPlugin
	{
		public static FashionLine_Core Instance;
		public const string ModName = "Fashion Line";
		public const string GUID = "prolo.fashionline";//never change this
		public const string Description = "Adds the ability to save coordinate cards to a character card and use them (Why was this not a thing?)";
		public const string Version = "0.1.0";

		internal static new ManualLogSource Logger;

		internal static bool KoiOverlayModExists = false;
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

			//Hiden
			public ConfigEntry<string> lastCoordDir;

		}

		void Awake()
		{
			Instance = this;
			Logger = base.Logger;
			ForeGrounder.SetCurrentForground();

			//Info.Metadata;
			var data = (KoiClothesOverlayMgr)FindObjectOfType(typeof(KoiClothesOverlayMgr));
			KoiOverlayModExists = data == null ? false :
			data.Info.Metadata.Version >= new Version(KoiClothesOverlayMgr.Version);

			if(KoiOverlayModExists)
				Logger.LogInfo("Koi Overlay Mod Exists!!!!!!!!");
			else
				Logger.LogInfo("Koi Overlay Mod Does Not Exist!!!!!!!!");

			string main = "";
			string adv = "Advanced";

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
				resetOnLaunch = Config.Bind(adv, "Reset On Launch", true, new ConfigDescription("", null,
				new ConfigurationManagerAttributes() { Order = index--, IsAdvanced = true })),

				//Hiden
				lastCoordDir = Config.Bind(adv, "Last Coord Dir.", "", new ConfigDescription("", null,
				new ConfigurationManagerAttributes() { Order = index--, Browsable = false, IsAdvanced = true })),

			};

			//Advanced
			{
				cfg.debug = Config.Bind(adv, "Log Debug", false, new ConfigDescription("", null,
				new ConfigurationManagerAttributes() { Order = index-- , IsAdvanced = true })).ConfigDefaulter();
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

		public static PluginData SaveExtData(this FashionLineController ctrl)
		{
			//	FashionLine_Core.Logger.LogMessage("Saved Extended Data!");
			return saveload.Save(ctrl);
		}
		public static PluginData LoadExtData(this FashionLineController ctrl, PluginData data = null)
		{
			//FashionLine_Core.Logger.LogMessage("Loaded Extended Data!");
			return saveload.Load(ctrl, data);
		}

		/// <summary>
		/// Crates Image Texture based on path
		/// </summary>
		/// <param name="path">directory path to image (i.e. C:/path/to/image.png)</param>
		/// <param name="data">raw image data that will be read instead of path if not null or empty</param>
		/// <returns>An Texture2D created from path if passed, else a black texture</returns>
		public static Texture2D CreateTexture(this string path, byte[] data = null) =>
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

		public static GameObject ScaleToParent2D(this GameObject obj, bool width = true, bool height = true)
		{
			RectTransform rectTrans = null;

			rectTrans = obj.GetComponent<RectTransform>();

			if(rectTrans == null) return obj;

			//var rectTrans = par.GetComponent<RectTransform>();
			rectTrans.anchorMin = new Vector2(
				width ? 0 : rectTrans.anchorMin.x,
				height ? 0 : rectTrans.anchorMin.y);
			rectTrans.anchorMax = new Vector2(
				width ? 1 : rectTrans.anchorMax.x,
				height ? 1 : rectTrans.anchorMax.y);
			rectTrans.offsetMin = new Vector2(
				width ? 0 : rectTrans.offsetMin.x,
				height ? 0 : rectTrans.offsetMin.y);
			rectTrans.offsetMax = new Vector2(
				width ? 0 : rectTrans.offsetMax.x,
				height ? 0 : rectTrans.offsetMax.y);
			rectTrans.localPosition = Vector3.zero;
			//rectTrans.pivot = new Vector2(0.5f, 0.5f);

			return obj;
		}

		public static T ScaleToParent2D<T>(this T comp, bool width = true, bool height = true)
		{

			if(comp is Component)
				((Component)(object)comp).gameObject.ScaleToParent2D(width: width, height: height);

			return comp;
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

		/// <summary>
		/// makes sure a path fallows the format "this/is/a/path" and not "this//is\\a/path" or similar
		/// </summary>
		/// <param name="dir"></param>
		/// <returns></returns>
		public static string MakeDirPath(this string dir, string oldslash = "\\", string newslash = "/")
		{

			dir = (dir ?? "").Trim().Replace(oldslash, newslash).Replace(newslash + newslash, newslash);

			if((dir.LastIndexOf('.') < dir.LastIndexOf(newslash))
				&& dir.Substring(dir.Length - newslash.Length) != newslash)
				dir += newslash;

			return dir;
		}

		/// <summary>
		/// Returns a list of the regestered handeler specified. returns empty list otherwise 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static IEnumerable<T> GetChaFuncCtrlOfType<T>()
		{
			foreach(var hnd in CharacterApi.RegisteredHandlers)
				if(hnd.ControllerType == typeof(T))
					return hnd.Instances.Cast<T>();

			return new T[] { };
		}


	}

	/// <summary>
	/// utility to bring process to foreground (used for the file select)
	/// </summary>
	public class ForeGrounder
	{
		static IntPtr ptr = IntPtr.Zero;

		/// <summary>
		/// set window to go back to
		/// </summary>
		public static void SetCurrentForground()
		{
			ptr = GetActiveWindow();

			//	MorphUtil.Logger.LogDebug($"Process ptr 1 set to: {ptr}");
		}

		/// <summary>
		/// reverts back to last window specified by SetCurrentForground
		/// </summary>
		public static void RevertForground()
		{
			//	MorphUtil.Logger.LogDebug($"process ptr: {ptr}");

			if(ptr != IntPtr.Zero)
				SwitchToThisWindow(ptr, true);
		}

		public static IntPtr GetForgroundHandeler() => ptr;

		[DllImport("user32.dll")]
		static extern IntPtr GetActiveWindow();
		[DllImport("user32.dll")]
		static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
	}

}
