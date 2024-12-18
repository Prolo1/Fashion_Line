using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;

using ProloAPI;
using ProloAPI.Extensions;

using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Utilities;
using ExtensibleSaveFormat;
using KK_Plugins.MaterialEditor;
using KoiClothesOverlayX;
using Manager;
//using BrowserFolders;

#if HONEY_API
using AIChara;
#else
using ChaCustom;
#endif

using static BepInEx.Logging.LogLevel;
using KoiSkinOverlayX;
using ADV.Commands.Chara;
//#if HONEY_API
//using MyBrowserFolders = BrowserFolders.AI_BrowserFolders;
//
//#elif KKS
//using MyBrowserFolders = BrowserFolders.KKS_BrowserFolders;
//#endif

namespace FashionLine
{
	using static FashionLine_Core;

	public class FashionLine_Controller : CharaCustomFunctionController
	{

		internal Dictionary<string, CoordData> fashionData = new Dictionary<string, CoordData>();
		public List<ChaFileCoordinate> defaultCoords { get; } = new List<ChaFileCoordinate>();
		private PluginData pluginData = null;
		private CoordData current = null;


		Coroutine co = null;
		public void OnCharaReload(GameMode currentGameMode, bool keepState = false)
		{

			if(cfg.debug.Value)
				Logger.LogDebug("OnCharaReload called");

			//reset data
			{
				if(!cfg.areCoordinatesPersistant.Value)
				{
					var line = fashionData.ToList();//copy list first
					foreach(var fashion in line)
						RemoveFashion(fashion.Key);
					fashionData.Clear();
				}
				pluginData = null;
				defaultCoords.Clear();
			}

			var coords =
#if KOI_API
				ChaControl.chaFile.coordinate;
#elif HONEY_API
				new ChaFileCoordinate[] { ChaControl.chaFile.coordinate };
#endif

			//save init outfits

			foreach(var coord in coords)
			{
				var defaultCoord = defaultCoords.AddNReturn(new ChaFileCoordinate());
				defaultCoord.LoadBytes(coord.SaveBytes(), coord.loadVersion);
				defaultCoord.pngData = coord?.pngData?.ToArray();//copy
				saveCoordDataTo(coord, defaultCoord);//testing out removal
													 //InvokeCoordWriteEvent(defaultCoord);
			}


			//IEnumerator func(int delay)
			//{
			//	for(int i = 0; i < delay; ++i)
			//		yield return null;
			//
			//	//save mat. editor data			
			//	var ctrlMEC = GetComponent<MaterialEditorCharaController>();
			//	if(MatEditerDependency.IsInTargetVersionRange && ctrlMEC)
			//		try
			//		{
			//			ctrlMEC.GetType().GetMethod("OnCoordinateBeingSaved",
			//				BindingFlags.Instance | BindingFlags.NonPublic,
			//				types: new Type[] { typeof(ChaFileCoordinate) },
			//				binder: null, modifiers: null)
			//				.Invoke(ctrlMEC, new object[] { defaultCoord });
			//		}
			//		catch(Exception e)
			//		{
			//			Logger.Log(Error, $"Something went wrong: {e}\n");
			//		}
			//
			//	//save overlay data
			//	var ctrlKCO = GetComponent<KoiClothesOverlayController>();
			//	if(KoiOverlayDependency.IsInTargetVersionRange && ctrlKCO)
			//		try
			//		{
			//
			//			ctrlKCO.GetType().GetMethod("OnCoordinateBeingSaved",
			//				BindingFlags.Instance | BindingFlags.NonPublic,
			//				types: new Type[] { typeof(ChaFileCoordinate) },
			//				binder: null, modifiers: null)
			//				.Invoke(ctrlKCO, new object[] { defaultCoord });
			//		}
			//		catch(Exception e)
			//		{
			//			Logger.Log(Error, $"Something went wrong: {e}\n");
			//		}
			//
			//
			//	yield break;
			//}

			//load new data
			pluginData = this.LoadExtData<CurrentSaveLoadManager, FashionLine_Controller>();

			//	if(co != null)
			//		StopCoroutine(co);
			//	co = StartCoroutine(func(11));

			//profit
		}

		void saveCoordDataTo(ChaFileCoordinate from, ChaFileCoordinate to)
		{
			var fromData = ExtendedSave.GetAllExtendedData(from);
			if(fromData == null) return;

			var toData = ExtendedSave.GetAllExtendedData(to);
			if(toData == null) ExtendedSave.SetExtendedDataById(to, "some random string that no one will guess", new PluginData());
			toData = toData ?? ExtendedSave.GetAllExtendedData(to);


			foreach(var data in fromData)
				toData[data.Key] = data.Value;


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


				if(!overwrite && fashionData.ContainsKey(name))
					throw new Exception("This coordinate already exists (or one with the same name)");

				if(fashionData.ContainsKey(name))
					FashionLine_GUI.RemoveCoordinate(fashionData[name]);

				FashionLine_GUI.AddCoordinate(in data);

				fashionData[name] = data;
			}
			catch(Exception e)
			{
				Logger.Log(Message | Error,
					$"Could not add [{name}] to FashionLine:\n{e.Message}");
				Logger.Log(Error, $"\n{e.TargetSite} {e.StackTrace}\n");
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
				Logger.Log(Message | Error,
					$"Could not remove [{name}] from FashionLine:\n{e.Message}");
				Logger.Log(Error, $"{e.TargetSite}\n{e.StackTrace}\n");
			}
		}

		public void RemoveFashion(in CoordData data)
		{
			try
			{
				if(!fashionData.ContainsValue(data))
					throw new Exception($"The CoordData [{data.name}] does not exist in list");

				var tmp = data;
				var name = fashionData.First((v) => v.Value == tmp).Key;
				fashionData.Remove(name);

				if(!MakerAPI.InsideMaker) return;
				FashionLine_GUI.RemoveCoordinate(in data);
			}

			catch(Exception e)
			{
				Logger.Log(Message | Error,
					$"Could not remove [{data?.name ?? ""}] from FashionLine:\n{e.Message}");
				Logger.Log(Error, $"{e.TargetSite}\n{e.StackTrace}\n");
			}
		}

		public void NextInLine()
		{
			var line = fashionData.ToList();

			if(!fashionData.ContainsValue(current)) return;

			var index = line.FindIndex((l) => l.Value == current) + 1;
			index %= line.Count;

			if(line.InRange(index))
				WearFashion(line[index].Value, cfg.addToCurrentAccessories.Value);
		}

		public void PrevInLine()
		{
			var line = fashionData.ToList();

			if(!fashionData.ContainsValue(current)) return;
			var index = line.FindIndex((l) => l.Value == current) - 1;

			index = index < -1 ? 0 : index;
			index = index < 0 ? line.Count - 1 : index;

			if(line.InRange(index))
				WearFashion(line[index].Value, cfg.addToCurrentAccessories.Value);
		}

		public void WearFashion(in CoordData costume, bool addAccessories, bool isFile = true, bool reload = true)
		{
			if(costume == null) return;

			current = costume;

			if(cfg.debug.Value)
				Logger.LogDebug("Wear fashion called");

			var coordinate = new ChaFileCoordinate();
			var ctrlKCO = GetComponent<KoiClothesOverlayController>();
			var ctrlMEC = GetComponent<MaterialEditorCharaController>();
			try
			{
				if(isFile)
				{
					using(MemoryStream stream = new MemoryStream(costume.data))
					{
						if(!coordinate.LoadFile(stream
#if HONEY_API
						, (int)Singleton<GameSystem>.Instance.language
#endif
						))
							Logger.Log(Warning | Message, $"Could not read card [{costume.name}]. Data size [{costume.data.Length}]");

						//remove excess
						var tmp1 = ChaControl.nowCoordinate.accessory.parts.ToList();
						tmp1.RemoveAll(acc => acc.type <= (int)ChaListDefine.CategoryNo.ao_none/*none*/);
						var tmp2 = coordinate.accessory.parts.ToList();
						tmp2.RemoveAll(acc => acc.type <= (int)ChaListDefine.CategoryNo.ao_none/*none*/);

						//conbine accessories with existing
						if(addAccessories)
							coordinate.accessory.parts = tmp1.Concat(tmp2).ToArray();

						ChaControl.nowCoordinate.MemberInit();//reset coordinate
						if(!ChaControl.nowCoordinate.LoadBytes(coordinate?.SaveBytes(), coordinate?.loadVersion))
							throw new ArgumentException("Could not load specified coordinate");

					}
				}
				else
				{
					var tmp = (ChaFileCoordinate)costume.extras.Find((p) => p is ChaFileCoordinate);

					if(tmp == null)
						throw new NullReferenceException("Coordinate does not exist");

					if(!coordinate.LoadBytes(tmp?.SaveBytes(), tmp?.loadVersion))
						Logger.LogMessage($"Could not read Coordinate [{tmp?.coordinateName ?? "Null"}]");

					saveCoordDataTo(tmp, coordinate);

					//remove excess
					var tmp1 = ChaControl.nowCoordinate.accessory.parts.ToList();
					tmp1.RemoveAll(acc => acc.type <= (int)ChaListDefine.CategoryNo.ao_none/*none*/);
					var tmp2 = coordinate.accessory.parts.ToList();
					tmp2.RemoveAll(acc => acc.type <= (int)ChaListDefine.CategoryNo.ao_none/*none*/);

					//combine access
					if(addAccessories)
						coordinate.accessory.parts = tmp1.Concat(tmp2).ToArray();

					ChaControl.nowCoordinate.MemberInit();//reset coordinate

					if(!ChaControl.nowCoordinate.LoadBytes(coordinate?.SaveBytes(), coordinate?.loadVersion))
						throw new ArgumentException("Could not load specified coordinate");
				}

			}
			catch(Exception e)
			{
				Logger.Log(Error, $"Something went wrong: {e}\n");
				reload = false;
			}

			FashionReload(reload);
			ForceInvokeCoordBeingLoaded(coordinate);
		}

		public void WearDefaultFashion(bool reload = true)
		{
			if(cfg.debug.Value)
				Logger.LogDebug("wear default called");

			var costume = new CoordData() { name = "(default)" };
			var coord =
#if KOI_API
			 (int)ChaControl.chaFile.status.coordinateType;
#elif HONEY_API
				0;
#endif



			costume.extras.AddNReturn(defaultCoords[coord]);

			WearFashion(costume, cfg.addToCurrentAccessories.Value, isFile: false, reload: reload);
		}

		private void FashionReload(bool reload = true)
		{
			if(!reload) return;

			try
			{

				if(cfg.debug.Value)
					Logger.LogDebug("Fashion reload called");

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


				ChaControl.AssignCoordinate(
#if KOI_API
		(ChaFileDefine.CoordinateType)ChaControl.chaFile.status.coordinateType
				//ChaFileDefine.CoordinateType.Plain
#endif
				);


				if(MakerAPI.InsideMaker)
				{

					var mkBase = MakerAPI.GetMakerBase();

#if HONEY_API
					mkBase.ChangeAcsSlotName(-1);
					mkBase.forceUpdateAcsList = true;
#endif
					mkBase.updateCustomUI = true;
				}
			}
			catch { }
		}


		private void ForceInvokeCoordBeingLoaded(ChaFileCoordinate coord, ChaControl control = null)
		{
			control = control ?? ChaControl;

			var ctrlers = CharacterApi.GetBehaviours(control);
			//save coordinate states
			var states = ctrlers.ToDictionary((x) => x, (y) => y.ControllerRegistration.MaintainCoordinateState);

			foreach(var ctrl in ctrlers)
				ctrl.ControllerRegistration.MaintainCoordinateState = false;

			typeof(CharacterApi).GetMethod("OnCoordinateBeingLoaded",
						BindingFlags.Static | BindingFlags.NonPublic,
						types: new Type[] { typeof(ChaControl), typeof(ChaFileCoordinate) },
						binder: null, modifiers: null)
						.Invoke(null, new object[]
						{control, coord});

			//restore coordinate states
			foreach(var ctrl in ctrlers)
				ctrl.ControllerRegistration.MaintainCoordinateState = states[ctrl];

		}

		#region Helper Coroutines
		public IEnumerator AddFashionCo(uint delay, string name, CoordData data)
		{
			for(int a = 0; a < ((int)delay); ++a)
				yield return null;

			AddFashion(name, data);

			yield break;
		}

		public IEnumerator WearFashionCo(uint delay, CoordData costume, bool addAccessories, bool isFile = true, bool reload = true)
		{
			for(int a = 0; a < ((int)delay); ++a)
				yield return null;

			WearFashion(costume, addAccessories, isFile: isFile, reload: reload);

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
			this.SaveExtData<CurrentSaveLoadManager, FashionLine_Controller>();
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