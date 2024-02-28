using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events; 
using UnityEngine.EventSystems;
using TMPro;

using BepInEx;
using KKAPI;
using KKAPI.Studio;
using KKAPI.Studio.UI;
using KKAPI.Utilities;
using KKAPI.Maker;
using KKAPI.Maker.UI;

using UniRx;
using Studio;
using HarmonyLib;
using Illusion.Game;

#if HONEY_API
using AIChara;
using CharaCustom;
#else
using ChaCustom;
#endif

using static KKAPI.Maker.MakerAPI;
using static KKAPI.Studio.StudioAPI;
using static FashionLine.FashionLine_Core;
using static FashionLine.FashionLine_Util;
//using static FashionLine.FashionLine_Util;
//using static Illusion.Game.Utils;

namespace FashionLine
{
	public class FashionLine_GUI : MonoBehaviour
	{
		#region Classes

		class IEnumerableCompare<T> : IEqualityComparer<IEnumerable<T>>
		{

			public bool Equals(IEnumerable<T> x, IEnumerable<T> y)
			{
				if(x.Count() != y.Count()) return false;
				for(int a = 0; a < x.Count(); ++a)
					if((object)x.ElementAt(a) != (object)y.ElementAt(a)) return false;

				return true;
			}



			public int GetHashCode(IEnumerable<T> obj)
			{
				return obj.GetHashCode();
			}


		}

		class OnlyKeyCompare<K, V> : IEqualityComparer<KeyValuePair<K, V>>
		{

			public bool Equals(KeyValuePair<K, V> x, KeyValuePair<K, V> y)
			{

				return (object)x.Key != (object)y.Key;
			}

			public int GetHashCode(KeyValuePair<K, V> obj)
			{
				return obj.GetHashCode();
			}
		}

		#endregion

		#region Data
		static readonly string[] sortOptions = new[] { "default", "default rev.", "name", "name rev.", "Created", "Created rev.", "Updated", "Updated rev." };

		#region Main Game
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

		#region Studio
		static CurrentStateCategory categoryStudio;
		internal static Texture2D userTexUI = Texture2D.blackTexture;
		internal static UnityEvent customUI = new UnityEvent();
		internal static Rect winRec = new Rect(105, 390, 440, 600);
		internal static Rect offsetRect = new Rect(new Vector2(winRec.width, 0), new Vector2(200, 200));
		static ToolbarToggle tgl;
		static Rect sortRect = new Rect();
		static bool enableStudioUI = false;
		static bool enableStudioUISort = false;
		static Func<int> sortDropdown = null;
		static int sortVal = -1;
		#endregion

		#endregion

		void OnGUI()
		{
			if(!StudioAPI.StudioLoaded || !enableStudioUI) return;


			var camCtrl = Studio.Studio.Instance.cameraCtrl;

			var colour1 = GUI.color;
			var colour2 = GUI.contentColor;
			var colour3 = GUI.backgroundColor;

			GUI.color = Color.white;
			GUI.contentColor = Color.white;
			var bgTex = cfg.enableBGUI.Value ? userTexUI ??
					(cfg.useCreatorDefaultBG.Value ? UIGoku : greyTex) :
					(cfg.useCreatorDefaultBG.Value ? UIGoku : greyTex);

			GUI.DrawTexture(winRec = GUI.Window(0,
				winRec, id =>
				{
					//var studioCtrl = Studio.Studio.Instance;
					//var camCtrl = studioCtrl.cameraCtrl;

					customUI.Invoke();

					winRec = IMGUIUtils.DragResizeEatWindow(id, winRec);


					if(!cfg.studioWinRec.Value.Equals(winRec))
						cfg.studioWinRec.Value = new Rect(winRec);
				}, ModName),
				bgTex,
				ScaleMode.StretchToFill);

			sortRect = new Rect(offsetRect.position + winRec.position, offsetRect.size);
			if(enableStudioUISort)
				GUI.DrawTexture(sortRect = GUI.Window(1, sortRect,
					(id) =>
					{
						colour1 = GUI.color;
						colour2 = GUI.contentColor;
						colour3 = GUI.backgroundColor;

						GUI.contentColor = Color.white;

						if(sortDropdown == null)
							sortDropdown = FashionLine_Util.GUILayoutDropdownDrawer
							((x, index) => new GUIContent() { text = x[index] }
							, sortOptions, -1,
							onSelect: (selected) =>
							{
								//enableStudioUISort = !enableStudioUISort; 
								return selected;
							});

						sortVal = sortDropdown();

						GUI.color = colour1;
						GUI.contentColor = colour2;
						GUI.backgroundColor = colour3;

						GUI.DragWindow();
						IMGUIUtils.EatInputInRect(sortRect);
						offsetRect = new Rect(sortRect.position - winRec.position, sortRect.size);

						if(offsetRect != cfg.studioSortOffset.Value)
							cfg.studioSortOffset.Value = offsetRect;

					}, "Sort Options"),
					bgTex,
					ScaleMode.StretchToFill);

			GUI.color = colour1;
			GUI.contentColor = colour2;
			GUI.backgroundColor = colour3;
		}

		void OnDestroy()
		{
			Cleanup();
		}

		public static void Init()
		{

			if(InsideStudio)
			{

				StudioLoadedChanged += (s, e) =>
				{
					//Allow OnGUI() to run
					var obj = new GameObject();
					obj.AddComponent<FashionLine_GUI>();
					obj.transform.SetAsLastSibling();
					obj.name = "FashionLine_GUI";

					CustomToolbarButtons.AddLeftToolbarToggle
						(new Texture2D(32, 32),
						onValueChanged: val =>
						{
							enableStudioUI = val;
						}).OnGUIExists(gui =>
						{
							//Toggle image bi-pass

							iconBG.filterMode = FilterMode.Bilinear;
							var btn = gui.ControlObject.GetComponentInChildren<Button>();
							btn.image.sprite =
							Sprite.Create(iconBG,
							new Rect(0, 0, iconBG.width, iconBG.height),
							Vector2.one * .5f);

							btn.image.color = Color.white;
						});
				};

				#region Init Values
				Dictionary<CoordData, Texture2D> costumes = new Dictionary<CoordData, Texture2D>();
				Vector2 scrollPos = Vector2.zero;
				CoordData selectKey = null;

				costumes.ObserveEveryValueChanged((l) => l.Keys.ToList(), FrameCountType.Update,
					new IEnumerableCompare<CoordData>())
					.Subscribe((val) =>
					{
						foreach(var costume in costumes.ToList())
							costumes[costume.Key] = costume.Key.data.LoadTexture();
						//	FashionLine_Core.Logger.LogDebug("the costumes have updated");
					});

				string tooltip = "";
				string search = "";
				int selectNum = -1;

				winRec = new Rect(cfg.studioWinRec.Value);
				offsetRect = new Rect(cfg.studioSortOffset.Value);
				#endregion

				//update loop
				customUI.AddListener(() =>
				{
					GUILayout.BeginVertical();

					//Currently selected Coordinate
					var tmpSty = new GUIStyle(GUI.skin.label);

					var sel = selectKey != null ?
					 selectKey.name : "";

					tmpSty.fontStyle = selectKey == null ? FontStyle.Italic : FontStyle.Normal;
					tmpSty.alignment = TextAnchor.LowerLeft;
					tmpSty.wordWrap = true;
					tmpSty.normal.textColor = tooltip.IsNullOrWhiteSpace() && selectKey != null ?
					Color.green : Color.white;
					sel = tooltip.IsNullOrWhiteSpace() ? sel : tooltip;

					int fws = (int)(Mathf.Clamp(winRec.width, .001f, winRec.width) / 16);
					tmpSty.fontSize = Math.Min(75, (int)(fws/* * (1.0f / sel.Length * 15)*/));

					GUILayout.Label(sel, tmpSty, GUILayout.Height(tmpSty.lineHeight));
					float txtH = GUILayoutUtility.GetLastRect().height;


					//Search Bar
					GUILayout.BeginHorizontal();

					tmpSty = new GUIStyle(GUI.skin.textField);
					tmpSty.alignment = TextAnchor.LowerLeft;
					tmpSty.fontSize = (int)(fws * 0.95f);
					tmpSty.fontStyle = search.IsNullOrWhiteSpace() ? FontStyle.Italic : FontStyle.Normal;
					//tmpSty.overflow= true;
					tmpSty.wordWrap = true;

					search = GUILayout.TextField(search, tmpSty,
						GUILayout.Height(tmpSty.lineHeight));
					if(search.IsNullOrEmpty())
					{
						tmpSty = new GUIStyle() { fontStyle = FontStyle.Italic, fontSize = fws };
						tmpSty.normal.textColor = GUI.skin.textField.normal.textColor;
						GUI.Label(GUILayoutUtility.GetLastRect(), "Search...", tmpSty);
					}
					txtH += GUILayoutUtility.GetLastRect().height;


					//Sort Button
					if(GUILayout.Button("Sort",
						GUILayout.Width(winRec.width * .20f),
						GUILayout.Height(tmpSty.lineHeight)))
						enableStudioUISort = !enableStudioUISort;

					GUILayout.EndHorizontal();

					//Card view Window
					scrollPos = GUILayout.BeginScrollView(scrollPos, false, true,
						GUILayout.Height((winRec.height - txtH) * .65f), GUILayout.ExpandWidth(true));

					var lists = StudioAPI.GetSelectedControllers<FashionLineController>();
					var tmp = new Dictionary<CoordData, Texture2D>();
					foreach(var list in lists)
						foreach(var list2 in list.fashionData)
							tmp[list2.Value] = costumes.TryGetValue(list2.Value, out var val1) ? val1 : null;

					foreach(var list in costumes.Except
					(tmp, new OnlyKeyCompare<CoordData, Texture2D>()).ToList())
						costumes.Remove(list.Key);

					foreach(var list in tmp)
						costumes[list.Key] = list.Value;


					if(costumes.Count > 0)
					{

						var tmporder = costumes.
						Where(val => (val.Key.translatedName + $" {val.Key.name}").Search(search.Replace(" ", ""))
						|| search.IsNullOrWhiteSpace()).ToList();

						var sort =
						new Func<KeyValuePair<CoordData, Texture2D>, object>
						((k) =>
						{
							switch(sortVal / 2)
							{
							case 0:
								return (object)tmporder.IndexOf(k);
							case 1:
								return (object)k.Key.translatedName.ToLower().Trim();
							case 2:
								return (object)k.Key.created;
							case 3:
								return (object)k.Key.updated;
							default:
								return (object)tmporder.IndexOf(k);
							}
						});

						if(sortVal % 2 == 0)
							tmporder = tmporder.OrderBy(sort).ToList();
						else
							tmporder = tmporder.OrderByDescending(sort).ToList();


						GUIContent[] content = tmporder.Attempt(
							v => new GUIContent() { image = v.Value, tooltip = v.Key.name }).ToArray();

						var myStyle = new GUIStyle() { alignment = TextAnchor.LowerCenter };
						myStyle.normal.textColor = Color.white.RGBMultiplied(.9f);
						myStyle.focused.textColor = Color.cyan;
						myStyle.wordWrap = true;

						float w = (winRec.width - (100 * cfg.studioUIWidth.Value));
						float h = ((w == 0 ? .001f : w) / 3 * 1.5f * Mathf.Ceil(content.Length / 3.0f));
						myStyle.fontSize = Mathf.CeilToInt(w / 18);
						myStyle.padding.left = (int)(w * (1 / 3.0f) * .07f);
						myStyle.padding.right = (int)(w * (1 / 3.0f) * .07f);
						myStyle.padding.bottom = (int)(h / Mathf.Ceil(content.Length / 3.0f) * 0.12f);

						selectNum = GUILayout.SelectionGrid(selectNum, content, 3,
							GUILayout.Width(w),
							GUILayout.Height(h));

						//overlay
						GUI.SelectionGrid(GUILayoutUtility.GetLastRect(), selectNum,
							content.Attempt(v => new GUIContent() { text = v.tooltip, tooltip = v.tooltip }).ToArray()
							, 3, myStyle);

						selectKey = tmporder.InRange(selectNum) ?
						tmporder.ElementAt(selectNum).Key : null;

						tooltip = GUI.mouseTooltip;
					}
					else
					{
						selectNum = -1;
						selectKey = null;
						tooltip = "";
					}

					//Bottom Buttons
					var colour1 = GUI.color;
					var colour2 = GUI.contentColor;
					var colour3 = GUI.backgroundColor;

					GUI.color = Color.white;
					GUI.contentColor = Color.white;

					tmpSty = new GUIStyle(GUI.skin.button);
					tmpSty.normal.textColor = tmpSty.normal.textColor.AlphaMultiplied(lists.Any() ? 1 : 0.60f);
					if(!lists.Any())
						tmpSty.active = tmpSty.hover = tmpSty.normal;

					GUILayout.EndScrollView();
					GUILayout.BeginHorizontal();
					if(GUILayout.Button("Wear Selected", tmpSty))
						foreach(var fashion in lists)
							if(selectKey != null)
								fashion.WearFashion(selectKey);

					if(GUILayout.Button("Wear Defult", tmpSty))
						foreach(var fashion in lists)
							fashion.WearDefaultFashion();
					GUILayout.EndHorizontal();

					GUILayout.BeginHorizontal();

					if(GUILayout.Button("Load Coordinate[s]", tmpSty) && lists.Any())
					{
						ForeGrounder.SetCurrentForground();
						GetNewImageTarget();
					}
					//if(GUILayout.Button("load current coordinate"));

					GUILayout.EndHorizontal();

					GUI.color = Color.white;
					GUI.contentColor = Color.red;
					GUI.backgroundColor = Color.white.RGBMultiplied(0.35f);

					GUILayout.Space(5);
					GUILayout.Label("DANGER ZONE");
					GUILayout.Space(5);

					GUILayout.BeginHorizontal();
					if(GUILayout.Button("Remove selected"))
						foreach(var fashion in lists)
							if(selectKey != null)
								fashion.RemoveFashion(selectKey);

					if(GUILayout.Button("Remove All"))
						foreach(var fashion in lists)
							foreach(var all in fashion.fashionData.Values.ToList())
								fashion.RemoveFashion(all);

					GUI.color = colour1;
					GUI.contentColor = colour2;
					GUI.backgroundColor = colour3;

					GUILayout.EndHorizontal();

					GUILayout.EndVertical();

				});

			}
			else
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
				MakerBaseLoaded += (s, e) => { AddFashionLineMenu_Maker(e); };
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
						txtpro.extraPadding = true;
						txtpro.alignment = TextAlignmentOptions.Center;
						txtpro.fontStyle = FontStyles.Bold;
						txtpro.color = Color.black;
						txtpro.enableAutoSizing = true;
						txtpro.fontSizeMax = 100;
						txtpro.fontSizeMin = 1;
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
			enableStudioUI = false;
			customUI.RemoveAllListeners();
			winRec = cfg.studioWinRec.Value;

			if(isPersistantHndl != null)
				cfg.areCoordinatesPersistant.SettingChanged -= isPersistantHndl;

		}

		static void AddFashionLineMenu_Maker(RegisterCustomControlsEvent e)
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

					var gridPar = new GameObject("Grid Layout Obj");
					gridPar.transform.parent = scrollRect.content;
					gridPar.AddComponent<RectTransform>();
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

					var txtObj = new GameObject();
					txtObj.transform.parent = tgl.transform;
					var txt = txtObj.AddComponent<TextMeshProUGUI>();
					txtObj.ScaleToParent2D(pwidth: .9f, pheight: .95f);

					yield return new WaitUntil(() => txt.fontMaterial != null);//wait for TMPro to instantiate

					txt.fontMaterial.shaderKeywords =
					txt.fontMaterial.shaderKeywords?.AddItem("OUTLINE_ON").ToArray();

					//FashionLine_Core.Logger
					//.LogDebug($"Material keywords:\n[{string.Join(",\n", txt.fontMaterial.shaderKeywords)}]");

					//	txt.material = txt.fontMaterial;
					txt.alignment = TextAlignmentOptions.Bottom;
					txt.color = Color.white;

					txt.outlineColor = Color.black;
					txt.outlineWidth = 0.2f;
					txt.autoSizeTextContainer = false;
					txt.enableAutoSizing = true;
					txt.raycastTarget = false;
					txt.enableWordWrapping = true;
					txt.fontSizeMax = 30;
					txt.fontSizeMin = 7;
					txt.SetAllDirty();

					gui.ControlObject.SetActive(false);

					yield break;
				}

				inst.StartCoroutine(SetupCo());
			});
			#endregion

			#region Top
			costTxt = e.AddControl(new MyMakerText("Costume Name", category, inst))
				.AddToCustomGUILayout(topUI: true, newVertLine: false, pWidth: 0.70f);

			e.AddControl(new MakerDropdown(settingName: "", options: sortOptions, initialValue: 0, category: category, owner: inst))
				.AddToCustomGUILayout(topUI: true, newVertLine: false, pWidth: 0.25f)
				.OnGUIExists(gui =>
				{
					gui.ControlObject.GetTextComponentInChildren()?.gameObject.SetActive(false);

					gui.ValueChanged.Subscribe((val) =>
					{
						var sort =
						new Func<KeyValuePair<string, CoordData>, object>
						((k) =>
						{
							switch(val / 2)
							{
							case 0:
								return (object)fashCtrl.fashionData.ToList().IndexOf(k);
							case 1:
								return (object)k.Value.translatedName.ToLower().Trim();
							case 2:
								return (object)k.Value.created;
							case 3:
								return (object)k.Value.updated;
							default:
								return (object)fashCtrl.fashionData.ToList().IndexOf(k);
							}
						});

						List<KeyValuePair<string, CoordData>> order;
						if(val % 2 == 0)
							order = fashCtrl.fashionData.OrderBy(sort).ToList();
						else
							order = fashCtrl.fashionData.OrderByDescending(sort).ToList();

						order.Do(valu =>
						{
							var tgl = (Toggle)valu.Value.extras.FirstOrNull(obj => obj is Toggle);
							tgl.transform.parent.SetAsLastSibling();
						});

					});
				});

			e.AddControl(new MakerTextbox(settingName: "Search:", defaultValue: "", category: category, owner: inst))
				.AddToCustomGUILayout(topUI: true, newVertLine: true)
				.OnGUIExists((gui) =>
				{
					var input = gui.ControlObject?.GetComponentInChildren<InputField>();
					input.textComponent.alignment = TextAnchor.MiddleLeft;

					input.ObserveEveryValueChanged((k) => k.text)
					.Subscribe((val) =>
					{
						//	var rgxOp = RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

						//de-activate all
						fashCtrl.fashionData
						.Do((k) =>
						{
							var tgl = (Toggle)k.Value.extras.Find((obj) => obj is Toggle);
							if(tgl != null)
								tgl.transform.parent.gameObject.SetActive(false);
						});

						//find search
						fashCtrl.fashionData
						.Where((k) => (k.Value.translatedName + k.Value.name).Search(val.Replace(" ", ""))
						|| val.IsNullOrWhiteSpace())
							.Do((k) =>
							{
								var tgl = (Toggle)k.Value.extras.FirstOrNull((obj) => obj is Toggle);
								if(tgl != null)
									tgl.transform.parent.gameObject.SetActive(true);
							});

					});

					//	((Behaviour)gui.ControlObject.GetTextComponentInChildren()).enabled = false;

					//input.MarkGeometryAsDirty();
					var placehold = ((Text)input.placeholder);
					placehold.text = "Search...";
					placehold.alignment = TextAnchor.MiddleLeft;

				});

			e.AddControl(new MakerSeparator(category, inst))
				.AddToCustomGUILayout(topUI: true);

			#endregion

			#region Bottom
			e.AddControl(new MakerToggle(category, "Make FashionLine Persistant", inst))
					.AddToCustomGUILayout(newVertLine: true)
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
				.AddToCustomGUILayout(newVertLine: true)
				.OnClick.AddListener(() =>
				{
					if(!tglGroup.AnyTogglesOn()) return;

					fashCtrl.WearFashion(currentCoord, reload: true);
					Illusion.Game.Utils.Sound.Play(SystemSE.ok_l);
				});

			e.AddControl(new MyMakerButton("Wear Default", category, inst))
				.AddToCustomGUILayout(newVertLine: false)
				.OnClick.AddListener(() =>
				{
					fashCtrl.WearDefaultFashion(reload: true);
					Illusion.Game.Utils.Sound.Play(SystemSE.ok_l);
				});

			e.AddControl(new MyMakerButton("Load Coordinate[s]", category, inst))
				.AddToCustomGUILayout(newVertLine: true)
				.OnClick.AddListener(() =>
				{
					ForeGrounder.SetCurrentForground();
					GetNewImageTarget();
				});

			e.AddControl(new MyMakerButton("Add Current Coordinate", category, inst))
				.AddToCustomGUILayout(newVertLine: false)
				.OnClick.AddListener(() =>
				{
					Hooks.OnSaveToFashionLineOnly(toFashionOnlyBtn, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
				});

			e.AddControl(new MyMakerText("Danger Zone", category, inst))
				.AddToCustomGUILayout(newVertLine: true)
				.OnGUIExists(gui =>
				{
					gui.TextColor = Color.red;
				});

			e.AddControl(new MyMakerButton("Remove Selected", category, inst))
				.AddToCustomGUILayout(newVertLine: true)
				.OnGUIExists((gui) =>
				{
					gui.TextColor = Color.red;
					gui.ButtonColor = Color.white.RGBMultiplied(0.35f);
				})
				.OnClick.AddListener(() =>
				{
					if(!tglGroup.AnyTogglesOn()) return;

					fashCtrl.RemoveFashion(in currentCoord);
					Illusion.Game.Utils.Sound.Play(SystemSE.cancel);
				});

			e.AddControl(new MyMakerButton("Remove All", category, inst))
				.AddToCustomGUILayout(newVertLine: false)
				.OnGUIExists((gui) =>
				{
					gui.TextColor = Color.red;
					gui.ButtonColor = Color.white.RGBMultiplied(0.35f);
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
			if(!MakerAPI.InsideMaker) return;

			var inst = FashionLine_Core.Instance;
			inst.StartCoroutine(AddCoordinateCO(coord));
		}

		public static void RemoveCoordinate(in CoordData coord)
		{
			if(!MakerAPI.InsideMaker) return;

			var inst = FashionLine_Core.Instance;
			inst.StartCoroutine(RemoveCoordinateCO(coord));
		}

		public static void RemoveAllCoordinates()
		{
			if(!MakerAPI.InsideMaker) return;

			var inst = FashionLine_Core.Instance;

			inst.StartCoroutine(RemoveAllCoordinatesCO());
		}


		#region Coroutine Helpers   

		static IEnumerator AddCoordinateCO(CoordData coordinate)
		{
			if(coordinate == null) yield break;

			yield return new WaitWhile(() => gridLayout == null);

			//	for( int a=0;a<12;++a)
			//		yield return null;

			var comp = GameObject.Instantiate<GameObject>(template.ControlObject, template.ControlObject.transform.parent);
			var img = comp.GetComponentInChildren<RawImage>();
			img.texture = coordinate.data?.LoadTexture(TextureFormat.RGBA32);

			var tgl = comp.GetComponentInChildren<Toggle>();
			var txt = tgl.GetComponentInChildren<TMP_Text>();
			coordinate.extras.Add(tgl);

			txt.text = coordinate.translatedName;

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
					else
						costTxt.TextColor = Color.yellow;
				}

				last = hover;
			});

			comp.SetActive(true);
			yield break;
		}

		static IEnumerator RemoveCoordinateCO(CoordData coordinate)
		{
			if(coordinate == null) yield break;

			yield return new WaitWhile(() => gridLayout == null);

			Toggle tmp = (Toggle)coordinate.extras.FirstOrNull((obj) => obj is Toggle);
			if(tmp)
				GameObject.Destroy(tmp.transform.parent.gameObject);

			if(tmp.isOn) currentCoord = null;

			yield break;
		}

		static IEnumerator RemoveAllCoordinatesCO()
		{

			yield return new WaitWhile(() => gridLayout == null);

			foreach(var tmp in tglGroup.ActiveToggles())
				GameObject.Destroy(tmp.transform.parent.gameObject);

			currentCoord = null;

			yield break;
		}

		#endregion

		#region File Stuff

		#region File Data
		public const string FileExt = ".png";
		public const string FileFilter = "Coordinate Images (*.png)|*.png";

		public static string DefaultCoordDirectory { get => (Directory.GetCurrentDirectory() + "/UserData/coordinate/").MakeDirPath("/", "\\"); }

		public static string TargetDirectory { get => Directory.Exists(cfg.lastCoordDir.Value) && !cfg.lastCoordDir.Value.IsNullOrWhiteSpace() ? cfg.lastCoordDir.Value : DefaultCoordDirectory; }

		#endregion

		private static string MakeDirPath(string path) => FashionLine_Util.MakeDirPath(path);

		public static void GetNewImageTarget()
		{
			//	OpenFileDialog.OpenSaveFileDialgueFlags.OFN_CREATEPROMPT;
			FashionLine_Core.Logger.LogInfo("Game Root Path: " + Directory.GetCurrentDirectory());

			var paths = OpenFileDialog.ShowDialog("Add New Coordinate[s] (You can select multiple)",
			Directory.Exists(cfg.lastCoordDir.Value) ?
			 cfg.lastCoordDir.Value : TargetDirectory,
			FileFilter,
			FileExt,
			OpenFileDialog.MultiFileFlags,
			owner: ForeGrounder.GetForgroundHandeler());

			var path = paths?.Attempt((s) => s.IsNullOrWhiteSpace() ?
			throw new Exception() : s).LastOrNull().MakeDirPath();

			cfg.lastCoordDir.Value = path?.Substring(0, path.LastIndexOf('/')) ?? TargetDirectory;

			OnImageTargetObtained(paths);
			if(paths.Any())
				Illusion.Game.Utils.Sound.Play(SystemSE.ok_l);
			else
				Illusion.Game.Utils.Sound.Play(SystemSE.cancel);
		}

		/// <summary>
		/// Called after a file is chosen in file explorer menu  
		/// </summary>
		/// <param name="strings: ">the info returned from file explorer. strings[0] returns the full file path</param>
		private static void OnImageTargetObtained(string[] strings)
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

				if(texPath.IsNullOrWhiteSpace())
				{
					continue;
				}

				//	var directory = Path.GetDirectoryName(texPath).MakeDirPath();
				var filename = texPath.Substring(texPath.LastIndexOf('/') + 1).MakeDirPath();//not sure why this happens on hs2?

				//use file

				var fashCtrls = InsideStudio ?
					StudioAPI.GetSelectedControllers<FashionLineController>() :
					MakerAPI.GetCharacterControl().GetComponents<FashionLineController>();


				var coord = new ChaFileCoordinate();
				coord.LoadFile(texPath);
				var name = filename.Substring(0, filename.LastIndexOf('.'));
				name = !coord.coordinateName.IsNullOrWhiteSpace() ?
					coord.coordinateName ?? name : name;

				var data = new CoordData()
				{
					data = File.ReadAllBytes(texPath),
					name = name,
					created = File.GetCreationTime(texPath),
					updated = File.GetLastWriteTime(texPath)
				};

				foreach(var ctrl in fashCtrls)
					ctrl.AddFashion(name, data);

			}

			if(cfg.debug.Value) FashionLine_Core.Logger.LogDebug($"Exit accept");
		}


		public static void GetNewBGUIPath()
		{
			//	OpenFileDialog.OpenSaveFileDialgueFlags.OFN_CREATEPROMPT;
			FashionLine_Core.Logger.LogInfo("Game Root Path: " + Directory.GetCurrentDirectory());

			var paths = OpenFileDialog.ShowDialog("Select Background",
			TargetDirectory.MakeDirPath("/", "\\"),
			FileFilter,
			FileExt,
			OpenFileDialog.SingleFileFlags,
			owner: ForeGrounder.GetForgroundHandeler());

			var path = paths?.Attempt((s) => s.IsNullOrWhiteSpace() ?
			throw new Exception() : s).LastOrNull().MakeDirPath();

			ForeGrounder.RevertForground();
			OnBGImageObtained(path);
		}

		private static void OnBGImageObtained(string path)
		{
			if(path.IsNullOrWhiteSpace()) return;

			cfg.bgUIImagepath.Value = path;
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
				get => ((Graphic)ControlObject.GetTextComponentInChildren()).color;
				set
				{
					var val = ((Graphic)ControlObject.GetTextComponentInChildren());
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
				get => ((Graphic)ControlObject.GetTextComponentInChildren()).color;
				set
				{
					var val = ((Graphic)ControlObject.GetTextComponentInChildren());
					val.color = value;
					val.SetAllDirty();
				}
			}
		}

		#region Studio
		class CurrentStateCategoryImage : CurrentStateCategorySubItemBase
		{
			private readonly BehaviorSubject<Texture> _texture;

			private int _width;

			private int _height;

			public int Width { get => _width; set { _width = value; _texture.OnNext(Image); } }

			public int Height { get => _height; set { _height = value; _texture.OnNext(Image); } }

			public Texture Image { get => _texture.Value; set { _texture.OnNext(value); } }

			public CurrentStateCategoryImage(Texture texture, int w = 100, int h = 100, string name = "") : base(name)
			{
				_texture = new BehaviorSubject<Texture>(texture);
				_width = w;
				_height = h;
			}

			protected override GameObject CreateItem(GameObject categoryObject)
			{
				GameObject gameObject = new GameObject("image", typeof(RectTransform), typeof(LayoutElement));
				gameObject.transform.SetParent(categoryObject.transform, worldPositionStays: false);
				gameObject.layer = 5;
				LayoutElement le = gameObject.GetComponent<LayoutElement>();
				le.minWidth = 456f;

				GameObject gameObject2 = new GameObject("img", typeof(RectTransform), typeof(CanvasRenderer));
				gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
				gameObject2.layer = 5;
				RawImage i = gameObject2.AddComponent<RawImage>();
				RectTransform irt = gameObject2.GetComponent<RectTransform>();

				_texture.Subscribe(delegate (Texture texture)
				{
					i.texture = texture;
					le.minHeight = Height + 30;
					irt.offsetMin = new Vector2((float)(-1 * Width) / 2f, (float)(-1 * Height) / 2f);
					irt.offsetMax = new Vector2((float)Width / 2f, (float)Height / 2f);
					le.enabled = false;
					le.enabled = true;
				});
				return gameObject;
			}

			protected override void OnUpdateInfo(OCIChar ociChar) { }

		}

		class CurrentStateCategoryButton : CurrentStateCategorySubItemBase
		{
			private readonly BehaviorSubject<Texture> _texture;
			public Texture Image { get => _texture.Value; set { _texture.OnNext(value); } }



			public CurrentStateCategoryButton(string name, Texture texture = null) : base(name)
			{
				_texture = new BehaviorSubject<Texture>(texture);
			}

			protected override GameObject CreateItem(GameObject categoryObject)
			{
				GameObject gameObject = new GameObject("button", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
				gameObject.transform.SetParent(categoryObject.transform, worldPositionStays: false);
				gameObject.layer = 5;
				LayoutElement le = gameObject.GetComponent<LayoutElement>();
				le.minWidth = 456f;

				GameObject gameObject2 = new GameObject("img", typeof(RectTransform), typeof(CanvasRenderer));
				gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
				gameObject2.layer = 5;
				RawImage i = gameObject2.AddComponent<RawImage>();
				RectTransform irt = gameObject2.GetComponent<RectTransform>();

				GameObject gameObject3 = new GameObject("txt", typeof(RectTransform));
				gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
				gameObject3.layer = 5;
				var txt = gameObject3.GetOrAddComponent<TextMeshProUGUI>().ScaleToParent2D(pwidth: .8f);
				txt.autoSizeTextContainer = false;
				txt.extraPadding = true;
				txt.alignment = TextAlignmentOptions.Center;
				txt.fontStyle = FontStyles.Bold;
				txt.color = Color.black;
				txt.enableAutoSizing = true;
				txt.fontSizeMax = 100;
				txt.fontSizeMin = 1;
				txt.SetAllDirty();

				_texture.Subscribe(delegate (Texture texture)
				{
					gameObject.GetComponent<Image>().enabled = texture == null;
					if(texture == null) return;

					int Height = 50;

					i.texture = texture;
					le.minHeight = Height + 30;
					irt.ScaleToParent2D();

					le.enabled = false;
					le.enabled = true;
				});
				return gameObject;
			}

			protected override void OnUpdateInfo(OCIChar ociChar) { }

		}

		#endregion

		#endregion
	}
}