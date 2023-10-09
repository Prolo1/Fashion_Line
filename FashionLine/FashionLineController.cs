using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using ExtensibleSaveFormat;

#if HONEY_API
using AIChara;
#else
using ChaCustom;
#endif
using Manager;

using static BepInEx.Logging.LogLevel;
using static FashionLine.FashionLine_Util;
using KKAPI.Utilities;
using BrowserFolders;
using KK_Plugins.MaterialEditor;
using ADV.Commands.Object;
using System.Runtime.InteropServices.ComTypes;
using KoiClothesOverlayX;
#if HONEY_API
using MyBrowserFolders = BrowserFolders.AI_BrowserFolders;
using System.Reflection;

#elif KKS
using MyBrowserFolders = BrowserFolders.KKS_BrowserFolders;
#endif
using UnityEngine;

namespace FashionLine
{
	public class FashionLineController : CharaCustomFunctionController
	{
		internal Dictionary<string, CoordData> fashionData = new Dictionary<string, CoordData>();
		private ChaFileCoordinate defultCoord = null;
		private PluginData pluginData = null;
		CoordData current = null;


		Coroutine co = null;
		private void PostAllLoad(object e, CharaReloadEventArgs v)
		{

		}


		public void OnCharaReload(GameMode currentGameMode)
		{
			//reset data
			{
				var line = fashionData.ToList();
				foreach(var fashion in line)
					RemoveFashion(fashion.Key);

				defultCoord = new ChaFileCoordinate();
				fashionData.Clear();
				pluginData = null;
			}


			//	FashionLine_Core.Logger.LogInfo("Got to post all function");
			//	//if(v.ReloadedCharacter != this) return;
			//	FashionLine_Core.Logger.LogInfo("Got to post all function x2");

			//IEnumerator savedefatltCO(int delay)
			//{
			//
			//	for(int a = -1; a < delay; ++a)
			//		yield return null;
			//}



			//save init outfit
			defultCoord.LoadBytes(
				ChaControl.nowCoordinate.SaveBytes(),
				ChaControl.nowCoordinate.loadVersion);

			//save overlay data
			var ctrlKCO = GetComponent<KoiClothesOverlayController>();
			if(FashionLine_Core.KoiOverlayDependency.Exists && ctrlKCO)
			{
				ctrlKCO.GetType().GetMethod("OnCoordinateBeingSaved",
					BindingFlags.Instance | BindingFlags.NonPublic)
					.Invoke(ctrlKCO, new object[] { defultCoord });

				//OnCoordinateBeingSaved();
			}

			//load new data
			pluginData = this.LoadExtData();

			//yield break;

			//if(co != null)
			//	StopCoroutine(co);
			//co = StartCoroutine(savedefatltCO(11));
			//profit
		}

		public void AddFashion(string name, CoordData data)
		{
			if(data == null) return;

			try
			{
				if(!new ChaFileCoordinate().
					LoadFile(new MemoryStream(data.data)
#if HONEY_API
					, (int)Singleton<GameSystem>.Instance.language
#endif
					))
					throw new Exception($"Was not able to read data from card [{name}] (Not a coordinate card)");

				if(fashionData.ContainsKey(name))
					throw new Exception("This coordinate already exists (or one with the same name)");

				fashionData.Add(name, data);

				if(!MakerAPI.InsideMaker) return;
				FashionLine_GUI.AddCoordinate(in data);
			}
			catch(Exception e)
			{
				FashionLine_Core.Logger.Log(Message | Error,
					$"Could not add [{name}] to FashionLine:\n{e.Message}");
				FashionLine_Core.Logger.Log(Error, $"\n{e.TargetSite} {e.StackTrace}\n");
			}
		}

		public void RemoveFashion(string name)
		{
			try
			{
				if(!fashionData.ContainsKey(name))
					throw new Exception($"The name [{name}] does not exist in list");

				var tmp = fashionData[name];
				fashionData.Remove(name);

				if(!MakerAPI.InsideMaker) return;
				FashionLine_GUI.RemoveCoordinate(in tmp);
			}
			catch(Exception e)
			{
				FashionLine_Core.Logger.Log(Message | Error,
					$"Could not remove [{name}] from FashionLine:\n{e.Message}");
				FashionLine_Core.Logger.Log(Error, $"{e.TargetSite}\n{e.StackTrace}\n");
			}
		}

		public void RemoveFashion(in CoordData data)
		{
			try
			{
				if(!fashionData.ContainsValue(data))
					throw new Exception($"The CoordData [{data.name}] does not exist in list");

				var tmp = data;
				var name = fashionData.FirstOrNull((v) => v.Value == tmp).Key;
				fashionData.Remove(name);

				if(!MakerAPI.InsideMaker) return;
				FashionLine_GUI.RemoveCoordinate(in data);
			}
			catch(Exception e)
			{
				FashionLine_Core.Logger.Log(Message | Error,
					$"Could not remove [{data.name}] from FashionLine:\n{e.Message}");
				FashionLine_Core.Logger.Log(Error, $"{e.TargetSite}\n{e.StackTrace}\n");
			}
		}

		public void NextInLine()
		{
			var line = fashionData.ToList();

			if(!fashionData.ContainsValue(current)) return;

			var index = line.FindIndex((l) => l.Value == current) + 1;
			index %= line.Count;

			if(line.InRange(index))
				WearFashion(line[index].Value);
		}

		public void PrevInLine()
		{
			var line = fashionData.ToList();

			if(!fashionData.ContainsValue(current)) return;
			var index = line.FindIndex((l) => l.Value == current) - 1;

			index = index < -1 ? 0 : index;
			index = index < 0 ? line.Count - 1 : index;

			if(line.InRange(index))
				WearFashion(line[index].Value);
		}

		public void WearFashion(in CoordData costume, bool isFile = true, bool reload = true)
		{
			if(costume == null) return;

			current = costume;

			if(FashionLine_Core.KoiOverlayDependency.Exists)
			{
				try
				{

					//	//There was no way around this
					//	var ctrl = GetComponent<KoiClothesOverlayController>();
					//	ctrl?.GetType().GetMethod("RemoveAllOverlays",
					//		bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance,
					//		types: new Type[] { /*typeof(GameMode), typeof(bool)*/ },
					//		binder: null, modifiers: null)
					//		.Invoke(ctrl, new object[] { /*KoikatuAPI.GetCurrentGameMode(), false*/ });
				}
				catch(Exception e)
				{
					FashionLine_Core.Logger.Log(Error, $"Something went wrong: {e}\n");
				}
			}

			if(MakerAPI.InsideMaker)
			{

				var mkBase = MakerAPI.GetMakerBase();
				mkBase.updateCustomUI = true;

#if HONEY_API
				mkBase.ChangeAcsSlotName(-1);
				mkBase.forceUpdateAcsList = true;
#endif
			}

			if(isFile)
			{

				MemoryStream stream = new MemoryStream(costume.data);
				if(!ChaControl.nowCoordinate.LoadFile(stream
#if HONEY_API
				, (int)Singleton<GameSystem>.Instance.language
#endif
				))
				{
					FashionLine_Core.Logger.LogMessage($"Could not read card [{costume.name}]. Data size [{costume.data.Length}]");
					//return;
				}
			}
			else
			{
				//if(!ChaControl.nowCoordinate.LoadBytes(data.data,
				//	ChaControl.nowCoordinate.loadVersion))
				//{
				//	FashionLine_Core.Logger.LogMessage($"Could not read raw data [{data.name}]. Data size [{data.data.Length}]");
				//	//return;
				//}

				ChaControl.nowCoordinate = (ChaFileCoordinate)costume.extras.Find((p) => p is ChaFileCoordinate);
			}

			var ctrlMEC = GetComponent<MaterialEditorCharaController>();
			if(FashionLine_Core.MatEditerDependency.Exists && ctrlMEC)
				ctrlMEC.GetType().GetMethod("LoadCoordinateExtSaveData",
					BindingFlags.Instance | BindingFlags.NonPublic)
					.Invoke(ctrlMEC, new object[] { ChaControl.nowCoordinate });

			var ctrlKCO = GetComponent<KoiClothesOverlayController>();
			if(FashionLine_Core.KoiOverlayDependency.Exists && ctrlKCO)
			{
				ctrlKCO.GetType().GetMethod("OnCoordinateBeingLoaded",
					BindingFlags.Instance | BindingFlags.NonPublic)
					.Invoke(ctrlKCO, new object[]
					{ ChaControl.nowCoordinate, false });

				//OnCoordinateBeingLoaded();
			}

			if(reload)
			{
				if(FashionLine_Core.MatEditerDependency.Exists && ctrlMEC)
					StartCoroutine(ctrlMEC.LoadData(true, true, true));

				ChaControl.Reload(false, true, true, true
#if HONEY_API
					, true
#endif
				);



				ChaControl.AssignCoordinate(
#if KOI_API
			(ChaFileDefine.CoordinateType)ChaControl.chaFile.status.coordinateType
				//ChaFileDefine.CoordinateType.Plain
#endif
				);
			}
		}

		public void WearDefaultFashion(bool reload = true)
		{
			var costume = new CoordData() { name = "(default)" };
			costume.extras.Add(defultCoord);
			WearFashion(costume, isFile: false, reload: false);

			var ctrlMEC = GetComponent<MaterialEditorCharaController>();

			if(FashionLine_Core.MatEditerDependency.Exists && ctrlMEC)
				ctrlMEC.GetType().GetMethod("LoadCharacterExtSaveData",
					BindingFlags.Instance | BindingFlags.NonPublic)
					.Invoke(ctrlMEC, null);



			if(reload)
			{
				if(FashionLine_Core.MatEditerDependency.Exists && ctrlMEC)
					StartCoroutine(ctrlMEC.LoadData(true, true, true));

				ChaControl.Reload(false, true, true, true
#if HONEY_API
					, true
#endif
				);

				ChaControl.AssignCoordinate(
#if KOI_API
			(ChaFileDefine.CoordinateType)ChaControl.chaFile.status.coordinateType
				//ChaFileDefine.CoordinateType.Plain
#endif
				);
			}
		}

		#region Helper Coroutines
		public IEnumerator AddFashionCo(uint delay, string name, CoordData data)
		{
			for(int a = 0; a < ((int)delay); ++a)
				yield return null;

			AddFashion(name, data);

			yield break;
		}

		public IEnumerator WearFashionCo(uint delay, CoordData costume, bool isFile = true, bool reload = true)
		{
			for(int a = 0; a < ((int)delay); ++a)
				yield return null;

			WearFashion(costume, isFile, reload);

			yield break;
		}

		#endregion

		#region Class Overrides
		protected override void Awake()
		{
			base.Awake();
			KKAPI.Chara.CharacterApi.CharacterReloaded += PostAllLoad;
		}

		protected override void OnDestroy()
		{
			KKAPI.Chara.CharacterApi.CharacterReloaded -= PostAllLoad;
			base.OnDestroy();
		}

		protected override void OnReload(GameMode currentGameMode, bool keepState)
		{
			if(keepState) return;
			OnCharaReload(currentGameMode);
		}

		protected override void OnCardBeingSaved(GameMode currentGameMode)
		{
			this.SaveExtData();
		}

		protected override void OnCoordinateBeingSaved(ChaFileCoordinate coordinate)
		{
			base.OnCoordinateBeingSaved(coordinate);
		}

		#endregion
	}

	public class CoordData
	{
		public byte[] data;
		public string name;

		public readonly List<object> extras = new List<object>();



		public CoordData Clone()
		{
			var tmp = new CoordData() { data = data.ToArray(), name = name + "" };
			tmp.extras.AddRange(extras);
			return tmp;
		}

		public bool Copy(CoordData dat)
		{
			if(dat == null) return false;

			var tmp = dat.Clone();
			extras.Clear();

			data = tmp.data;
			name = tmp.name;
			extras.AddRange(tmp.extras);

			return true;
		}

	}
}