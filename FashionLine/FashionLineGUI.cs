using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Collections;

using UnityEngine;
using UnityEngine.UI;

using CharaCustom;
using KKAPI.Utilities;
using KKAPI.Maker;
using KKAPI.Maker.UI;

using AIChara;

using static KKAPI.Maker.MakerAPI;
using static FashionLine.FashionLine_Util;
using static FashionLine.FashionLine_Core;

namespace FashionLine
{

	public class FashionLineGUI
	{

		#region Data
		private static MakerCategory category;

		private static Coroutine lastExtent;
		public static readonly string subCategoryName = "FashionLine";
		public static readonly string displayName = "Fashion Line";


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
				MakerCategory peram = MakerConstants.Parameter.Character;
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

		private static void Cleanup()
		{
			//charaCustom = null;
			//boobCustom = null;
			//bodyCustom = null;
			//faceCustom = null;

			gridLayout = null;
		}

		static MakerImage template = null;

		static GridLayoutGroup gridLayout = null;

		public static void AddCoordinate(ChaFileCoordinate coordinate)
		{
			var inst = FashionLine_Core.Instance;

			inst.StartCoroutine(AddCoordinateCO(coordinate));
		}

		static void AddFashionLineMenu(RegisterCustomControlsEvent e)
		{

			Cleanup();

			var inst = FashionLine_Core.Instance;


			#region Init
			template = e.AddControl(new MakerImage(Texture2D.blackTexture, category, inst));
			template.OnGUIExists((gui) =>
			{
				var scrollRect = gui.ControlObject.GetComponentInParent<ScrollRect>();

				try
				{
					GameObject.Destroy(scrollRect.content.GetComponent<LayoutGroup>());
				}
				catch { }
				var grid = scrollRect.content.GetOrAddComponent<GridLayoutGroup>();

				int space = 7;
				grid.constraintCount = 3;
				grid.padding = new RectOffset(space, space, (int)(space * .5f), (int)(space * .5f));
				grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
				grid.startCorner = GridLayoutGroup.Corner.UpperLeft;


				gui.ControlObject.SetActive(false);
			});
			#endregion

			e.AddControl(new MakerButton("Add", category, inst))
				.OnGUIExists((gui) => inst.StartCoroutine(AddToBottomGUILayoutCO(gui, horizontal: true)));
			e.AddControl(new MakerButton("Remove", category, inst))
				.OnGUIExists((gui) => inst.StartCoroutine(AddToBottomGUILayoutCO(gui, horizontal: true)));
			e.AddControl(new MakerDropdown("A Cool Drop", new[] { "thing 1", "thing 2", "thing 3", "thing 4", }, category, 0, inst))
				.OnGUIExists((gui) => inst.StartCoroutine(AddToBottomGUILayoutCO(gui, horiScale: .50f, horizontal: true)));

			e.AddControl(new MakerButton("Test", category, inst))
				.OnGUIExists((gui) => inst.StartCoroutine(AddToBottomGUILayoutCO(gui, horizontal: false)));

		}

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
			vlg.childAlignment = TextAnchor.LowerCenter;
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
			viewLE.minHeight = scrollRect.transform.GetComponent<RectTransform>().rect.height * .75f;
			//viewLE.minHeight = -1;

			viewLE.flexibleHeight = -1;
			viewLE.flexibleWidth = -1;



			//Create Horizontal Layout
			if(horizontal)
			{
				//if(gui.ControlObject.GetComponent<HorizontalLayoutGroup>())
				//{
				//	var tmp = GameObject.Instantiate<GameObject>(new GameObject("Ctrl Object"), gui.ControlObject.transform.parent);
				//	gui.ControlObject.transform.SetParent(tmp.transform);
				//	//ctrlObj = tmp;
				//	//par = tmp?.transform.parent;
				//	//scrollRect = ctrlObj.GetComponentInParent<ScrollRect>();
				//
				//}

				//Create Layout Element GameObject
				FashionLine_Core.Logger.LogInfo($"Horizontal layout start {par.hierarchyCount}");
				par = newVertLine ?
					GameObject.Instantiate<GameObject>(new GameObject("LayoutElement"), par)?.transform :
					par.GetComponentsInChildren<HorizontalLayoutGroup>(2)
					.LastOrNull((elem) => elem.GetComponent<HorizontalLayoutGroup>())?.transform.parent ??
					GameObject.Instantiate<GameObject>(new GameObject("LayoutElement"), par)?.transform;

				par = par.gameObject.GetOrAddComponent<RectTransform>().transform;//May need this line (I totally do)

				par.GetOrAddComponent<LayoutElement>();

			}
			//calculate base GameObject sizeing
			if(horizontal)
			{

				var ele = par.GetOrAddComponent<LayoutElement>();
				ele.minWidth = -1;
				ele.minHeight = -1;
				ele.preferredHeight = Math.Max(ele?.preferredHeight ?? -1, par.GetComponentsInChildren<LayoutElement>()?.LastOrNull()?.preferredHeight ?? ele?.preferredHeight ?? -1);
				ele.preferredWidth =
#if HONEY_API
				scrollRect.GetComponent<RectTransform>().rect.width;
#else
				viewLE.minWidth;
#endif

				par.GetComponentInParent<VerticalLayoutGroup>().CalculateLayoutInputHorizontal();
				par.GetComponentInParent<VerticalLayoutGroup>().CalculateLayoutInputVertical();
				FashionLine_Core.Logger.LogInfo("Horizontal layout end");

			}


			//Create and Set Horizontal Layout Settings
			if(horizontal)
			{

				//if(gui.ControlObject.GetComponent<HorizontalLayoutGroup>())
				//{
				//	var tmp = GameObject.Instantiate<GameObject>(new GameObject("Ctrl Object"), gui.ControlObject.transform.parent);
				//	gui.ControlObject.transform.SetParent(tmp.transform);
				//	par = tmp?.transform;
				//}

				par = par.GetComponentsInChildren<HorizontalLayoutGroup>(2)?
					.LastOrNull((elem) => elem.gameObject.GetComponent<HorizontalLayoutGroup>())?.transform ??
					GameObject.Instantiate<GameObject>(new GameObject("HorizontalLayoutGroup"), par)?.transform;
				par = par.gameObject.GetOrAddComponent<RectTransform>().transform;//May need this line (I totally do)

				var layout = par.GetOrAddComponent<HorizontalLayoutGroup>();

				FashionLine_Core.Logger.LogInfo("Parent created");

				if(par == null)
					FashionLine_Core.Logger.LogInfo("Parent is null");

				layout.childControlWidth = true;
				layout.childControlHeight = true;
				layout.childForceExpandWidth = true;
				layout.childForceExpandHeight = true;
				layout.childAlignment = TextAnchor.MiddleCenter;

				par?.GetComponent<RectTransform>()?.ScaleToParent2D();
				FashionLine_Core.Logger.LogInfo($"Rect Size: {layout.GetComponent<RectTransform>().rect.size}");
				FashionLine_Core.Logger.LogInfo($"Preferred Width: {layout.preferredWidth}");

			}


			//Set this object's Layout settings
			if(cfg.debug.Value) FashionLine_Core.Logger.LogDebug("setting as last");
			ctrlObj.transform.SetParent(par);
			ctrlObj.transform.SetAsLastSibling();
			var thisLE = ctrlObj.GetOrAddComponent<LayoutElement>();

			if(thisLE.transform.childCount > 0)
				thisLE.transform.GetChild(0).GetComponent<RectTransform>().ScaleToParent2D();



			thisLE.flexibleWidth = horizontal ? -1f : -1f;
			//thisLE.;
			thisLE.minWidth =
#if HONEY_API
				horizontal && horiScale > 0 ? par.GetComponent<RectTransform>().rect.width * horiScale : -1f;
#else
				horizontal && horiScale > 0 ? viewLE.minWidth * horiScale: -1f ;
#endif
			thisLE.preferredWidth = -1;//horizontal ? -1f : scrollRect.content.rect.width * .95f;


			thisLE.flexibleHeight = -1f;
			thisLE.preferredHeight = ctrlObj.GetComponent<RectTransform>().rect.height;


			//var ele2 = par.GetComponent<HorizontalLayoutGroup>();
			//ele2?.CalculateLayoutInputHorizontal();
			//ele2?.CalculateLayoutInputVertical();
			//FashionLine_Core.Logger.LogInfo($"Rect Size: {ele2?.GetComponent<RectTransform>().sizeDelta.ToString() ?? "Null"}");
			//FashionLine_Core.Logger.LogInfo($"Preferred Width: {ele2?.preferredWidth.ToString() ?? "Null"}");

			//Reorder Scrollbar
			scrollRect.verticalScrollbar.transform.SetAsLastSibling();
			yield break;
		}

		static IEnumerator AddCoordinateCO(ChaFileCoordinate coordinate)
		{

			yield return new WaitWhile(() => gridLayout == null);
			var comp = GameObject.Instantiate<GameObject>(template.ControlObject, template.ControlObject.transform.parent);
			var img = comp.GetComponentInChildren<Image>();

			img.material.mainTexture = coordinate.pngData.LoadTexture(TextureFormat.RGBA32);



			comp.SetActive(true);
			yield break;
		}
	}
}
