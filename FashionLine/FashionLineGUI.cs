using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;

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
//using static FashionLine.FashionLine_Util;
//using static Illusion.Game.Utils;

namespace FashionLine
{
	public class FashionLineGUI
	{

		#region Data
		private static MakerCategory category = null;
		public static readonly string subCategoryName = "FashionLine";
		public static readonly string displayName = "Fashion Line";

		static CoordData currentCoord = null;
		static GridLayoutGroup gridLayout = null;
		static ToggleGroup tglGroup = null;
		static MakerImage template = null;

#if HONEY_API
		public static CvsO_Type charaCustom { get; private set; } = null;
		public static CvsB_ShapeBreast boobCustom { get; private set; } = null;
		public static CvsB_ShapeWhole bodyCustom { get; private set; } = null;
		public static CvsF_ShapeWhole faceCustom { get; private set; } = null;
#else
		public static CvsChara charaCustom { get; private set; } = null;
		public static CvsBreast boobCustom { get; private set; } = null;
		public static CvsBodyShapeAll bodyCustom { get; private set; } = null;
		public static CvsFaceShapeAll faceCustom { get; private set; } = null;
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


#else
				0;//don't remove this!
				bodyCustom = (CvsBodyShapeAll)Resources.FindObjectsOfTypeAll(typeof(CvsBodyShapeAll))[0];
				faceCustom = (CvsFaceShapeAll)Resources.FindObjectsOfTypeAll(typeof(CvsFaceShapeAll))[0];
				boobCustom = (CvsBreast)Resources.FindObjectsOfTypeAll(typeof(CvsBreast))[0];
				charaCustom = (CvsChara)Resources.FindObjectsOfTypeAll(typeof(CvsChara))[0];
#endif

#if HONEY_API
				//Force the floating settings window to show up

				var btn = allCvs?.FirstOrNull(p => p?.btnItem?.gameObject?.GetTextFromTextComponent() == displayName).btnItem;
				btn?.onClick?.AddListener(() => GetMakerBase().drawMenu.ChangeMenuFunc());
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
					gridPar.ScaleToParent2D(height: false);

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



			((MakerButton)e.AddControl(new MakerButton("Add", category, inst))
				.OnGUIExists((gui) => inst.StartCoroutine(AddToBottomGUILayoutCO(gui, horizontal: true))))
				.OnClick.AddListener(() =>
				{
					ForeGrounder.SetCurrentForground();
					GetNewImageTarget();
				});

			((MakerButton)e.AddControl(new MakerButton("Remove", category, inst))
				.OnGUIExists((gui) => inst.StartCoroutine(AddToBottomGUILayoutCO(gui, horizontal: true))))
				.OnClick.AddListener(() =>
				{
					if(!tglGroup.AnyTogglesOn()) return;

					fashCtrl.RemoveFashion(in currentCoord);
					Illusion.Game.Utils.Sound.Play(SystemSE.cancel);
				});

			((MakerButton)e.AddControl(new MakerButton("Wear Selected Costume", category, inst))
				.OnGUIExists((gui) => inst.StartCoroutine(AddToBottomGUILayoutCO(gui, horizontal: false))))
				.OnClick.AddListener(() =>
				{
					if(!tglGroup.AnyTogglesOn()) return;

					fashCtrl.WearCostume(currentCoord);
					Illusion.Game.Utils.Sound.Play(SystemSE.ok_l);
				});

			((MakerButton)e.AddControl(new MakerButton("Wear Drfault Costume", category, inst))
				.OnGUIExists((gui) => inst.StartCoroutine(AddToBottomGUILayoutCO(gui, horizontal: false))))
				.OnClick.AddListener(() =>
				{
					fashCtrl.WearDefaultCostume();
					Illusion.Game.Utils.Sound.Play(SystemSE.ok_l);
				});
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
		static IEnumerator AddToBottomGUILayoutCO(BaseGuiEntry gui, bool horizontal = false, float horiScale = -1, bool newVertLine = false)
		{
			if(cfg.debug.Value) FashionLine_Core.Logger.LogDebug("moving object");

			yield return new WaitWhile(() => gui?.ControlObject?.GetComponentInParent<ScrollRect>()?.transform == null);

			var ctrlObj = gui.ControlObject;

			var par = ctrlObj.GetComponentInParent<ScrollRect>().transform;
			var scrollRect = ctrlObj.GetComponentInParent<ScrollRect>();


			if(cfg.debug.Value) FashionLine_Core.Logger.LogDebug("Parent: " + par);


			//setup VerticalLayoutGroup
			var vlg = scrollRect.gameObject.GetOrAddComponent<VerticalLayoutGroup>();

#if HONEY_API
			vlg.childAlignment = TextAnchor.UpperCenter;
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
#if KOI_API
			scrollRect.GetComponent<Image>().sprite = scrollRect.content.GetComponent<Image>()?.sprite;
			scrollRect.GetComponent<Image>().color = (Color)scrollRect.content.GetComponent<Image>()?.color;


			scrollRect.GetComponent<Image>().enabled = true;
			scrollRect.GetComponent<Image>().raycastTarget = true;
			var img = scrollRect.content.GetComponent<Image>();
			if(!img)
				img = scrollRect.viewport.GetComponent<Image>();
			img.enabled = false;
#endif

			//Setup LayoutElements 
			scrollRect.verticalScrollbar.GetOrAddComponent<LayoutElement>().ignoreLayout = true;
			scrollRect.content.GetOrAddComponent<LayoutElement>().ignoreLayout = true;
			var viewLE = scrollRect.viewport.GetOrAddComponent<LayoutElement>();
			viewLE.minWidth = -1;
			viewLE.flexibleWidth = -1;
			//viewLE.minHeight = -1;
			float vHeight = scrollRect.rectTransform.rect.height;
			viewLE.minHeight = vHeight * .75f;
			//	viewLE.flexibleHeight = vHeight * .25f;


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
				viewLE.minWidth;
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


			//Set this object's Layout settings
			if(cfg.debug.Value) FashionLine_Core.Logger.LogDebug("setting as last");
			ctrlObj.transform.SetParent(par);
			ctrlObj.transform.SetAsLastSibling();
			var thisLE = ctrlObj.GetOrAddComponent<LayoutElement>();

			if(thisLE.transform.childCount > 0)
				thisLE.transform.GetChild(0).GetComponent<RectTransform>().ScaleToParent2D();



			thisLE.flexibleWidth = -1;
			thisLE.flexibleHeight = -1;
			thisLE.minWidth = -1;
			//thisLE.minHeight = -1;

			thisLE.preferredWidth =
#if HONEY_API
				horizontal && horiScale > 0 ? par.GetComponent<RectTransform>().rect.width * horiScale : -1;
#else
				horizontal && horiScale > 0 ? viewLE.minWidth * horiScale : -1;
#endif
			//thisLE.preferredHeight = ctrlObj.GetComponent<RectTransform>().rect.height;


			//Reorder Scrollbar
			scrollRect.verticalScrollbar.transform.SetAsLastSibling();
			yield break;
		}

		static IEnumerator AddCoordinateCO(CoordData coordinate)
		{

			yield return new WaitWhile(() => gridLayout == null);

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

		private static string _defaultOverlayDirectory { get => (Directory.GetCurrentDirectory() + "/UserData/coordinate/").MakeDirPath(); }

		public static string TargetDirectory { get => Directory.Exists(cfg.lastCoordDir.Value) && !cfg.lastCoordDir.Value.IsNullOrWhiteSpace() ? cfg.lastCoordDir.Value : _defaultOverlayDirectory; }

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
			Illusion.Game.Utils.Sound.Play(Illusion.Game.SystemSE.ok_l);
		}
		#endregion

	}
}