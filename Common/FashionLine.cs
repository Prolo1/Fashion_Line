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
using KKAPI.Studio;
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
using System.Reflection;
using static GUIDrawer;
using System.Threading;
using System.Runtime.InteropServices.ComTypes;
using HarmonyLib;



#if HONEY_API
using AllBrowserFolders = BrowserFolders.AI_BrowserFolders;
#elif KKS
using AllBrowserFolders = BrowserFolders.KKS_BrowserFolders;
#endif

namespace FashionLine
{

	#region dependencies
	[
	// Tell BepInEx that we need KKAPI to run, and that we need the latest version of it.
	// Check documentation of KoikatuAPI.VersionConst for more info.
	BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst),
	// Tell BepInEx that we need ExtendedSave to run, and that we need the latest version of it.
	// Check documentation of KoikatuAPI.VersionConst for more info.
	BepInDependency(ExtensibleSaveFormat.ExtendedSave.GUID, ExtensibleSaveFormat.ExtendedSave.Version),
	// Tell BepInEx that we need MaterialEditor to run, and that we only need it if it's there.
	// Check documentation of KoikatuAPI.VersionConst for more info.
	BepInDependency(MaterialEditorPlugin.PluginGUID, BepInDependency.DependencyFlags.SoftDependency),
	// Tell BepInEx that we need Overlay to run, and that we only need it if it's there.
	// Check documentation of KoikatuAPI.VersionConst for more info.
	BepInDependency(KoiClothesOverlayX.KoiClothesOverlayMgr.GUID, BepInDependency.DependencyFlags.SoftDependency),
	// Tell BepInEx that we need MaterialEditor to run, and that we only need it if it's there.
	// Check documentation of KoikatuAPI.VersionConst for more info.
	BepInDependency(AllBrowserFolders.Guid, BepInDependency.DependencyFlags.SoftDependency),
	]
	#endregion
	// Specify this as a plugin that gets loaded by BepInEx
	[BepInPlugin(GUID, ModName, Version)]
	public partial class FashionLine_Core : BaseUnityPlugin
	{
		public static FashionLine_Core Instance;
		public const string ModName = "Fashion Line";
		public const string GUID = "prolo.fashionline";//never change this
		public const string Description =
			@"Adds the ability to save coordinate cards to a " +
			@"character card and use them (Why was this not part of HS2/AI?¯\_(ツ)_/¯)";
		public const string Version = "0.3.0";

		internal static new ManualLogSource Logger;

		internal static DependencyInfo<KoiClothesOverlayMgr> KoiOverlayDependency;
		internal static DependencyInfo<MaterialEditorPlugin> MatEditerDependency;
		internal static DependencyInfo<AllBrowserFolders> BrowserfolderDependency;

		internal static Texture2D icon = null;
		internal static Texture2D UIGoku = null;
		internal static Texture2D iconBG = null;

		public static FashionLineConfig cfg;
		public struct FashionLineConfig
		{
			//Main
			public ConfigEntry<bool> enable;
			public ConfigEntry<bool> areCoordinatesPersistant;
			public ConfigEntry<KeyboardShortcut> prevInLine;
			public ConfigEntry<KeyboardShortcut> nextInLine;

			//Studio
			public ConfigEntry<bool> useCreatorDefaultBG;
			public ConfigEntry<bool> enableBGUI;
			public ConfigEntry<string> bgUIImagepath;

			//Advanced
			public ConfigEntry<bool> resetOnLaunch;
			public ConfigEntry<bool> debug;
			public ConfigEntry<float> viewportUISpace;
			public ConfigEntry<float> studioUIWidth;
			public ConfigEntry<Rect> studioWinRec;
			public ConfigEntry<Rect> studioSortOffset;

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
				MatEditerDependency = new DependencyInfo<MaterialEditorPlugin>(new Version(MaterialEditorPlugin.PluginVersion));
				BrowserfolderDependency = new DependencyInfo<AllBrowserFolders>(new Version(AllBrowserFolders.Version));

				if(!KoiOverlayDependency.InTargetVersionRange)
					Logger.LogWarning($"Some functionality may be locked due to the " +
						$"absence of [{nameof(KoiClothesOverlayMgr)}] " +
						$"or the use of an incorrect version\n" +
						$"{KoiOverlayDependency}");

				if(!MatEditerDependency.InTargetVersionRange)
					Logger.LogWarning($"Some functionality may be locked due to the " +
							$"absence of [{nameof(MaterialEditorPlugin)}] " +
							$"or the use of an incorrect version\n" +
							$"{MatEditerDependency}");

				//if(!BrowserfolderDependency.InTargetVersionRange)
				//	Logger.LogWarning($"Some functionality may be locked due to the " +
				//			$"absence of [{nameof(BrowserfolderDependency)}] " +
				//			$"or the use of an incorrect version\n" +
				//			$"{BrowserfolderDependency}");

			}

			//Embeded Resources
			{
				/**This stuff will be used later*/
				var assembly = Assembly.GetExecutingAssembly();
				var resources = assembly.GetManifestResourceNames();
				Logger.LogDebug($"\nResources:\n[{string.Join(", ", resources)}]");

				MemoryStream memStreme = new MemoryStream();
				var data = assembly.GetManifestResourceStream(resources.FirstOrDefault((txt) => txt.ToLower().Contains("ultra instinct.jpg")));
				data.CopyTo(memStreme);
				UIGoku =
					memStreme?.GetBuffer()?
					.LoadTexture();
				memStreme.SetLength(0);

				data = assembly.GetManifestResourceStream(resources.FirstOrDefault((txt) => txt.ToLower().Contains("icon.png")));
				data.CopyTo(memStreme);
				icon =
					memStreme?.GetBuffer()?
					.LoadTexture();
				memStreme.SetLength(0);
				icon.Compress(false);
				icon.Apply();

				data = assembly.GetManifestResourceStream(resources.FirstOrDefault((txt) => txt.ToLower().Contains("new icon.png")));
				data.CopyTo(memStreme);
				iconBG =
					memStreme?.GetBuffer()?
					.LoadTexture();
				memStreme.SetLength(0);
				iconBG.Compress(false);
				iconBG.Apply();



			}

			//Type Convertors
			{
				TomlTypeConverter.AddConverter(
				   typeof(Rect),
				   new TypeConverter()
				   {
					   ConvertToString = (o, t) =>
					   {
						   var rec = (Rect)o;

						   return string.Format("{0:f0}:{1:f0}:{2:f0}:{3:f0}", rec.x, rec.y, rec.width, rec.height);
					   },
					   ConvertToObject = (s, t) =>
					   {

						   var values = s.Split(':');

						   return new Rect(
							   float.Parse(values[0]),
							   float.Parse(values[1]),
							   float.Parse(values[2]),
							   float.Parse(values[3]));
					   },
				   });
			}
			int secIndex = 0;
			int secIndex2 = 99;
			string main = "";
			//string mainx =
			//$"{secIndex++:d2}. " + main;
			string stud = "Studio";
			string studx =
			$"{secIndex++:d2}. " + stud;
			string adv = "Advanced";
			string advx =
			$"{secIndex2--:d2}. " + adv;
			bool enableBGUI = true;
			int index = 0;
			cfg = new FashionLineConfig()
			{
				//main
				enable = Config.Bind(main, "Enable", true, new ConfigDescription("Alows the mod to do stuff", null,
				new ConfigurationManagerAttributes() { Order = index--, Category = main })),

				areCoordinatesPersistant = Config.Bind(main, "Is FashionLine Persistent", false,
				new ConfigDescription("changes if the current FashionLine will persist when changing characters in maker", null,
				new ConfigurationManagerAttributes() { Order = index--, Category = main })),

				prevInLine = Config.Bind(main, "Prev. In Line", KeyboardShortcut.Empty,
				new ConfigDescription("Switch the current outfit with the previous outfit in the list", null,
				new ConfigurationManagerAttributes() { Order = index--, Category = main })),
				nextInLine = Config.Bind(main, "Next In Line", KeyboardShortcut.Empty,
				new ConfigDescription("Switch the current outfit with the next outfit in the list", null,
				new ConfigurationManagerAttributes() { Order = index--, Category = main })),

				//Studio
				useCreatorDefaultBG = Config.Bind(stud, "Use Creator Default BG", true,
				new ConfigDescription("Use the creator recommended background as a default 😄", null,
				new ConfigurationManagerAttributes()
				{
					Order = index--,
					Category = studx,
					Browsable = StudioAPI.InsideStudio,
				})),
				enableBGUI = Config.Bind(stud, "Enable BG UI", true,
				new ConfigDescription("Use your own background as a default 😄", null,
				new ConfigurationManagerAttributes() { Order = index--, Category = studx, Browsable = false })),
				bgUIImagepath = Config.Bind(stud, "BG UI Image Path", "",
				new ConfigDescription("Use your own background image (will be [gray / creator defult] otherwise)", null,
				new ConfigurationManagerAttributes()
				{
					Order = index--,
					Category = studx,
					Browsable = StudioAPI.InsideStudio,
				})),



				//Advanced (the rest are in seperate location)
				resetOnLaunch = Config.Bind(adv, "Reset On Launch", true, new ConfigDescription("When enabled, reset adv. values when the mod is launched", null,
				new ConfigurationManagerAttributes() { Order = index--, IsAdvanced = true, Category = advx })),

				//Hiden
				lastCoordDir = Config.Bind(adv, "Last Coord Dir.", "", new ConfigDescription("", tags:
				new ConfigurationManagerAttributes() { Order = index--, Browsable = false, IsAdvanced = true, Category = advx })),

			};

			//Drawers
			{

				var cfgmngatrib = cfg.bgUIImagepath.Description.Tags.OfType<ConfigurationManagerAttributes>().FirstOrDefault();
				cfgmngatrib.CustomDrawer = (a) =>
				{
					GUILayout.BeginHorizontal();

					cfg.enableBGUI.Value = GUILayout.Toggle(cfg.enableBGUI.Value, new GUIContent()
					{
						text = !cfg.enableBGUI.Value ? "Disabled" : null
					});

					if(cfg.enableBGUI.Value)
					{
						var val = GUILayout.TextField((string)a.BoxedValue, GUILayout.Width(202));

						if(val != (string)a.BoxedValue)
							a.BoxedValue = val;

						if(GUILayout.Button("Select"))
							FashionLine_GUI.GetNewBGUIPath();
					}

					GUILayout.EndHorizontal();
				};

			}

			//Advanced
			{
				cfg.debug = Config.Bind(adv, "Log Debug", false,
					new ConfigDescription("View extra debug logs", null,
					new ConfigurationManagerAttributes()
					{
						Order = index--,
						IsAdvanced = true,
						Category = advx
					})).ConfigDefaulter();
				cfg.viewportUISpace = Config.Bind(adv, "Viewport UI Space", .43f,
					new ConfigDescription("Increase / decrease the Fashion Line viewport size ",
					new AcceptableValueRange<float>(0, 1),
					new ConfigurationManagerAttributes()
					{
						Order = index--,
						ShowRangeAsPercent = false,
						IsAdvanced = true,
						Category = advx
					})).ConfigDefaulter();
				cfg.studioUIWidth = Config.Bind(adv, "Studio UI Width", .5f,
					new ConfigDescription("Increase / decrease the Fashion Line content width ",
					new AcceptableValueRange<float>(0, 1),
					new ConfigurationManagerAttributes()
					{
						Order = index--,
						ShowRangeAsPercent = false,
						IsAdvanced = true,
						Category = advx
					})).ConfigDefaulter();
				cfg.studioWinRec = Config.Bind(adv, "Studio Win Rect", FashionLine_GUI.winRec,
					new ConfigDescription("reset the window location / Size if needed", null,
					new ConfigurationManagerAttributes()
					{
						Order = index--,
						ShowRangeAsPercent = false,
						IsAdvanced = true,
						CustomDrawer = (draw) =>
						{
							Rect tmp = new Rect(cfg.studioWinRec.Value);
							GUILayout.BeginHorizontal();

							GUILayout.Label("X", GUILayout.ExpandWidth(false));
							//tmp.x = GUILayout.HorizontalSlider(tmp.x, 0, Screen.width, GUILayout.ExpandWidth(true));
							float.TryParse(GUILayout.TextField(string.Format("{0:f0}", tmp.x)), out tmp.m_XMin);

							GUILayout.Label("Y", GUILayout.ExpandWidth(false));
							//tmp.y = GUILayout.HorizontalSlider(tmp.y, 0, Screen.height, GUILayout.ExpandWidth(true));
							float.TryParse(GUILayout.TextField(string.Format("{0:f0}", tmp.y)), out tmp.m_YMin);

							GUILayout.Label("Width", GUILayout.ExpandWidth(false));
							//tmp.width = GUILayout.HorizontalSlider(tmp.width, 0, Screen.width, GUILayout.ExpandWidth(true));
							float.TryParse(GUILayout.TextField(string.Format("{0:f0}", tmp.width)), out tmp.m_Width);

							GUILayout.Label("Height", GUILayout.ExpandWidth(false));
							//tmp.height = GUILayout.HorizontalSlider(tmp.height, 0, Screen.height, GUILayout.ExpandWidth(true));
							float.TryParse(GUILayout.TextField(string.Format("{0:f0}", tmp.height)), out tmp.m_Height);


							GUILayout.EndHorizontal();

							if(cfg.studioWinRec.Value != tmp)
								cfg.studioWinRec.Value = tmp;
						},
						Category = advx
					}));
				cfg.studioSortOffset = Config.Bind(adv, "Studio Sort Offset", FashionLine_GUI.offsetRect,
					new ConfigDescription("reset the window location / size if needed", null,
					new ConfigurationManagerAttributes()
					{
						Order = index--,
						ShowRangeAsPercent = false,
						IsAdvanced = true,
						CustomDrawer = (draw) =>
						{
							Rect tmp = new Rect(cfg.studioSortOffset.Value);
							GUILayout.BeginHorizontal();

							GUILayout.Label("X", GUILayout.ExpandWidth(false));
							//tmp.x = GUILayout.HorizontalSlider(tmp.x, 0, Screen.width, GUILayout.ExpandWidth(true));
							float.TryParse(GUILayout.TextField(string.Format("{0:f0}", tmp.x)), out tmp.m_XMin);

							GUILayout.Label("Y", GUILayout.ExpandWidth(false));
							//tmp.y = GUILayout.HorizontalSlider(tmp.y, 0, Screen.height, GUILayout.ExpandWidth(true));
							float.TryParse(GUILayout.TextField(string.Format("{0:f0}", tmp.y)), out tmp.m_YMin);

							GUILayout.Label("Width", GUILayout.ExpandWidth(false));
							//tmp.width = GUILayout.HorizontalSlider(tmp.width, 0, Screen.width, GUILayout.ExpandWidth(true));
							float.TryParse(GUILayout.TextField(string.Format("{0:f0}", tmp.width)), out tmp.m_Width);

							GUILayout.Label("Height", GUILayout.ExpandWidth(false));
							//tmp.height = GUILayout.HorizontalSlider(tmp.height, 0, Screen.height, GUILayout.ExpandWidth(true));
							float.TryParse(GUILayout.TextField(string.Format("{0:f0}", tmp.height)), out tmp.m_Height);


							GUILayout.EndHorizontal();

							if(cfg.studioSortOffset.Value != tmp)
								cfg.studioSortOffset.Value = tmp;
						},
						Category = advx
					}));

			}

			CfgUpdate();

			FashionLine_GUI.userTexUI = (cfg.bgUIImagepath.Value).CreateTexture(nullReturn: true);
			cfg.bgUIImagepath.SettingChanged += (m, n) =>
			{
				FashionLine_GUI.userTexUI = (cfg.bgUIImagepath.Value).CreateTexture(nullReturn: true);
			};

			cfg.viewportUISpace.SettingChanged += (m, n) =>
			{
				FashionLine_GUI.template.ResizeCustomUIViewport();
			};

			cfg.studioWinRec.SettingChanged += (m, n) =>
			{
				if(!cfg.studioWinRec.Value.Equals(FashionLine_GUI.winRec))
					FashionLine_GUI.winRec = new Rect(cfg.studioWinRec.Value);
			};

			cfg.studioSortOffset.SettingChanged += (m, n) =>
			{
				if(!cfg.studioSortOffset.Value.Equals(FashionLine_GUI.offsetRect))
					FashionLine_GUI.offsetRect = new Rect(cfg.studioSortOffset.Value);
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

			//Instantiate(new GameObject(), null).AddComponent<Canvas>();
		}

		void CfgUpdate()
		{
			//	var orphaned = this.Config.GetUnorderedOrphanedEntries().OrderByDescending((a) => a.Value.Length).ToList();
			//
			//	if(orphaned.Any())
			//		foreach(var cfg in this.Config.ToList())
			//		{
			//			var thing = orphaned.FirstOrDefault(a => cfg.Key.Key == a.Key.Key);
			//
			//			if(!thing.IsDefault())
			//				this.Config[cfg.Key].SetSerializedValue(thing.Value ?? "");
			//		}
			//
			//	var clearing = (Dictionary<ConfigDefinition, string>)Config.GetType().
			//
			//		GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Config);
			//	clearing.Clear();
			//
			//	Config.Save();

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

		static Texture2D _greyTex = null;
		public static Texture2D greyTex
		{
			get
			{
				if(_greyTex != null) return _greyTex;

				_greyTex = new Texture2D(1, 1);
				var pixels = _greyTex.GetPixels();
				for(int a = 0; a < pixels.Length; ++a)
					pixels[a] = Color.black;
				_greyTex.SetPixels(pixels);
				_greyTex.Apply();

				return _greyTex;
			}
		}


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
		public static Texture2D CreateTexture(this string path, byte[] data = null, bool nullReturn = false) =>
			(/* !data.IsNullOrEmpty() || */!File.Exists(path.MakeDirPath("/", "\\"))) ?
			data?.LoadTexture(TextureFormat.RGBA32) ?? (nullReturn ? null : Texture2D.blackTexture) :
			File.ReadAllBytes(path)?.LoadTexture(TextureFormat.RGBA32) ??
			(nullReturn ? null : Texture2D.blackTexture);

		public static bool InRange<T>(this IEnumerable<T> list, int index)
		=> index >= 0 && index < list.Count();

		/// <summary>
		/// Adds a value to the end of a list and returns it
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="list"></param>
		/// <param name="val"></param>
		/// <returns></returns>
		public static T AddNReturn<T>(this ICollection<T> list, T val)
		{
			list.Add(val);
			return list.Last();
		}


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
			{
				var val = enu.Count() > 0 ? enu.First() : (T)(object)null;
				return val;
			}
			catch
			{
				try
				{
					return (T)(object)null;

				}
				catch { throw new Exception("This object is not nullable"); }
			}
		}     //I love loopholes 🤣
		public static T FirstOrNull<T>(this IEnumerable<T> enu, Func<T, bool> predicate)
		{
			try
			{
				var val = enu.Count() > 0 ? enu.First(predicate) : (T)(object)null;
				return val;
			}
			catch
			{
				try
				{
					return (T)(object)null;

				}
				catch { throw new Exception("This object is not nullable"); }
			}
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

		public static Graphic GetTextComponentInChildren(this GameObject obj)
		{
			return (Graphic)obj?.GetComponentInChildren<TMP_Text>() ??
			 obj?.GetComponentInChildren<Text>();
		}
		public static Graphic GetTextComponentInChildren(this Graphic obj)
		{
			return (Graphic)obj?.GetComponentInChildren<TMP_Text>() ??
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

		public static List<KeyValuePair<ConfigDefinition, string>> GetUnorderedOrphanedEntries(this ConfigFile file, string sec = "")
		{
			Dictionary<ConfigDefinition, string> OrphanedEntries = new Dictionary<ConfigDefinition, string>();
			List<KeyValuePair<ConfigDefinition, string>> orderedOrphanedEntries = new List<KeyValuePair<ConfigDefinition, string>>();
			string section = string.Empty;
			string[] array = File.ReadAllLines(file.ConfigFilePath);
			for(int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				if(text.StartsWith("#"))
				{
					continue;
				}

				if(text.StartsWith("[") && text.EndsWith("]"))
				{
					section = text.Substring(1, text.Length - 2);
					continue;
				}

				string[] array2 = text.Split(new char[1] { '=' }, 2);
				if(sec == section || sec.IsNullOrEmpty())
					if(array2.Length == 2)
					{
						string key = array2[0].Trim();
						string text2 = array2[1].Trim();
						ConfigDefinition key2 = new ConfigDefinition(section, key);


						if(!((IDictionary<ConfigDefinition, ConfigEntryBase>)file).TryGetValue(key2, out var value))
						{
							OrphanedEntries[key2] = text2;
							orderedOrphanedEntries.Add(new KeyValuePair<ConfigDefinition, string>(key2, text2));
						}
					}

			}

			return orderedOrphanedEntries;
		}

		/// <summary>
		/// makes sure a path fallows the format "this/is/a/path" and not "this//is\\a/path" or similar
		/// </summary>
		/// <param name="dir"></param>
		/// <returns></returns>
		public static string MakeDirPath(this string dir, string oldslash = @"\", string newslash = "/")
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

		public static T AddToCustomGUILayout<T>(this T gui, bool topUI = false, float pWidth = -1, float viewpercent = -1, bool newVertLine = true) where T : BaseGuiEntry
		{
			gui.OnGUIExists(g =>
			{
				Instance.StartCoroutine(g.AddToCustomGUILayoutCO
				(topUI, pWidth, viewpercent, newVertLine));
			});
			return gui;
		}

		static IEnumerator AddToCustomGUILayoutCO<T>(this T gui, bool topUI = false, float pWidth = -1, float viewpercent = -1, bool newVertLine = true) where T : BaseGuiEntry
		{
			if(cfg.debug.Value) FashionLine_Core.Logger.LogDebug("moving object");

			yield return new WaitWhile(() => gui?.ControlObject?.GetComponentInParent<ScrollRect>()?.transform == null);

			//	newVertLine = horizontal ? newVertLine : true;
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


			Transform layoutObj = null;
			//Create  LayoutElement
			//if(horizontal)
			{
				//Create Layout Element GameObject
				par = newVertLine ?
					GameObject.Instantiate<GameObject>(new GameObject("LayoutElement"), par)?.transform :
					par.GetComponentsInChildren<HorizontalLayoutGroup>(2)
					.LastOrNull((elem) => elem.GetComponent<HorizontalLayoutGroup>())?.transform.parent ??
					GameObject.Instantiate<GameObject>(new GameObject("LayoutElement"), par)?.transform;

				layoutObj = par = par.gameObject.GetOrAddComponent<RectTransform>().transform;//May need this line (I totally do)


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
					.FirstOrNull((elem) => elem.gameObject.GetComponent<HorizontalLayoutGroup>())?.transform ??
					GameObject.Instantiate<GameObject>(new GameObject("HorizontalLayoutGroup"), par)?.transform;
				par = par.gameObject.GetOrAddComponent<RectTransform>().transform;//May need this line (I totally do)


				var layout = par.GetOrAddComponent<HorizontalLayoutGroup>();


				layout.childControlWidth = true;
				layout.childControlHeight = true;
				layout.childForceExpandWidth = true;
				layout.childForceExpandHeight = true;
				layout.childAlignment = TextAnchor.MiddleCenter;

				par?.ScaleToParent2D();

			}


			if(cfg.debug.Value) FashionLine_Core.Logger.LogDebug("setting as first/last");

			//remove extra LayoutElements
			var rList = ctrlObj.GetComponents<LayoutElement>();
			for(int a = 1; a < rList.Length; ++a)
				GameObject.DestroyImmediate(rList[a]);

			//change child layoutelements
			foreach(var val in ctrlObj.GetComponentsInChildren<LayoutElement>())
				if(val.gameObject != ctrlObj)
					val.flexibleWidth = val.minWidth = val.preferredWidth = -1;


			//edit layoutgroups
			foreach(var val in ctrlObj.GetComponentsInChildren<HorizontalLayoutGroup>())
			//	if(val.gameObject != ctrlObj)
			{
				val.childControlWidth = true;
				val.childForceExpandWidth = true;

			}

			//Set this object's Layout settings
			ctrlObj.transform.SetParent(par, false);
			ctrlObj.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
			var apos = ctrlObj.GetComponent<RectTransform>().anchoredPosition; apos.x = 0;
			if(topUI)
			{
				if(layoutObj?.GetSiblingIndex() != scrollRect.viewport.transform.GetSiblingIndex() - 1)
					layoutObj?.SetSiblingIndex
						(scrollRect.viewport.transform.GetSiblingIndex());
			}
			else
				layoutObj?.SetAsLastSibling();

			//if(ctrlObj.GetComponent<LayoutElement>())
			//	GameObject.Destroy(ctrlObj.GetComponent<LayoutElement>());
			var thisLE = ctrlObj.GetOrAddComponent<LayoutElement>();
			thisLE.layoutPriority = 5;
			thisLE.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
			bool check = thisLE.transform.childCount > 1 &&
				!thisLE.GetComponent<HorizontalOrVerticalLayoutGroup>();
			if(check)
			{
				var tmp = GameObject.Instantiate(new GameObject(), thisLE.transform);
				var hlog = tmp.AddComponent<HorizontalLayoutGroup>();
				hlog.childAlignment = TextAnchor.MiddleLeft;
				hlog.childControlHeight = true;
				hlog.childControlWidth = false;
				hlog.childForceExpandHeight = false;
				hlog.childForceExpandWidth = true;

				for(int a = 0; a < thisLE.transform.childCount; ++a)
					if(thisLE.transform.GetChild(a) != tmp.transform)
						thisLE.transform.GetChild(a--).SetParent(tmp.transform);

			}
			if(thisLE.transform.childCount == 1)
				thisLE.transform.GetChild(0).ScaleToParent2D();


			thisLE.flexibleWidth = -1;
			thisLE.flexibleHeight = -1;
			thisLE.minWidth = -1;
			//thisLE.minHeight = -1;

			thisLE.preferredWidth =
#if HONEY_API
				  pWidth > 0 ? scrollRect.rectTransform.rect.width * pWidth : -1;
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

		public static Func<int> GUILayoutDropdownDrawer(Func<string[], int, GUIContent> content, string[] items = null, int initIndex = 0, Func<string[], string[]> listUpdate = null, Func<int, int> onSelect = null, bool vertical = true)
		{
			int selectedItem = initIndex;
			bool selectingItem = false;
			Vector2 scrollpos = Vector2.zero;

			return new Func<int>(() =>
			{
				if(vertical)
					GUILayout.BeginVertical();
				else
					GUILayout.BeginHorizontal();

				items = listUpdate?.Invoke(items) ?? items;

				if(!items.InRange(selectedItem))
					selectedItem = Math.Max(0, Math.Min
					(items.Length - 1, selectedItem));

				if(!items.InRange(selectedItem)) return -1;


				try
				{
					GUILayout.Space(3);
					bool btn;
					//int maxWidth = 350, maxHeight = 200;
					if(items.Length > 0)
						if((btn = GUILayout.Button(content?.Invoke(items, selectedItem) ?? new GUIContent(),
							 GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false))) || selectingItem)
						{
							selectingItem = !(btn && selectingItem);//if dropdown btn was pressed

							var rec = new Rect(GUILayoutUtility.GetLastRect());
							rec.y += rec.height;
							rec.height *= 3;
							var recView = new Rect(rec);
							recView.width *= .8f;
							recView.height = items.Length * (rec.width / 3);

							scrollpos = GUILayout.BeginScrollView(scrollpos, false, false,
								//GUILayout.Height(rec.height),
								GUILayout.ExpandWidth(true),
								GUILayout.ExpandHeight(true)
								);

							var select = GUILayout.SelectionGrid(selectedItem, items, 1,
								//	GUILayout.Height(recView.height),
								GUILayout.ExpandWidth(true),
								GUILayout.ExpandHeight(true)
								);
							if(select != selectedItem) { selectingItem = false; select = onSelect != null ? onSelect(select) : select; }
							selectedItem = select;

							GUILayout.EndScrollView();

						}

					GUILayout.Space(5);
				}
				catch(Exception e)
				{
					FashionLine_Core.Logger.LogError(e);
				}

				if(vertical)
					GUILayout.EndVertical();
				else
					GUILayout.EndHorizontal();

				return selectedItem;
			});
		}
		public static PluginData Copy(this PluginData source)
		{
			return new PluginData
			{
				version = source.version,
				data = source.data.ToDictionary((p) => p.Key, (p) => p.Value),
			};
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
