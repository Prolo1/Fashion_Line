using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Utilities;
using ExtensibleSaveFormat;

using UnityEngine;

#if HONEY_API
using AIChara;
#else
using ChaCustom;
#endif
using Manager;

using BrowserFolders;
using KK_Plugins.MaterialEditor;
using KoiClothesOverlayX;

using static BepInEx.Logging.LogLevel;
using static FashionLine.FashionLine_Util;
using static FashionLine.FashionLine_Core;
#if HONEY_API
using MyBrowserFolders = BrowserFolders.AI_BrowserFolders;

#elif KKS
using MyBrowserFolders = BrowserFolders.KKS_BrowserFolders;
#endif

namespace FashionLine
{
	public class FashionLineController : CharaCustomFunctionController
	{
		internal Dictionary<string, CoordData> fashionData = new Dictionary<string, CoordData>();
		private ChaFileCoordinate defaultCoord = null;
		private PluginData pluginData = null;
		private CoordData current = null;


		Coroutine co = null;
		public void OnCharaReload(GameMode currentGameMode, bool keepState = false)
		{

			if(FashionLine_Core.cfg.debug.Value)
				FashionLine_Core.Logger
					.LogDebug("OnCharaReload called");


			//reset data
			{
				if(!FashionLine_Core.cfg.areCoordinatesPersistant.Value)
				{
					var line = fashionData.ToList();
					foreach(var fashion in line)
						RemoveFashion(fashion.Key);
					fashionData.Clear();
				}

				defaultCoord = new ChaFileCoordinate();
				pluginData = null;
			}


			//save init outfit
			defaultCoord.LoadBytes(
				ChaControl.nowCoordinate.SaveBytes(),
				ChaControl.nowCoordinate.loadVersion);
			defaultCoord.pngData = ChaControl.nowCoordinate?.pngData?.ToArray();//copy

			IEnumerator func(int delay)
			{
				for(int i = 0; i < delay; ++i)
					yield return null;

				//save mat. editor data
				var ctrlMEC = GetComponent<MaterialEditorCharaController>();
				if(FashionLine_Core.MatEditerDependency.InTargetVersionRange && ctrlMEC)
					try
					{
						ctrlMEC.GetType().GetMethod("OnCoordinateBeingSaved",
							BindingFlags.Instance | BindingFlags.NonPublic,
							types: new Type[] { typeof(ChaFileCoordinate) },
							binder: null, modifiers: null)
							.Invoke(ctrlMEC, new object[] { defaultCoord });
					}
					catch(Exception e)
					{
						FashionLine_Core.Logger.Log(Error, $"Something went wrong: {e}\n");
					}

				//save overlay data
				var ctrlKCO = GetComponent<KoiClothesOverlayController>();
				if(FashionLine_Core.KoiOverlayDependency.InTargetVersionRange && ctrlKCO)
					try
					{

						ctrlKCO.GetType().GetMethod("OnCoordinateBeingSaved",
							BindingFlags.Instance | BindingFlags.NonPublic,
							types: new Type[] { typeof(ChaFileCoordinate) },
							binder: null, modifiers: null)
							.Invoke(ctrlKCO, new object[] { defaultCoord });
					}
					catch(Exception e)
					{
						FashionLine_Core.Logger.Log(Error, $"Something went wrong: {e}\n");
					}

				yield break;
			}

			//load new data
			pluginData = this.LoadExtData();

			if(co != null)
				StopCoroutine(co);
			co = StartCoroutine(func(11));

			//profit
		}

		public void AddFashion(string name, CoordData data, bool overwrite = false)
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

				if(overwrite)
				{
					if(fashionData.ContainsKey(name))
						FashionLine_GUI.RemoveCoordinate(fashionData[name]);
				}
				else if(fashionData.ContainsKey(name))
					throw new Exception("This coordinate already exists (or one with the same name)");

				if(!overwrite)
					FashionLine_GUI.RemoveCoordinate(fashionData[name]);

				FashionLine_GUI.AddCoordinate(in data);

				fashionData[name] = data;
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
			var coord = ChaControl.nowCoordinate;

			if(FashionLine_Core.cfg.debug.Value)
				FashionLine_Core.Logger
					.LogDebug("Wear fashion called");

			try
			{
				if(isFile)
				{

					MemoryStream stream = new MemoryStream(costume.data);
					if(!ChaControl.nowCoordinate.LoadFile(stream
#if HONEY_API
					, (int)Singleton<GameSystem>.Instance.language
#endif
					))
					{
						FashionLine_Core.Logger.Log(Warning | Message, $"Could not read card [{costume.name}]. Data size [{costume.data.Length}]");
						//return;
					}
				}
				else
				{
					coord = (ChaFileCoordinate)costume.extras.Find((p) => p is ChaFileCoordinate);
					if(!ChaControl.nowCoordinate.LoadBytes(coord?.SaveBytes(), coord?.loadVersion))
						FashionLine_Core.Logger.LogMessage($"Could not read Coordinate [{coord?.coordinateName ?? "Null"}]");
				}
			}
			catch(Exception e)
			{
				FashionLine_Core.Logger.Log(Error, $"Something went wrong: {e}\n");
			}


			var ctrlKCO = GetComponent<KoiClothesOverlayController>();
			if(FashionLine_Core.KoiOverlayDependency.InTargetVersionRange && ctrlKCO)
				try
				{
					ctrlKCO.GetType().GetMethod("OnCoordinateBeingLoaded",
						BindingFlags.Instance | BindingFlags.NonPublic,
						types: new Type[] { typeof(ChaFileCoordinate), typeof(bool) },
						binder: null, modifiers: null)
						.Invoke(ctrlKCO, new object[]
						{ coord, false });
				}
				catch(Exception e)
				{
					FashionLine_Core.Logger.Log(Error, $"Something went wrong: {e}\n");
				}

			var ctrlMEC = GetComponent<MaterialEditorCharaController>();
			if(FashionLine_Core.MatEditerDependency.InTargetVersionRange && ctrlMEC)
				try
				{
					ctrlMEC.GetType().GetMethod("OnCoordinateBeingLoaded",
						BindingFlags.Instance | BindingFlags.NonPublic,
						types: new Type[] { typeof(ChaFileCoordinate), typeof(bool) },
						binder: null, modifiers: null)
						.Invoke(ctrlMEC, new object[]
						{ coord, false });
				}
				catch(Exception e)
				{
					FashionLine_Core.Logger.Log(Error, $"Something went wrong: {e}\n");
				}

			FashionReload(reload);
		}

		public void WearDefaultFashion(bool reload = true)
		{
			if(FashionLine_Core.cfg.debug.Value)
				FashionLine_Core.Logger
					.LogDebug("weard defult called");

			var costume = new CoordData() { name = "(default)" };
			costume.extras.Add(defaultCoord);

			WearFashion(costume, isFile: false, reload: true);
		}

		private void FashionReload(bool reload = true)
		{
			try
			{

				if(reload)
				{
					if(FashionLine_Core.cfg.debug.Value)
						FashionLine_Core.Logger
							.LogDebug("Fashion reload called");

#if HONEY_API
					Singleton<Character>.Instance.customLoadGCClear = false;
#endif
					ChaControl.Reload(false, true, true, true
#if HONEY_API
						, true
#endif
					);
#if HONEY_API
					Singleton<Character>.Instance.customLoadGCClear = true;
#endif

					if(MakerAPI.InsideMaker)
					{

						var mkBase = MakerAPI.GetMakerBase();

#if HONEY_API
						mkBase.ChangeAcsSlotName(-1);
						mkBase.forceUpdateAcsList = true;
#endif
						mkBase.updateCustomUI = true;
					}


					ChaControl.AssignCoordinate(
#if KOI_API
			(ChaFileDefine.CoordinateType)ChaControl.chaFile.status.coordinateType
					//ChaFileDefine.CoordinateType.Plain
#endif
					);
				}
			}
			catch { }
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
		//protected override void Awake()
		//{
		//	base.Awake();
		//	//	KKAPI.Chara.CharacterApi.CharacterReloaded += PostAllLoad;
		//}

		//protected override void OnDestroy()
		//{
		//	//KKAPI.Chara.CharacterApi.CharacterReloaded -= PostAllLoad;
		//	base.OnDestroy();
		//}

		protected override void OnReload(GameMode currentGameMode, bool keepState)
		{

			if(keepState) return;
			OnCharaReload(currentGameMode, keepState);
		}

		protected override void OnCardBeingSaved(GameMode currentGameMode)
		{
			this.SaveExtData();
		}

		#endregion
	}

	public class CoordData
	{
		public byte[] data;
		public string name;
		public DateTime updated;
		public DateTime created;
		public string translatedName
		{
			get
			{
				TranslationHelper.TryTranslate(name, out var trans);
				return trans ?? name;
			}
		}

		public readonly List<object> extras = new List<object>();

		public CoordData()
		{
			updated = created = DateTime.Now;
		}

		public CoordData Clone()
		{
			var tmp = new CoordData()
			{
				data = data.ToArray(),
				name = name + "",
				created = new DateTime(created.Ticks),
				updated = DateTime.Now
			};
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