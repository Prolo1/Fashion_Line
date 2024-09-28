using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

using ProloAPI;
using ProloAPI.Extensions;

using HarmonyLib;
using KKAPI.Maker;
using KKAPI.Utilities;
using Manager;

#if HONEY_API
using AIChara;
using CharaCustom;
#else
using ChaCustom;
#endif

using static ProloAPI.Utilities.PGeneral;
namespace FashionLine
{
	public partial class FashionLine_Core
	{
		public static string LastCoordSaveLocation { get; private set; }
			= FashionLine_GUI.DefaultCoordDirectory;
		public static ChaFileCoordinate LastCoord { get; private set; }
			= null;

		public static class Hooks
		{
			public static void Init()
			{
				var harm = Harmony.CreateAndPatchAll(typeof(Hooks), GUID);

				//	harm.UnpatchSelf();//for pausing Hook execution 
			}

#if KOI_API
			/// <summary>
			/// Set default coordinate to current costume slot
			/// </summary>
			/// <param name="__instance"></param>
			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
			static void OnClothingTypeChange(ChaControl __instance)
			{

				if(!__instance.chaFile.status.coordinateType.InRange(0, __instance.chaFile.coordinate.Length)) return;

				var ctrl = __instance.GetComponent<FashionLine_Controller>();
				var coord = __instance.chaFile.coordinate[__instance.chaFile.status.coordinateType];

				Logger.LogMessage("clothing about to be changed");
				__instance.StartCoroutine(func());
				IEnumerator func()
				{
					yield return null;
					//save init outfit
					ctrl.defaultCoord.LoadBytes(coord.SaveBytes(), coord.loadVersion);
					ctrl.defaultCoord.pngData = coord?.pngData?.ToArray();//copy
				}

				Logger.LogMessage("clothing type changed");
			}
#endif
			[HarmonyPrefix]
			[HarmonyPatch(typeof(ChaFileCoordinate), nameof(ChaFileCoordinate.SaveFile))]
			static void OnPreCoordSave(ChaFileCoordinate __instance, string __0)
			{
				SetLastSaveLocation(__0);
				SetLastCoord(__instance);
			}

			public static bool iscoordsavefinished = true;
			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileCoordinate), nameof(ChaFileCoordinate.SaveFile))]
			static void OnPostCoordSave() => iscoordsavefinished = true;

			static void SetLastSaveLocation(string path) =>
				LastCoordSaveLocation = path ?? LastCoordSaveLocation;

			static void SetLastCoord(ChaFileCoordinate inst) =>
				LastCoord = inst ?? LastCoord;

			[HarmonyPostfix]
			[HarmonyPatch(typeof(Button), nameof(Button.OnPointerClick))]
			static void OnPreButtonClick(Button __instance, PointerEventData __0)
			{
				if(!__instance.interactable) return;
				OnCreateNSaveToFashionLine(__instance, __0);
				OnSaveToFashionLineOnly(__instance, __0);

			}

			public static void OnCreateNSaveToFashionLine(Button btn, PointerEventData data)
			{
				if(!MakerAPI.InsideMaker) return;
				if(data.button != PointerEventData.InputButton.Left) return;

#if HONEY_API
				if(btn == null) return;
				if(btn != FashionLine_GUI.coordToFashionBtn) return;
				var orig = FashionLine_GUI.clothesSave.clothesLoadWin.button[1];
				//btn.onClick.ActuallyRemoveAllListeners();

				bool flag = false;
				Coroutine tmp1CO = null;
				//Coroutine tmp2CO = null;

				void btnFunc()
				{
					//	Logger.LogInfo("clicked button");

					UnityAction save = () =>
					{
						iscoordsavefinished = false;
						IEnumerator func()
						{
							FileStream stream = null;
							CoordData coordData = null;

							//	Logger.LogInfo("Waiting on coord save");
							while(!iscoordsavefinished) yield return null;
							//	Logger.LogInfo("Coord save complete");

							try
							{
								stream = new FileStream(LastCoordSaveLocation, FileMode.Open, FileAccess.Read);
								coordData = new CoordData()
								{
									data = stream.ReadAllBytes(),
									name = LastCoord.coordinateName
								};

								stream.Close();
								//	stream.Dispose();
							}
							catch(Exception ex)
							{
								stream?.Close();
								//stream?.Dispose();

								Logger.LogError(ex);
							}

							if(coordData != null)
								yield return Instance.StartCoroutine(MakerAPI.GetCharacterControl()
									.GetComponent<FashionLine_Controller>()
									.AddFashionCo(0, LastCoord.coordinateName, coordData));

							//Logger.LogInfo("ran new listener");
							flag = true;

							yield break;
						}

						tmp1CO = Instance.StartCoroutine(func());
					};

					UnityAction cancel = () =>
					{

						flag = true;
						//	Logger.LogInfo("ran new listener back");
					};

					IEnumerator Killme()
					{
						FashionLine_GUI.clothesSave?.clothesNameInput?.
							btnEntry?.onClick.AddListener(save);
						FashionLine_GUI.clothesSave?.clothesNameInput?.
							btnBack?.onClick.AddListener(cancel);

						while(!flag) yield return null;

						FashionLine_GUI.clothesSave?.clothesNameInput?.
							btnEntry?.onClick.RemoveListener(save);
						FashionLine_GUI.clothesSave?.clothesNameInput?.
							btnBack?.onClick.RemoveListener(cancel);

						//	Instance.StopCoroutine(tmp1CO);

						//Logger.LogInfo("removed added listener");

						yield break;
					}

					Instance.StartCoroutine(Killme());

					orig?.onClick?.Invoke();//try running after CoRoutine
				}
				btnFunc();
#endif
			}

			public static void OnSaveToFashionLineOnly(Button btn, PointerEventData data)
			{
				if(!MakerAPI.InsideMaker) return;
				if(data.button != PointerEventData.InputButton.Left) return;


#if HONEY_API
				if(btn == null) return;
				if(btn != FashionLine_GUI.toFashionOnlyBtn) return;
				var orig = FashionLine_GUI.clothesSave.clothesLoadWin.button[1];
				//btn.onClick.ActuallyRemoveAllListeners();

				bool flag = false;
				Coroutine tmp1CO = null;
				//Coroutine tmp2CO = null;

				void btnFunc()
				{
					//	Logger.LogInfo("clicked button");

					UnityAction save = () =>
					{
						iscoordsavefinished = false;
						IEnumerator func()
						{
							FileStream stream = null;
							CoordData coordData = null;

							//	Logger.LogInfo("Waiting on coord save");
							while(!iscoordsavefinished) yield return null;
							//	Logger.LogInfo("Coord save complete");

							try
							{
								stream = new FileStream(LastCoordSaveLocation, FileMode.Open, FileAccess.Read);
								coordData = new CoordData()
								{
									data = stream.ReadAllBytes(),
									name = LastCoord.coordinateName
								};

								stream.Close();

								File.Delete(LastCoordSaveLocation);
								//	stream.Dispose();
							}
							catch(Exception ex)
							{
								stream?.Close();
								//stream?.Dispose();

								Logger.LogError(ex);
							}

							if(coordData != null)
								yield return Instance.StartCoroutine(MakerAPI.GetCharacterControl()
									.GetComponent<FashionLine_Controller>()
									.AddFashionCo(0, LastCoord.coordinateName, coordData));

							//	Logger.LogInfo("ran new listener");
							flag = true;

							yield break;
						}

						tmp1CO = Instance.StartCoroutine(func());
					};

					UnityAction cancel = () =>
					{

						flag = true;
						//	Logger.LogInfo("ran new listener back");
					};

					IEnumerator Killme()
					{
						FashionLine_GUI.clothesSave?.clothesNameInput?.
							btnEntry?.onClick.AddListener(save);
						FashionLine_GUI.clothesSave?.clothesNameInput?.
							btnBack?.onClick.AddListener(cancel);

						while(!flag) yield return null;

						FashionLine_GUI.clothesSave?.clothesNameInput?.
							btnEntry?.onClick.RemoveListener(save);
						FashionLine_GUI.clothesSave?.clothesNameInput?.
							btnBack?.onClick.RemoveListener(cancel);

						//	Instance.StopCoroutine(tmp1CO);

						//	Logger.LogInfo("removed added listener");

						yield break;
					}

					Instance.StartCoroutine(Killme());

					orig?.onClick?.Invoke();//try running after CoRoutine
				}
				btnFunc();
#endif

			}
		}
	}
}
