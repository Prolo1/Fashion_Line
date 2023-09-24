using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

namespace FashionLine
{
	public class FashionLineController : CharaCustomFunctionController
	{
		internal Dictionary<string, CoordData> fashionData = new Dictionary<string, CoordData>();
		private ChaFileCoordinate defultCoord = null;
		private PluginData pluginData = null;
		CoordData current = null;

		public void OnCharaReload(GameMode currentGameMode)
		{
			//reset data
			{
				var line = fashionData.ToList();
				foreach(var fashion in line)
					RemoveFashion(fashion.Key);

				fashionData.Clear();
				defultCoord = new ChaFileCoordinate();
			}

			//save init outfit
			defultCoord.LoadBytes(ChaControl.nowCoordinate.SaveBytes(),
				ChaControl.nowCoordinate.loadVersion);


			//load new data
			pluginData = this.LoadExtData();

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
				FashionLineGUI.AddCoordinate(in data);
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
				FashionLineGUI.RemoveCoordinate(in tmp);
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
				FashionLineGUI.RemoveCoordinate(in data);
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
				WearCostume(line[index].Value);
		}

		public void LastInLine()
		{
			var line = fashionData.ToList();

			if(!fashionData.ContainsValue(current)) return;
			var index = line.FindIndex((l) => l.Value == current) - 1;

			index = index < -1 ? 0 : index;
			index = index < 0 ? line.Count - 1 : index;

			if(line.InRange(index))
				WearCostume(line[index].Value);
		}

		public void WearCostume(in CoordData data)
		{
			if(data == null) return;

			current = data;

			if(FashionLine_Core.KoiOverlayModExists)
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

			MemoryStream stream = new MemoryStream(data.data);
			if(!ChaControl.nowCoordinate.LoadFile(stream
#if HONEY_API
				, (int)Singleton<GameSystem>.Instance.language
#endif
			))
			{
				FashionLine_Core.Logger.LogMessage($"Could not read card [{data.name}]");
				//return;
			}

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

		public void WearDefaultCostume()
		{
			if(defultCoord == null) return;

			if(FashionLine_Core.KoiOverlayModExists)
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


#if HONEY_API
			ChaControl.nowCoordinate.LoadBytes
				(defultCoord.SaveBytes(),
				defultCoord.loadVersion);
#else
			ChaControl.nowCoordinate = defultCoord;
#endif

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

		#region Class Overrides

		protected override void OnReload(GameMode currentGameMode, bool maintainState)
		{
			OnCharaReload(currentGameMode);
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
