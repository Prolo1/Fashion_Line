using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

using BepInEx;
using KKAPI.Utilities;
using KKAPI.Maker;
using KKAPI.Maker.UI;
#if HONEY_API
using AIChara;
using CharaCustom;
#else
using ChaCustom;
#endif
using Illusion.Game;

using static KKAPI.Maker.MakerAPI;
using static FashionLine.FashionLine_Core;
using UniRx;
using TMPro;
//using static FashionLine.FashionLine_Util;
//using static Illusion.Game.Utils;

namespace FashionLine
{
	public class FashionLine_GUI
	{

		#region Data
		private static MakerCategory category = null;
		public static readonly string subCategoryName = "FashionLine";
		public static readonly string displayName = "Fashion Line";

		static MyMakerText costTxt = null;
		static CoordData currentCoord = null;
		static GridLayoutGroup gridLayout = null;
		static ToggleGroup tglGroup = null;
		static EventHandler isPersistantHndl = null;
		internal static MakerImage template = null;
		public static Button coordToFashionBtn = null;
		public static Button toFashionOnlyBtn = null;

#if HONEY_API
		public static CvsO_Type charaCustom { get; private set; } = null;
		public static CvsB_ShapeBreast boobCustom { get; private set; } = null;
		public static CvsB_ShapeWhole bodyCustom { get; private set; } = null;
		public static CvsF_ShapeWhole faceCustom { get; private set; } = null;
		public static CvsC_ClothesSave clothesSave { get; private set; } = null;
		public static CvsC_ClothesInput clothesInput { get; private set; } = null;
#else
		public static CvsChara charaCustom { get; private set; } = null;
		public static CvsBreast boobCustom { get; private set; } = null;
		public static CvsBodyShapeAll bodyCustom { get; private set; } = null;
		public static CvsFaceShapeAll faceCustom { get; private set; } = null;
		public static CvsClothes clothesSave { get; private set; } = null;
#endif

		#endregion

		public static void Init()
		{

			RegisterCustomSubCategories += (s, e) =>
		   {
			   //Create custom category 

#if HONEY_API
			   MakerCategory peram = MakerConstants.Parameter.Type;
#else
			   MakerCategory peram = MakerConstants.Parameter.QA;
#endif




			   category = new MakerCategory(peram.CategoryName, subCategoryName, displayName: displayName);
			   // category2 = new MakerCategory(MakerConstants.Clothes.CategoryName, "Save / Delete");

			   e.AddSubCategory(category);
		   };
			MakerBaseLoaded += (s, e) => { AddFashionLineMenu(e); };
			MakerFinishedLoading += (s, e) =>
			{
				var allCvs =

#if HONEY_API
				((CvsSelectWindow[])Resources.FindObjectsOfTypeAll(typeof(CvsSelectWindow)))
				.OrderBy((k) => k.transform.GetSiblingIndex())//I just want them in the right order
				.Attempt(p => p.items)
				.Aggregate((l, r) => l.Concat(r).ToArray());//should flaten array


				bodyCustom = (CvsB_ShapeWhole)allCvs.FirstOrNull((p) => p.cvsBase is CvsB_ShapeWhole)?.cvsBase;
				faceCustom = (CvsF_ShapeWhole)allCvs.FirstOrNull((p) => p.cvsBase is CvsF_ShapeWhole)?.cvsBase;
				boobCustom = (CvsB_ShapeBreast)allCvs.FirstOrNull((p) => p.cvsBase is CvsB_ShapeBreast)?.cvsBase;
				charaCustom = (CvsO_Type)allCvs.FirstOrNull((p) => p.cvsBase is CvsO_Type)?.cvsBase;
				clothesSave = (CvsC_ClothesSave)(allCvs.FirstOrNull((p) => p.cvsBase is CvsC_ClothesSave)?.cvsBase);
				//	clothesInput = (CvsC_ClothesInput)(allCvs.FirstOrNull((p) => p.cvsBase is CvsC_ClothesInput)?.cvsBase);


#else
				0;//don't remove this!
				bodyCustom = (CvsBodyShapeAll)Resources.FindObjectsOfTypeAll(typeof(CvsBodyShapeAll))[0];
				faceCustom = (CvsFaceShapeAll)Resources.FindObjectsOfTypeAll(typeof(CvsFaceShapeAll))[0];
				boobCustom = (CvsBreast)Resources.FindObjectsOfTypeAll(typeof(CvsBreast))[0];
				charaCustom = (CvsChara)Resources.FindObjectsOfTypeAll(typeof(CvsChara))[0];
#endif


#if HONEY_API
				//add new buttons to coordinate save screen
				{
					var orig = clothesSave.clothesLoadWin.button[1];
					var par = orig.transform.parent;

					coordToFashionBtn = GameObject
					.Instantiate(orig.gameObject, par)
					.GetComponent<Button>();

					var parRec = coordToFashionBtn.GetComponent<RectTransform>();
					parRec.pivot = Vector3.one * .5f;
					parRec.anchoredPosition =
				   parRec.anchoredPosition +
				   new Vector2(parRec.rect.size.x * .5f,
				   parRec.rect.size.y * -.5f - 80);



					Text txt;
					GameObject obj;
					if(txt = coordToFashionBtn.GetComponentInChildren<Text>())
					{
						obj = txt.gameObject;
						GameObject.DestroyImmediate(txt);
					}
					else
						obj = coordToFashionBtn.GetComponentInChildren<TMP_Text>()?.gameObject;


					var txtpro = obj.GetOrAddComponent<TextMeshProUGUI>();
					txtpro.ScaleToParent2D(pwidth: .8f);
					txtpro.autoSizeTextContainer = false;
					//Bounds b = txtpro.bounds;
					//b.size = par.GetComponent<RectTransform>().rect.size * new Vector2(.50f, .90f);
					//b.center = Vector3.zero;
					txtpro.extraPadding = true;
					txtpro.alignment = TextAlignmentOptions.Center;
					txtpro.color = Color.black;
					txtpro.enableAutoSizing = true;
					txtpro.fontSizeMax = 100;
					txtpro.fontSizeMin = 1;
					txtpro.fontStyle = FontStyles.Bold;
					txtpro.SetAllDirty();

					coordToFashionBtn.SetTextFromTextComponent("Save & Add to FashionLine");
					coordToFashionBtn.onClick.ActuallyRemoveAllListeners();

					toFashionOnlyBtn = GameObject
					.Instantiate(coordToFashionBtn.gameObject, par)
					.GetComponent<Button>();
					toFashionOnlyBtn.GetComponent<RectTransform>().anchoredPosition =
					toFashionOnlyBtn.GetComponent<RectTransform>().anchoredPosition +
					new Vector2(toFashionOnlyBtn.GetComponent<RectTransform>().rect.size.x + 5, 0);
					toFashionOnlyBtn.SetTextFromTextComponent("Add Only to FashionLine ");

					//Add buttons to original list 
					clothesSave.clothesLoadWin.button.Append(coordToFashionBtn);
					clothesSave.clothesLoadWin.button.Append(toFashionOnlyBtn);

					try
					{

						orig.ObserveEveryValueChanged((j) => j.m_Interactable).Subscribe((inter) =>
						{
							if(!coordToFashionBtn) return;
							if(!toFashionOnlyBtn) return;
							toFashionOnlyBtn.interactable = coordToFashionBtn.interactable = inter;
						});
					}
					catch(Exception ex)
					{
						FashionLine_Core.Logger.LogError("could not subscribe to value change: " + ex);
					}

					//Onclick code is done in a hook...

					//var HLGroup = par.gameObject.GetComponent<HorizontalLayoutGroup>();
					////HLGroup.CalculateLayoutInputHorizontal();
					////HLGroup.CalculateLayoutInputVertical();
					//HLGroup.SetDirty();
				}

				//Force the floating settings window to show up
				{
					var btn = allCvs?.FirstOrNull(p => p?.btnItem?.gameObject?.GetTextFromTextComponent() == displayName).btnItem;
					btn?.onClick?.AddListener(() => GetMakerBase().drawMenu.ChangeMenuFunc());
				}
#endif
			};
			MakerExiting += (s, e) => { Cleanup(); };

		}

		static void Cleanup()
		{
			charaCustom = null;
			boobCustom = null;
			bodyCustom = null;
			faceCustom = null;

			//Seperation

			gridLayout = null;
			currentCoord = null;
			tglGroup = null;
			coordToFashionBtn = null;
			toFashionOnlyBtn = null;
			costTxt = null;

			if(isPersistantHndl != null)
				cfg.areCoordinatesPersistant.SettingChanged -= isPersistantHndl;

		}

		static void AddFashionLineMenu(RegisterCustomControlsEvent e)
		{
			Cleanup();

			var inst = FashionLine_Core.Instance;
			var fashCtrl = MakerAPI.GetCharacterControl().GetComponent<FashionLineController>();

			#region Init
#if KOI_API
			e.AddControl<MakerText>(new MakerText(displayName, category, inst));
			e.AddControl(new MakerSeparator(category, inst));
#endif
			template = e.AddControl(new MakerImage(Texture2D.blackTexture, category, inst));
			template.OnGUIExists((gui) =>
			{
				IEnumerator SetupCo()
				{
					yield return new WaitWhile(() => gui?.ControlObject?.GetComponentInParent<ScrollRect>()?.transform == null);
					gui.ControlObject.SetActive(false);

					var scrollRect = gui.ControlObject.GetComponentInParent<ScrollRect>();
					var layoutEle = gui.ControlObject.GetOrAddComponent<LayoutElement>();
					tglGroup = gui.ControlObject.transform.parent.GetOrAddComponent<ToggleGroup>();
					tglGroup.allowSwitchOff = true;


					var imgObj = gui.ControlObject.GetComponentInChildren<RawImage>();

					var tgl = imgObj.GetOrAddComponent<Toggle>();


					tgl.group = tglGroup;



					layoutEle.minWidth = 100;
					layoutEle.minHeight = 165;
					layoutEle.preferredWidth = -1;
					layoutEle.preferredHeight = -1;

					//try
					//{
					//	GameObject.DestroyImmediate(scrollRect.content.GetComponent<LayoutGroup>());
					//}
					//catch { }

					var gridPar = GameObject.Instantiate<GameObject>(new GameObject("Grid Layout Obj"), scrollRect.content).AddComponent<RectTransform>().gameObject;
					gridLayout = gridPar.AddComponent<GridLayoutGroup>();
					gui.ControlObject.transform.SetParent(gridPar.transform);
					imgObj.ScaleToParent2D();
					gridPar.ScaleToParent2D(changeheight: false);

					int space = 7;
					gridLayout.constraintCount = 3;
					gridLayout.spacing = new Vector2(space, space * .5f);
					gridLayout.cellSize = new Vector2(scrollRect.content.GetComponent<RectTransform>().rect.width / (gridLayout.constraintCount + .5f), layoutEle.minHeight);
					gridLayout.cellSize = new Vector2(gridLayout.cellSize.x, gridLayout.cellSize.x * 1.3f);
					gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
					gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
					gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
					gridLayout.childAlignment = TextAnchor.MiddleCenter;

					//	gridLayout.

					yield break;
				}

				inst.StartCoroutine(SetupCo());
			});
			#endregion

			#region Top
			costTxt = e.AddControl(new MyMakerText("Costume Name", category, inst))
				.AddToCustomGUILayout(topUI: true, horizontal: false);

			e.AddControl(new MakerSeparator(category, inst))
				.AddToCustomGUILayout(topUI: true, horizontal: false);

			#endregion

			#region Bottom
			e.AddControl(new MakerToggle(category, "Make FashionLine Persistant", inst))
				.AddToCustomGUILayout(horizontal: false)
				.OnGUIExists((gui) =>
				{
					var obj = (MakerToggle)gui;
					cfg.areCoordinatesPersistant.SettingChanged += isPersistantHndl =
					(s, a) =>
					{
						if(obj.Value != cfg.areCoordinatesPersistant.Value)
							obj.Value = cfg.areCoordinatesPersistant.Value;
					};
					isPersistantHndl(null, null);

					gui.ValueChanged.Subscribe((on) =>
					{
						cfg.areCoordinatesPersistant.Value = on;
					});
				});

			e.AddControl(new MyMakerButton("Wear Selected", category, inst))
				.AddToCustomGUILayout(horizontal: true, newVertLine: true)
				.OnClick.AddListener(() =>
				{
					if(!tglGroup.AnyTogglesOn()) return;

					fashCtrl.WearFashion(currentCoord, reload: true);
					Illusion.Game.Utils.Sound.Play(SystemSE.ok_l);
				});

			e.AddControl(new MyMakerButton("Wear Default", category, inst))
			   .AddToCustomGUILayout(horizontal: true)
			   .OnClick.AddListener(() =>
			   {
				   fashCtrl.WearDefaultFashion(reload: true);
				   Illusion.Game.Utils.Sound.Play(SystemSE.ok_l);
			   });

			e.AddControl(new MyMakerButton("Load Coordinate[s]", category, inst))
				.AddToCustomGUILayout(horizontal: true, newVertLine: true)
				.OnClick.AddListener(() =>
				{
					ForeGrounder.SetCurrentForground();
					GetNewImageTarget();
				});

			e.AddControl(new MyMakerButton("Add Current Coordinate", category, inst))
			   .AddToCustomGUILayout(horizontal: true)
			   .OnClick.AddListener(() =>
			   {
				   Hooks.OnSaveToFashionLineOnly(toFashionOnlyBtn, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
			   });

			e.AddControl(new MyMakerText("Danger Zone", category, inst))
				.AddToCustomGUILayout(horizontal: true, newVertLine: true)
				.OnGUIExists(gui =>
				{
					gui.TextColor = Color.red;
				});

			e.AddControl(new MyMakerButton("Remove Selected", category, inst))
				.AddToCustomGUILayout(horizontal: true, newVertLine: true)
				.OnGUIExists((gui) =>
				{
					gui.TextColor = Color.red;
					gui.ButtonColor = Color.black;
				})
				.OnClick.AddListener(() =>
				{
					if(!tglGroup.AnyTogglesOn()) return;

					fashCtrl.RemoveFashion(in currentCoord);
					Illusion.Game.Utils.Sound.Play(SystemSE.cancel);
				});

			e.AddControl(new MyMakerButton("Remove All", category, inst))
				.AddToCustomGUILayout(horizontal: true)
				.OnGUIExists((gui) =>
				{
					gui.TextColor = Color.red;
					gui.ButtonColor = Color.black;
				})
				.OnClick.AddListener(() =>
				{
					foreach(var fash in fashCtrl.fashionData.Values.ToList())
						fashCtrl.RemoveFashion(in fash);
					Illusion.Game.Utils.Sound.Play(SystemSE.cancel);
				});
			#endregion
		}

		public static void AddCoordinate(in CoordData coord)
		{
			var inst = FashionLine_Core.Instance;
			inst.StartCoroutine(AddCoordinateCO(coord));
		}

		public static void RemoveCoordinate(in CoordData coord)
		{
			var inst = FashionLine_Core.Instance;
			inst.StartCoroutine(RemoveCoordinateCO(coord));
		}


		#region Coroutine Helpers   

		static IEnumerator AddCoordinateCO(CoordData coordinate)
		{

			yield return new WaitWhile(() => gridLayout == null);

			//	for( int a=0;a<12;++a)
			//		yield return null;

			var comp = GameObject.Instantiate<GameObject>(template.ControlObject, template.ControlObject.transform.parent);
			var img = comp.GetComponentInChildren<RawImage>();
			img.texture = coordinate.data?.LoadTexture(TextureFormat.RGBA32);

			var tgl = comp.GetComponentInChildren<Toggle>();
			coordinate.extras.Add(tgl);

			tgl.targetGraphic = img;
			var colours = tgl.colors = new ColorBlock()
			{
				normalColor = Color.white,
				highlightedColor = new Color(1, 1, 1, .75f),
				pressedColor = new Color(1, 1, 1, .45f),
				colorMultiplier = 1,
				fadeDuration = 0.1f,
			};

			tgl.onValueChanged.AddListener((val) =>
			{
#if KKS
				colours.selectedColor = Color.white;
#else
				img.color = Color.white;
#endif
				currentCoord = null;

				if(!val) return;

#if KKS
				colours.selectedColor = Color.green - new Color(0, 0, 0, .15f);
#else
				img.color = Color.green - new Color(0, 0, 0, .15f);
#endif

				currentCoord = coordinate;

#if !KKS
				tgl.InstantClearState();
#endif
			});

			bool last = false;
			tgl.ObserveEveryValueChanged(p => p.isPointerInside).Subscribe((hover) =>
			{
				if(last == hover) return;//not sure if this does anything...

				if(hover)
				{
					costTxt.Text = coordinate.name;
					costTxt.TextColor = Color.yellow;
				}
				else
				{
					costTxt.Text = FashionLine_Util.GetAllChaFuncCtrlOfType<FashionLineController>()
					.FirstOrNull()?.fashionData.Values
					.FirstOrNull(j => (Toggle)j.extras.FirstOrNull(k => k is Toggle) == tgl.group.ActiveToggles().FirstOrNull())
					?.name ?? (tgl.group.AnyTogglesOn() ? costTxt.Text : "");//May find something better in the future (I hope so 😰)

					if(tgl.group.AnyTogglesOn())
						costTxt.TextColor = Color.green;
				}

				last = hover;
			});

			comp.SetActive(true);
			yield break;
		}

		static IEnumerator RemoveCoordinateCO(CoordData coordinate)
		{

			yield return new WaitWhile(() => gridLayout == null);

			Toggle tmp = (Toggle)coordinate.extras.FirstOrNull((obj) => obj is Toggle);
			if(tmp)
				GameObject.Destroy(tmp.transform.parent.gameObject);

			if(tmp.isOn) currentCoord = null;

			yield break;
		}

		#endregion

		#region Image Stuff

		#region File Data
		public const string FileExt = ".png";
		public const string FileFilter = "Coordinate Images (*.png)|*.png";

		public static string DefaultCoordDirectory { get => (Directory.GetCurrentDirectory() + "/UserData/coordinate/").MakeDirPath(); }

		public static string TargetDirectory { get => Directory.Exists(cfg.lastCoordDir.Value) && !cfg.lastCoordDir.Value.IsNullOrWhiteSpace() ? cfg.lastCoordDir.Value : DefaultCoordDirectory; }

		#endregion

		private static string MakeDirPath(string path) => FashionLine_Util.MakeDirPath(path);


		/// <summary>
		/// Called after a file is chosen in file explorer menu  
		/// </summary>
		/// <param name="strings: ">the info returned from file explorer. strings[0] returns the full file path</param>
		private static void OnFileObtained(string[] strings)
		{

			ForeGrounder.RevertForground();
			if(cfg.debug.Value) FashionLine_Core.Logger.LogDebug($"Enters accept");
			if(strings == null || strings.Length == 0) return;

			foreach(string s in strings)
			{
				var texPath = s.MakeDirPath();

				if(cfg.debug.Value)
				{
					FashionLine_Core.Logger.LogDebug($"Original path: {texPath}");
					FashionLine_Core.Logger.LogDebug($"texture path: {Path.Combine(Path.GetDirectoryName(texPath), Path.GetFileName(texPath))}");
				}

				if(texPath.IsNullOrEmpty())
				{
					continue;
				}

				//	var directory = Path.GetDirectoryName(texPath).MakeDirPath();
				var filename = texPath.Substring(texPath.LastIndexOf('/') + 1).MakeDirPath();//not sure why this happens on hs2?

				//use file
				var fashCtrl = MakerAPI.GetCharacterControl().GetComponent<FashionLineController>();

				var coord = new ChaFileCoordinate();
				coord.LoadFile(texPath);
				var name = filename.Substring(0, filename.LastIndexOf('.'));
				name = !coord.coordinateName.IsNullOrWhiteSpace() ?
					coord.coordinateName ?? name : name;

				fashCtrl.AddFashion(name, new CoordData()
				{
					data = File.ReadAllBytes(texPath),
					name = name
				});
			}
			if(cfg.debug.Value) FashionLine_Core.Logger.LogDebug($"Exit accept");
		}

		public static void GetNewImageTarget()
		{
			//	OpenFileDialog.OpenSaveFileDialgueFlags.OFN_CREATEPROMPT;
			FashionLine_Core.Logger.LogInfo("Game Root Path: " + Directory.GetCurrentDirectory());

			var paths = OpenFileDialog.ShowDialog("Add New Coordinate[s] (You can select multiple)",
			TargetDirectory.MakeDirPath("/", "\\"),
			FileFilter,
			FileExt,
			OpenFileDialog.MultiFileFlags,
			owner: ForeGrounder.GetForgroundHandeler());

			var path = paths?.Attempt((s) => s.IsNullOrWhiteSpace() ?
			throw new Exception() : s).LastOrNull().MakeDirPath();

			cfg.lastCoordDir.Value = path?.Substring(0, path.LastIndexOf('/')) ?? TargetDirectory;

			OnFileObtained(paths);
			Illusion.Game.Utils.Sound.Play(SystemSE.ok_l);
		}
		#endregion

		#region User UI Objects
		class MyMakerText : MakerText
		{
			public MyMakerText(string text, MakerCategory category, BaseUnityPlugin owner)
				: base(text, category, owner)
			{
				this.OnGUIExists(gui => { });
			}

			new public Color TextColor
			{
				get => ((Graphic)ControlObject.GetTextComponent()).color;
				set
				{
					var val = ((Graphic)ControlObject.GetTextComponent());
					val.color = value;
					val.SetAllDirty();
				}
			}
		}
		class MyMakerButton : MakerButton
		{
			public MyMakerButton(string text, MakerCategory category, BaseUnityPlugin owner) : base(text, category, owner)
			{
			}

			public Color ButtonColor
			{
				get => ControlObject.GetComponentInChildren<Button>().targetGraphic.color;
				set
				{
					var val = ControlObject.GetComponentInChildren<Button>().targetGraphic;
					val.color = value;
					val.SetAllDirty();
				}
			}
			new public Color TextColor
			{
				get => ((Graphic)ControlObject.GetTextComponent()).color;
				set
				{
					var val = ((Graphic)ControlObject.GetTextComponent());
					val.color = value;
					val.SetAllDirty();
				}
			}
		}

		#endregion
	}
}