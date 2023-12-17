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


using static FashionLine.FashionLine_Util;

using KK_Plugins.MaterialEditor;
#if HONEY_API
using AllBrowserFolders = BrowserFolders.AI_BrowserFolders;
#elif KKS
using AllBrowserFolders = BrowserFolders.KKS_BrowserFolders;
#endif

namespace FashionLine
{
	// Tell BepInEx that we need KKAPI to run, and that we need the latest version of it.
	// Check documentation of KoikatuAPI.VersionConst for more info.
	[BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
	// Tell BepInEx that we need ExtendedSave to run, and that we need the latest version of it.
	// Check documentation of KoikatuAPI.VersionConst for more info.
	[BepInDependency(ExtensibleSaveFormat.ExtendedSave.GUID, ExtensibleSaveFormat.ExtendedSave.Version)]

	// Tell BepInEx that we need MaterialEditor to run, and that we only need it if it's there.
	// Check documentation of KoikatuAPI.VersionConst for more info.
	[BepInDependency(MaterialEditorPlugin.PluginGUID, BepInDependency.DependencyFlags.SoftDependency)]

	// Tell BepInEx that we need Overlay to run, and that we only need it if it's there.
	// Check documentation of KoikatuAPI.VersionConst for more info.
	[BepInDependency(KoiClothesOverlayX.KoiClothesOverlayMgr.GUID, BepInDependency.DependencyFlags.SoftDependency)]

	// Tell BepInEx that we need MaterialEditor to run, and that we only need it if it's there.
	// Check documentation of KoikatuAPI.VersionConst for more info.
	[BepInDependency(AllBrowserFolders.Guid, BepInDependency.DependencyFlags.SoftDependency)]

	// Specify this as a plugin that gets loaded by BepInEx
	[BepInPlugin(GUID, ModName, Version)]
	public partial class FashionLine_Core : BaseUnityPlugin
	{
		public static FashionLine_Core Instance;
		public const string ModName = "Fashion Line";
		public const string GUID = "prolo.fashionline";//never change this
		public const string Description =
			"Adds the ability to save coordinate cards to a " +
			"character card and use them (Why was this not a thing?)";
		public const string Version = "0.2.0";

		internal static new ManualLogSource Logger;

		internal static DependencyInfo<KoiClothesOverlayMgr> KoiOverlayDependency;
		//	internal static DependencyInfo<AllBrowserFolders> BrowserfolderDependency;
		internal static DependencyInfo<MaterialEditorPlugin> MatEditerDependency;

		public static FashionLineConfig cfg;
		public struct FashionLineConfig
		{
			//Main
			public ConfigEntry<bool> enable;
			public ConfigEntry<bool> areCoordinatesPersistant;
			public ConfigEntry<KeyboardShortcut> prevInLine;
			public ConfigEntry<KeyboardShortcut> nextInLine;

			//Advanced
			public ConfigEntry<bool> resetOnLaunch;
			public ConfigEntry<bool> debug;
			public ConfigEntry<float> viewportUISpace;

			//Hiden
			public ConfigEntry<string> lastCoordDir;

		}

		void Awake()
		{
			Instance = this;
			Logger = base.Logger;
			ForeGrounder.SetCurrentForground();
			//Soft dependency variables
			{
				KoiOverlayDependency = new DependencyInfo<KoiClothesOverlayMgr>(new Version(KoiClothesOverlayMgr.Version));
				//	BrowserfolderDependency = new DependencyInfo<AllBrowserFolders>(new Version(AllBrowserFolders.Version));
				MatEditerDependency = new DependencyInfo<MaterialEditorPlugin>(new Version(MaterialEditorPlugin.PluginVersion));

				if(!KoiOverlayDependency.InTargetVersionRange)
					Logger.LogWarning($"Some functionality may be locked due to the " +
						$"absence of [{nameof(KoiOverlayDependency)}] " +
						$"or the use of an incorrect version\n" +
						$"{KoiOverlayDependency}");

				//if(!BrowserfolderDependency.InTargetVersionRange)
				//	Logger.LogWarning($"Some functionality may be locked due to the " +
				//			$"absence of [{nameof(BrowserfolderDependency)}] " +
				//			$"or the use of an incorrect version\n" +
				//			$"{BrowserfolderDependency}");

				if(!MatEditerDependency.InTargetVersionRange)
					Logger.LogWarning($"Some functionality may be locked due to the " +
							$"absence of [{nameof(MatEditerDependency)}] " +
							$"or the use of an incorrect version\n" +
							$"{MatEditerDependency}");

			}

			string main = "";
			string adv = "Advanced";

			int index = 0;
			cfg = new FashionLineConfig()
			{
				//main
				enable = Config.Bind(main, "Enable", true, new ConfigDescription("Alows the mod to do stuff", null,
				new ConfigurationManagerAttributes() { Order = index-- })),

				areCoordinatesPersistant = Config.Bind(main, "Is FashionLine Persistent", false,
				new ConfigDescription("changes if the current FashionLine will persist when changing characters in maker", null,
				new ConfigurationManagerAttributes() { Order = index-- })),

				prevInLine = Config.Bind(main, "Prev. In Line", KeyboardShortcut.Empty,
				new ConfigDescription("Switch the current outfit with the previous outfit in the list", null,
				new ConfigurationManagerAttributes() { Order = index--, })),
				nextInLine = Config.Bind(main, "Next In Line", KeyboardShortcut.Empty,
				new ConfigDescription("Switch the current outfit with the next outfit in the list", null,
				new ConfigurationManagerAttributes() { Order = index--, })),

				//Advanced (the rest are in seperate location)
				resetOnLaunch = Config.Bind(adv, "Reset On Launch", true, new ConfigDescription("When enabled, reset adv. values when the mod is launched", null,
				new ConfigurationManagerAttributes() { Order = index--, IsAdvanced = true })),

				//Hiden
				lastCoordDir = Config.Bind(adv, "Last Coord Dir.", "", new ConfigDescription("", null,
				new ConfigurationManagerAttributes() { Order = index--, Browsable = false, IsAdvanced = true })),

			};

			//Advanced
			{
				cfg.debug = Config.Bind(adv, "Log Debug", false, new ConfigDescription("View extra debug logs", null,
				new ConfigurationManagerAttributes() { Order = index--, IsAdvanced = true })).ConfigDefaulter();
				cfg.viewportUISpace = Config.Bind(adv, "Viewport UI Space", .52f, new ConfigDescription("Increase / decrease the Fashion Line viewport size ", new AcceptableValueRange<float>(0, 1),
				new ConfigurationManagerAttributes() { Order = index--, ShowRangeAsPercent = false, IsAdvanced = true })).ConfigDefaulter();
			}

			cfg.viewportUISpace.SettingChanged += (m, n) =>
			{
				FashionLine_GUI.template.ResizeCustomUIViewport();
			};

			IEnumerator KeyUpdate()
			{

				yield return new WaitWhile(() =>
				{
					var list = GetAllChaFuncCtrlOfType<FashionLineController>();
					if(cfg.nextInLine.Value.IsDown())
						foreach(var ctrl in list)
							ctrl.NextInLine();

					if(cfg.prevInLine.Value.IsDown())
						foreach(var ctrl in list)
							ctrl.PrevInLine();

					return true;
				});
			}

			StartCoroutine(KeyUpdate());
			CharacterApi.RegisterExtraBehaviour<FashionLineController>(GUID);
			Hooks.Init();
			FashionLine_GUI.Init();
		}
	}

	public class DependencyInfo<T> where T : BaseUnityPlugin
	{
		public DependencyInfo(Version minTargetVer = null, Version maxTargetVer = null)
		{
			plugin = (T)GameObject.FindObjectOfType(typeof(T));
			Exists = plugin != null;
			MinTargetVersion = minTargetVer ?? new Version();
			MaxTargetVersion = maxTargetVer ?? new Version();
			InTargetVersionRange = Exists &&
				((CurrentVersion = plugin?.Info.Metadata.Version
				?? new Version()) >= MinTargetVersion);

			if(maxTargetVer != null && maxTargetVer >= MinTargetVersion)
				InTargetVersionRange &= Exists && (CurrentVersion <= MaxTargetVersion);
		}

		/// <summary>
		/// plugin reference
		/// </summary>
		public readonly T plugin = null;
		/// <summary>
		/// does the mod exist
		/// </summary>
		public bool Exists { get; } = false;
		/// <summary>
		/// Current version matches or exceeds the min target mod version. 
		/// if a max is set it will also make sure the mod is within range.
		/// </summary>
		public bool InTargetVersionRange { get; } = false;
		/// <summary>
		/// min version this mod expects
		/// </summary>
		public Version MinTargetVersion { get; } = null;
		/// <summary>
		/// max version this mod expects
		/// </summary>
		public Version MaxTargetVersion { get; } = null;
		/// <summary>
		/// version that is actually downloaded in the game
		/// </summary>
		public Version CurrentVersion { get; } = null;

		public void PrintExistsMsg()
		{

		}

		public override string ToString()
		{
			return
				$"Plugin Name: {plugin?.Info.Metadata.Name ?? "Null"}\n" +
				$"Current version: {CurrentVersion?.ToString() ?? "Null"}\n" +
				$"Min Target Version: {MinTargetVersion}\n" +
				$"Max Target Version: {MaxTargetVersion}\n";
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

		public static bool InRange<T>(this IEnumerable<T> list, int index)
		=> index >= 0 && index < list.Count();


		/// <summary>
		/// 
		/// </summary>
		/// <param name="gui"></param>
		/// <param name="act"></param>
		/// <returns></returns>
		public static T OnGUIExists<T>(this T gui, UnityAction<T> act) where T : BaseGuiEntry
		{
			if(gui == null) return gui;

			IEnumerator func(T gui1, UnityAction<T> act1)
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

		public static GameObject ScaleToParent2D(this GameObject obj, float pwidth = 1, float pheight = 1, bool changewidth = true, bool changeheight = true)
		{
			RectTransform rectTrans = null;

			rectTrans = obj?.GetComponent<RectTransform>();

			if(rectTrans == null) return obj;

			//var rectTrans = par.GetComponent<RectTransform>();
			rectTrans.anchorMin = new Vector2(
				changewidth ? 0 + (1 - pwidth) : rectTrans.anchorMin.x,
				changeheight ? 0 + (1 - pheight) : rectTrans.anchorMin.y);
			rectTrans.anchorMax = new Vector2(
				changewidth ? 1 - (1 - pwidth) : rectTrans.anchorMax.x,
				changeheight ? 1 - (1 - pheight) : rectTrans.anchorMax.y);

			rectTrans.localPosition = Vector3.zero;//The location of this line matters

			rectTrans.offsetMin = new Vector2(
				changewidth ? 0 : rectTrans.offsetMin.x,
				changeheight ? 0 : rectTrans.offsetMin.y);
			rectTrans.offsetMax = new Vector2(
				changewidth ? 0 : rectTrans.offsetMax.x,
				changeheight ? 0 : rectTrans.offsetMax.y);
			//rectTrans.pivot = new Vector2(0.5f, 0.5f);

			return obj;
		}

		public static T ScaleToParent2D<T>(this T comp, float pwidth = 1, float pheight = 1, bool width = true, bool height = true) where T : Component
		{
			comp?.gameObject.ScaleToParent2D(pwidth: pwidth, pheight: pheight, changewidth: width, changeheight: height);
			return comp;
		}

		public static IEnumerable<T> GetComponentsInChildren<T>(this GameObject obj, int depth) =>
			 obj.GetComponentsInChildren<T>().Attempt((v1) =>
			(((Component)(object)v1).transform.HierarchyLevelIndex() - obj.transform.HierarchyLevelIndex()) < (depth + 1) ?
			v1 : (T)(object)((T)(object)null).GetType());
		public static IEnumerable<T> GetComponentsInChildren<T>(this Component obj, int depth) =>
			obj.gameObject.GetComponentsInChildren<T>(depth);

		public static int HierarchyLevelIndex(this Transform obj) => obj.parent ? obj.parent.HierarchyLevelIndex() + 1 : 0;
		public static int HierarchyLevelIndex(this GameObject obj) => obj.transform.HierarchyLevelIndex();

		public static Component GetTextComponent(this GameObject obj)
		{
			return (Component)obj?.GetComponentInChildren<TMP_Text>() ??
			 obj?.GetComponentInChildren<Text>();
		}
		public static Component GetTextComponent(this Component obj)
		{
			return (Component)obj?.GetComponentInChildren<TMP_Text>() ??
			 obj?.GetComponentInChildren<Text>();
		}

		/// <summary>
		/// gets the text of the first Text or TMP_Text component in a game object or it's children.
		///  If no component return null. 
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public static string GetTextFromTextComponent(this GameObject obj)
			=>
			obj?.GetComponentInChildren<TMP_Text>()?.text ??
			obj?.GetComponentInChildren<Text>()?.text ?? null;

		/// <summary>
		/// sets the text of the first Text or TMP_Text component in a game object or it's children.
		///  If no component does nothing. 
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public static void SetTextFromTextComponent(this GameObject obj, string txt) =>
		((Component)obj?.GetComponentInChildren<TMP_Text>() ??
			obj?.GetComponentInChildren<Text>())?
			.SetTextFromTextComponent(txt);

		/// <summary>
		/// sets the text of the first Text or TMP_Text component in a game object or it's children.
		///  If no component does nothing. 
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public static void SetTextFromTextComponent(this Component obj, string txt)
		{
			Component comp;
			if(comp = obj?.GetComponentInChildren<TMP_Text>())
				((TMP_Text)comp).text = (txt);
			else if(comp = obj?.GetComponentInChildren<Text>())
				((Text)comp).text = (txt);
		}

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
		public static IEnumerable<T> GetAllChaFuncCtrlOfType<T>() where T : CharaCustomFunctionController
		{
			foreach(var hnd in CharacterApi.RegisteredHandlers)
				if(hnd.ControllerType == typeof(T))
					return hnd.Instances.Cast<T>();

			return new T[] { };
		}

		public static T AddToCustomGUILayout<T>(this T gui, bool horizontal = false, bool topUI = false, float horiScale = -1, float viewpercent = -1, bool newVertLine = false) where T : BaseGuiEntry
		{
			gui.OnGUIExists(g =>
			{
				Instance.StartCoroutine(g.AddToCustomGUILayoutCO
				(horizontal, topUI, horiScale, viewpercent, newVertLine));
			});
			return gui;
		}

		static Coroutine resizeco;
		public static void ResizeCustomUIViewport<T>(this T template, float viewpercent = -1) where T : BaseGuiEntry
		{
			if(viewpercent >= 0 && cfg.viewportUISpace.Value != viewpercent)
				cfg.viewportUISpace.Value = viewpercent;
			viewpercent = cfg.viewportUISpace.Value;

			if(template != null)
				template.OnGUIExists((gui) =>
				{
					IEnumerator func()
					{

						var ctrlObj = gui?.ControlObject;
						if(ctrlObj == null) yield break;

						yield return new WaitUntil(() =>
						ctrlObj?.GetComponentInParent<ScrollRect>() != null);

						var scrollRect = ctrlObj?.GetComponentInParent<ScrollRect>();

						var viewLE = scrollRect.viewport.GetOrAddComponent<LayoutElement>();
						float vHeight = Mathf.Abs(scrollRect.rectTransform.rect.height);
						viewLE.minHeight = vHeight * viewpercent;

						LayoutRebuilder.MarkLayoutForRebuild(scrollRect.rectTransform);
					}

					if(resizeco != null) Instance.StopCoroutine(resizeco);
					resizeco = Instance.StartCoroutine(func());
				});

		}

		static IEnumerator AddToCustomGUILayoutCO<T>(this T gui, bool horizontal = false, bool topUI = false, float horiScale = -1, float viewpercent = -1, bool newVertLine = false) where T : BaseGuiEntry
		{

			if(cfg.debug.Value) FashionLine_Core.Logger.LogDebug("moving object");

			yield return new WaitWhile(() => gui?.ControlObject?.GetComponentInParent<ScrollRect>()?.transform == null);

#if HONEY_API
			if(gui is MakerText)
			{
				var piv = (Vector2)gui.ControlObject?
					.GetComponentInChildren<Text>()?
					.rectTransform.pivot;
				piv.x = -.5f;
				piv.y = 1f;
			}
#endif

			var ctrlObj = gui.ControlObject;

			var scrollRect = ctrlObj.GetComponentInParent<ScrollRect>();
			var par = ctrlObj.GetComponentInParent<ScrollRect>().transform;


			if(cfg.debug.Value) FashionLine_Core.Logger.LogDebug("Parent: " + par);


			//setup VerticalLayoutGroup
			var vlg = scrollRect.gameObject.GetOrAddComponent<VerticalLayoutGroup>();

#if HONEY_API
			vlg.childAlignment = TextAnchor.UpperLeft;
#else
			vlg.childAlignment = TextAnchor.UpperCenter;
#endif
			var pad = 10;//(int)cfg.unknownTest.Value;//10
			vlg.padding = new RectOffset(pad, pad + 5, 0, 0);
			vlg.childControlWidth = true;
			vlg.childControlHeight = true;
			vlg.childForceExpandWidth = true;
			vlg.childForceExpandHeight = false;

			//This fixes the KOI_API rendering issue & enables scrolling over viewport (not elements tho)
			//Also a sizing issue in Honey_API
#if KOI_API
			scrollRect.GetComponent<Image>().sprite = scrollRect.content.GetComponent<Image>()?.sprite;
			scrollRect.GetComponent<Image>().color = (Color)scrollRect.content.GetComponent<Image>()?.color;


			scrollRect.GetComponent<Image>().enabled = true;
			scrollRect.GetComponent<Image>().raycastTarget = true;
			var img = scrollRect.content.GetComponent<Image>();
			if(!img)
				img = scrollRect.viewport.GetComponent<Image>();
			img.enabled = false;
#elif HONEY_API
			//		scrollRect.GetComponent<RectTransform>().sizeDelta =
			//		  scrollRect.transform.parent.GetComponentInChildren<Image>().rectTransform.sizeDelta;
#endif

			//Setup LayoutElements 
			scrollRect.verticalScrollbar.GetOrAddComponent<LayoutElement>().ignoreLayout = true;
			scrollRect.content.GetOrAddComponent<LayoutElement>().ignoreLayout = true;

			var viewLE = scrollRect.viewport.GetOrAddComponent<LayoutElement>();
			viewLE.layoutPriority = 1;
			viewLE.minWidth = -1;
			viewLE.flexibleWidth = -1;
			gui.ResizeCustomUIViewport(viewpercent);




			//Create  LayoutElement
			if(horizontal)
			{

				//Create Layout Element GameObject
				par = newVertLine ?
					GameObject.Instantiate<GameObject>(new GameObject("LayoutElement"), par)?.transform :
					par.GetComponentsInChildren<HorizontalLayoutGroup>(2)
					.LastOrNull((elem) => elem.GetComponent<HorizontalLayoutGroup>())?.transform.parent ??
					GameObject.Instantiate<GameObject>(new GameObject("LayoutElement"), par)?.transform;

				par = par.gameObject.GetOrAddComponent<RectTransform>().transform;//May need this line (I totally do)


				//calculate base GameObject sizeing

				var ele = par.GetOrAddComponent<LayoutElement>();
				ele.minWidth = -1;
				ele.minHeight = -1;
				ele.preferredHeight = Math.Max(ele?.preferredHeight ?? -1, ctrlObj.GetOrAddComponent<LayoutElement>()?.minHeight ?? ele?.preferredHeight ?? -1);
				ele.preferredWidth =
#if HONEY_API
				scrollRect.GetComponent<RectTransform>().rect.width;
#else
				//viewLE.minWidth;
				0;
#endif

				par.GetComponentInParent<VerticalLayoutGroup>().CalculateLayoutInputHorizontal();
				par.GetComponentInParent<VerticalLayoutGroup>().CalculateLayoutInputVertical();


				//Create and Set Horizontal Layout Settings

				par = par.GetComponentsInChildren<HorizontalLayoutGroup>(2)?
					.LastOrNull((elem) => elem.gameObject.GetComponent<HorizontalLayoutGroup>())?.transform ??
					GameObject.Instantiate<GameObject>(new GameObject("HorizontalLayoutGroup"), par)?.transform;
				par = par.gameObject.GetOrAddComponent<RectTransform>().transform;//May need this line (I totally do)

				var layout = par.GetOrAddComponent<HorizontalLayoutGroup>();


				layout.childControlWidth = true;
				layout.childControlHeight = true;
				layout.childForceExpandWidth = true;
				layout.childForceExpandHeight = true;
				layout.childAlignment = TextAnchor.MiddleCenter;

				par?.GetComponent<RectTransform>()?.ScaleToParent2D();
			}

			if(cfg.debug.Value) FashionLine_Core.Logger.LogDebug("setting as first/last");

			//remove extra LayoutElements
			var rList = ctrlObj.GetComponents<LayoutElement>();
			for(int a = 1; a < rList.Length; ++a)
				GameObject.DestroyImmediate(rList[a]);

			//Set this object's Layout settings
			ctrlObj.transform.SetParent(par, false);
			ctrlObj.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
			var apos = ctrlObj.GetComponent<RectTransform>().anchoredPosition; apos.x = 0;
			if(topUI)
				ctrlObj.transform.SetSiblingIndex
					(scrollRect.viewport.transform.GetSiblingIndex());
			else
				ctrlObj.transform.SetAsLastSibling();

			//if(ctrlObj.GetComponent<LayoutElement>())
			//	GameObject.Destroy(ctrlObj.GetComponent<LayoutElement>());
			var thisLE = ctrlObj.GetOrAddComponent<LayoutElement>();
			thisLE.layoutPriority = 5;
			thisLE.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

			if(thisLE.transform.childCount > 1)
			{
				var tmp = GameObject.Instantiate(new GameObject(), thisLE.transform);
				for(int a = 0; a < thisLE.transform.childCount; ++a)
					if(thisLE.transform.GetChild(a) != tmp.transform)
						thisLE.transform.GetChild(a--).SetParent(tmp.transform);

			}
			if(thisLE.transform.childCount > 0)
				thisLE.transform.GetChild(0).ScaleToParent2D();


			thisLE.flexibleWidth = -1;
			thisLE.flexibleHeight = -1;
			thisLE.minWidth = -1;
			//thisLE.minHeight = -1;

			thisLE.preferredWidth =
#if HONEY_API
				horizontal && horiScale > 0 ? par.GetComponent<RectTransform>().rect.width * horiScale : -1;
#else
			//	horizontal && horiScale > 0 ? viewLE.minWidth * horiScale : -1;
			0;
#endif
			//thisLE.preferredHeight = ctrlObj.GetComponent<RectTransform>().rect.height;


			//Reorder Scrollbar
			if(!topUI)
			{
				scrollRect.verticalScrollbar?.transform.SetAsLastSibling();
				scrollRect.horizontalScrollbar?.transform.SetAsLastSibling();
			}

			vlg.SetLayoutVertical();
			LayoutRebuilder.MarkLayoutForRebuild(scrollRect.GetComponent<RectTransform>());
			yield break;
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
