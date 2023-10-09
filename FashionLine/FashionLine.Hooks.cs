using System;
using System.Collections.Generic;
using System.Text;

#if HONEY_API
using AIChara;
using CharaCustom;
#else
using ChaCustom;
#endif
using Manager;

using HarmonyLib;
using System.IO;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using KKAPI.Maker;
using KKAPI.Utilities;
using UnityEngine;

namespace FashionLine
{
	public partial class FashionLine_Core
	{
		public static string LastCoordSaveLocation { get; private set; }
			= FashionLine_GUI.DefaultCoordDirectory;
		public static ChaFileCoordinate LastCoord { get; private set; }
			= null;

		private static class Hooks
		{
			public static void Init()
			{
				Harmony.CreateAndPatchAll(typeof(Hooks), GUID);

			}

			[HarmonyPrefix]
			//#if HONEY_API
			//			[HarmonyPatch(typeof(CvsC_CreateCoordinateFile), nameof(CvsC_CreateCoordinateFile.CreateCoordinateFile))]
			//#endif
			[HarmonyPatch(typeof(ChaFileCoordinate), nameof(ChaFileCoordinate.SaveFile))]
			static void OnPreCoordSave(ChaFileCoordinate __instance, string __0)
			{
				GetLastSaveLocation(__0);
				GetLastCoord(__instance);
			}


			static bool iscoordsavefinish = false;
			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileCoordinate), nameof(ChaFileCoordinate.SaveFile))]
			static void OnPostCoordSave() => iscoordsavefinish = true;

			static void GetLastSaveLocation(string path) =>
				LastCoordSaveLocation = path ?? LastCoordSaveLocation;
			static void GetLastCoord(ChaFileCoordinate inst) =>
				LastCoord = inst ?? LastCoord;


			[HarmonyPostfix]
			[HarmonyPatch(typeof(Button), nameof(Button.OnPointerClick))]
			static void OnPreButtonClick(Button __instance)
			{
				if(!__instance.interactable) return;

				OnCreateNSaveToFashionLine(__instance);
			}

			static void OnCreateNSaveToFashionLine(Button btn)
			{
#if HONEY_API
				if(btn == null) return;
				if(btn != FashionLine_GUI.coordToFashionBtn) return;
				var orig = FashionLine_GUI.clothesSave.clothesLoadWin.button[1];
				btn.onClick.RemoveAllListeners();

				bool flag = false;
				void btnFunc()
				{
					Logger.LogInfo("clicked button");
					orig.onClick.Invoke();

					UnityAction save = () =>
					{
						iscoordsavefinish = false;
						IEnumerator func()
						{
							FileStream stream = null;
							CoordData coordData = null;
							//	for(int a = 0; a < 10; ++a)
							yield return new WaitUntil(() => iscoordsavefinish);

							try
							{
								stream = new FileStream(LastCoordSaveLocation, FileMode.Open, FileAccess.Read);
								coordData = new CoordData()
								{
									data = stream.ReadAllBytes(),
									name = LastCoord.coordinateName
								};

								stream.Close();
								stream.Dispose();
							}
							catch(Exception ex)
							{
								stream?.Close();
								stream?.Dispose();

								Logger.LogError(ex);
							}

							if(coordData != null)
								yield return Instance.StartCoroutine(MakerAPI.GetCharacterControl()
									.GetComponent<FashionLineController>()
									.AddFashionCo(20, LastCoord.coordinateName, coordData));
						
							Logger.LogInfo("ran new listener");
							flag = true;

							yield break;
						}

						Instance.StartCoroutine(func());
					};

					UnityAction cancel = () =>
					{

						flag = true;
						Logger.LogInfo("ran new listener back");
					};

					Instance.StartCoroutine(Killme());
					IEnumerator Killme()
					{
						yield return new WaitUntil(() => flag);

						FashionLine_GUI.clothesSave.clothesNameInput.
						btnEntry.onClick.RemoveListener(save);
						FashionLine_GUI.clothesSave.clothesNameInput.
						btnBack.onClick.RemoveListener(cancel);

						Logger.LogInfo("removed added listener");

						yield break;
					}

					FashionLine_GUI.clothesSave.clothesNameInput.
					btnEntry.onClick.AddListener(save);
					FashionLine_GUI.clothesSave.clothesNameInput.
					btnBack.onClick.AddListener(cancel);

				}
				btnFunc();
#endif
			}
		}
	}
}
