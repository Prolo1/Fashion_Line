using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

using ProloAPI;
using ProloAPI.Extentions;

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
using KK_Plugins.MaterialEditor;

using Studio;
using UniRx;

#if HONEY_API
using AIChara;
using CharaCustom;
#else
using ChaCustom;
#endif

using static BepInEx.Logging.LogLevel;
using static FashionLine.FashionLine_Core;

//#if HONEY_API
//using All_BrowserFolders = BrowserFolders.AI_BrowserFolders;
//#elif KKS
//using All_BrowserFolders = BrowserFolders.KKS_BrowserFolders;
//#endif

namespace FashionLine
{
	using static ProloAPI.Utilities.Util_General;

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
	//// Tell BepInEx that we need MaterialEditor to run, and that we only need it if it's there.
	//// Check documentation of KoikatuAPI.VersionConst for more info.
	//BepInDependency(All_BrowserFolders.Guid, BepInDependency.DependencyFlags.SoftDependency),
	]
	#endregion
	// Specify this as a plugin that gets loaded by BepInEx
	[BepInPlugin(GUID, ModName, Version)]
	public partial class FashionLine_Core : ProloUnityPlugin<FashionLine_Core>
	{

		#region Variables
		public const string ModName = "Fashion Line";
		public const string GUID = "prolo.fashionline";//never change this
		public const string Description =
			@"Adds the ability to save coordinate cards to a " +
			@"character card and use them (Why was this not part of HS2/AI?¯\_(ツ)_/¯)";
		public const string Version = "0.3.3.1";

		//public static FashionLine_Core Instance;
		//internal static new ManualLogSource Logger;

		internal static DependencyInfo<KoiClothesOverlayMgr> KoiOverlayDependency;
		internal static DependencyInfo<MaterialEditorPlugin> MatEditerDependency;
		//internal static DependencyInfo<All_BrowserFolders> BrowserfolderDependency;

		internal static Texture2D icon = null;
		internal static Texture2D UIGoku = null;
		internal static Texture2D iconBG = null;

		public static FashionLineConfig cfg;
		public struct FashionLineConfig : IConfiguration
		{
			//Main
			public ConfigEntry<bool> enable { get; set; }
			public ConfigEntry<bool> areCoordinatesPersistant { get; set; }
			public ConfigEntry<KeyboardShortcut> prevInLine { get; set; }
			public ConfigEntry<KeyboardShortcut> nextInLine { get; set; }

			//Studio
			public ConfigEntry<bool> useCreatorDefaultBG { get; set; }
			public ConfigEntry<bool> enableBGUI { get; set; }
			public ConfigEntry<string> bgUIImagepath { get; set; }

			//Advanced
			public ConfigEntry<bool> resetOnLaunch { get; set; }
			public ConfigEntry<bool> debug { get; set; }
			public ConfigEntry<float> viewportUISpace { get; set; }
			public ConfigEntry<float> studioUIWidth { get; set; }
			public ConfigEntry<Rect> studioWinRec { get; set; }
			public ConfigEntry<Rect> studioSortOffset { get; set; }

			//Hiden
			public ConfigEntry<string> lastCoordDir { get; set; }

		}
		#endregion

		void initConfiguration()
		{
			int secIndex = 0;
			int secIndex2 = 99;
			int index = 0;
			//bool enableBGUI = true;

			string main = "";
			//string mainx =
			//$"{secIndex++:d2}. " + main;

			string stud = "Studio";
			string studx =
			$"{secIndex++:d2}. " + stud;

			string adv = "Advanced";
			string advx =
			$"{secIndex2--:d2}. " + adv;

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

			//Advanced
			{
				cfg.debug = Config.Bind(adv, "Log Debug", false,
					new ConfigDescription("View extra debug logs", null,
					new ConfigurationManagerAttributes()
					{
						Order = index--,
						IsAdvanced = true,
						Category = advx
					})).ConfigDefaulter(cfg);
				cfg.viewportUISpace = Config.Bind(adv, "Viewport UI Space",
#if HONEY_API
					0.43f,
#elif KOI_API
					0.69f,
#endif
					new ConfigDescription("Increase / decrease the Fashion Line viewport size ",
					new AcceptableValueRange<float>(0, 1),
					new ConfigurationManagerAttributes()
					{
						Order = index--,
						ShowRangeAsPercent = false,
						IsAdvanced = true,
						Category = advx
					})).ConfigDefaulter(cfg);

				cfg.studioUIWidth = Config.Bind(adv, "Studio UI Width", .5f,
					new ConfigDescription("Increase / decrease the Fashion Line content width ",
					new AcceptableValueRange<float>(0, 1),
					new ConfigurationManagerAttributes()
					{
						Order = index--,
						ShowRangeAsPercent = false,
						IsAdvanced = true,
						Category = advx
					})).ConfigDefaulter(cfg);
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

			//enable ProloAPI Debug
			Debug = cfg.debug.Value;
			cfg.debug.SettingChanged += (m, n) => Debug = cfg.debug.Value;

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


			//CfgUpdate();


			FashionLine_GUI.userTexUI = (cfg.bgUIImagepath.Value).CreateTexture();
			cfg.bgUIImagepath.SettingChanged += (m, n) =>
			{
				FashionLine_GUI.userTexUI = (cfg.bgUIImagepath.Value).CreateTexture();
			};

			cfg.viewportUISpace.SettingChanged += (m, n) =>
			{
				FashionLine_GUI.template.ResizeCustomUIViewport(cfg.viewportUISpace.Value);
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
		}

		void Awake()
		{
			Debug = true;

			ForeGrounder.SetCurrentForground();
			//Soft dependency variables
			{
				KoiOverlayDependency = new DependencyInfo<KoiClothesOverlayMgr>(new Version(KoiClothesOverlayMgr.Version));
				MatEditerDependency = new DependencyInfo<MaterialEditorPlugin>(new Version(MaterialEditorPlugin.PluginVersion));
				//BrowserfolderDependency = new DependencyInfo<All_BrowserFolders>(new Version(All_BrowserFolders.Version));

				if(!KoiOverlayDependency.IsInTargetVersionRange)
					Logger.Log(Message | Warning, $"Some [{ModName}] functionality may be locked due to the " +
						$"absence of [{nameof(KoiClothesOverlayMgr)}] " +
						$"or the use of an incorrect version\n" +
						$"{KoiOverlayDependency}");

				if(!MatEditerDependency.IsInTargetVersionRange)
					Logger.Log(Message | Warning, $"Some [{ModName}] functionality may be locked due to the " +
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
			using(MemoryStream memStream = new MemoryStream())
			{
				var assembly = Assembly.GetExecutingAssembly();
				var resources = assembly.GetManifestResourceNames();

				//var data = assembly.GetManifestResourceStream(resources.FirstOrDefault((txt) => txt.ToLower().Contains("ultra instinct.jpg")));
				//data.CopyTo(memStreme);
				ResourceGrabber("ultra instinct.jpg", assembly, resources, memStream);
				UIGoku =
					memStream?.GetBuffer()?
					.LoadTexture();

				//data = assembly.GetManifestResourceStream(resources.FirstOrDefault((txt) => txt.ToLower().Contains("icon.png")));
				//data.CopyTo(memStreme);
				//icon =
				//	memStreme?.GetBuffer()?
				//	.LoadTexture();
				//memStreme.SetLength(0);
				//icon.Compress(false);
				//icon.Apply();

				//data = assembly.GetManifestResourceStream(resources.FirstOrDefault((txt) => txt.ToLower().Contains("new icon.png")));
				//data.CopyTo(memStreme);
				ResourceGrabber("new icon.png", assembly, resources, memStream);
				iconBG =
					memStream?.GetBuffer()?
					.LoadTexture();
				memStream.SetLength(0);
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

			initConfiguration();

			Hooks.Init();
			CharacterApi.RegisterExtraBehaviour<FashionLine_Controller>(GUID);
			FashionLine_GUI.Init();

			//Instantiate(new GameObject(), null).AddComponent<Canvas>();
		}

		void Update()
		{
			//Key Updates
			var list = GetAllChaFuncCtrlOfType<FashionLine_Controller>();
			if(cfg.nextInLine.Value.IsDown())
				foreach(var ctrl in list)
					ctrl.NextInLine();

			if(cfg.prevInLine.Value.IsDown())
				foreach(var ctrl in list)
					ctrl.PrevInLine();

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


}
