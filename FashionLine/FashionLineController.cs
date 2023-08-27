using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using ExtensibleSaveFormat;

using AIChara;
using Manager;

using static BepInEx.Logging.LogLevel;


namespace FashionLine
{
	public class FashionLineController : CharaCustomFunctionController
	{
		internal Dictionary<string, CoordData> fashionData = new Dictionary<string, CoordData>();
		private ChaFileCoordinate defultCoord = null;
		private PluginData pluginData = null;

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

			defultCoord.LoadBytes
				(ChaControl.nowCoordinate.SaveBytes(),
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
					LoadFile(new MemoryStream(data.data),
					(int)Singleton<GameSystem>.Instance.language))
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
				FashionLine_Core.Logger.Log(Error, $"{e.TargetSite} {e.StackTrace}\n");
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

		public void WearCostume(CoordData data)
		{
			if(data == null) return;

			MemoryStream stream = new MemoryStream(data.data);
			if(!ChaControl.nowCoordinate.LoadFile(stream, (int)Singleton<GameSystem>.Instance.language))
			{
				FashionLine_Core.Logger.LogMessage($"Could not read card [{data.name}]");
				return;
			}
			ChaControl.Reload(false, true, true, true, true);

			if(MakerAPI.InsideMaker)
			{
				var mkBase = MakerAPI.GetMakerBase();
				mkBase.ChangeAcsSlotName(-1);
				mkBase.updateCustomUI = true;
				mkBase.forceUpdateAcsList = true;
			}

			ChaControl.AssignCoordinate();
		}
		public void WearDefaultCostume()
		{
			if(defultCoord == null) return;

			ChaControl.nowCoordinate.LoadBytes
				(defultCoord.SaveBytes(),
				defultCoord.loadVersion);
			ChaControl.Reload(false, true, true, true, true);

			if(MakerAPI.InsideMaker)
			{
				var mkBase = MakerAPI.GetMakerBase();
				mkBase.ChangeAcsSlotName(-1);
				mkBase.updateCustomUI = true;
				mkBase.forceUpdateAcsList = true;
			}

			ChaControl.AssignCoordinate();
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
