using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

using UnityEngine;
using UnityEngine.UI;

using ProloAPI;
using ProloAPI.Extentions;

using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Utilities;
using MessagePack.Resolvers;
using MessagePack.Unity;
using MessagePack;
using ExtensibleSaveFormat;

#if HONEY_API
using CharaCustom;
using Manager;
#endif

using static BepInEx.Logging.LogLevel;


//using AIProject;

/*
 Data that can (potentially) affect the save:
* CoordData class
* 
 all I can think of for now
 */

namespace FashionLine
{

	/// <summary>
	/// saves controls from current data. make a new one if variables change
	/// </summary>
	public class CurrentSaveLoadManager : SaveLoadManagerV1
	{

		public new int Version => base.Version + 1;
		public new string[] DataKeys => new[] { "FashionData_Data" };

		public new enum LoadDataType : int
		{ Data, }

		/// <summary>
		/// creates an updated version 
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		protected new PluginData UpdateVersionFromPrev(FashionLine_Controller ctrl, PluginData data)
		{

			if(data == null || data?.version != Version)
			{
				data = base.UpdateVersionFromPrev(ctrl, data)?.Copy();
				if(data != null && data.version == base.Version)
				{
					var oldData = LZ4MessagePackSerializer.Deserialize<Dictionary<string, OldCoordData>>((byte[])data.data[DataKeys[(int)SaveLoadManagerV1.LoadDataType.Data]], CompositeResolver.Instance);


					data.data[DataKeys[(int)LoadDataType.Data]] =
						LZ4MessagePackSerializer.Serialize(oldData.ToDictionary(k => k.Key,
						v =>
						{
							var tmp = new CoordData() { data = v.Value.data, name = v.Value.name };
							tmp.extras.AddRange(v.Value.extras);

							return tmp;
						}), CompositeResolver.Instance);

					data.version = Version;
					//CharaMorpher_Core.Logger.LogDebug($"Old version: {data?.version.ToString() ?? "Don't exist..."}");
				}
				else
					data = null;
			}

			if(data == null)
				data = ctrl?.GetExtendedData(true);

			return data;
		}

		public override PluginData Load(FashionLine_Controller ctrl, PluginData data)
		{

			data = UpdateVersionFromPrev(ctrl, data);// use if version goes up (i.e. 1->2)

			if(data == null) return null;

			try
			{
				if(data.version != Version) throw new Exception($"Target data was incorrect version: expected [V{Version}] instead of [V{data.version}]");

				var carddata = LZ4MessagePackSerializer.Deserialize<Dictionary<string, CoordData>>((byte[])data.data[DataKeys[((int)LoadDataType.Data)]], CompositeResolver.Instance);

				if(carddata == null) throw new Exception("Data does not exist");

				//FashionLine_Core.Logger.LogInfo($"cardata count: {carddata.Count}");
				foreach(var line in carddata)
					ctrl.AddFashion(line.Key, line.Value, overwrite: true);
			}
			catch(Exception e)
			{
				FashionLine_Core.Logger.Log(Error | Message, $"Could not load PluginData:\n{e.Message}");
				FashionLine_Core.Logger.Log(Error, $"\n{e.TargetSite}\n{e.StackTrace}\n");
				return null;
			}

			return data;
		}

		public override PluginData Save(FashionLine_Controller ctrl, PluginData data = null)
		{
			if(data == null)
				data = new PluginData();
			data.version = Version;

			try
			{

				if(ctrl.fashionData == null)
					throw new Exception("No FashionLine Data to be Saved 😮");
				if(ctrl.fashionData.Count <= 0) return null;

				var dataLine = ctrl.fashionData.ToDictionary((k) => k.Key, (v) => v.Value.Clone());
				foreach(var fashion in dataLine)
					for(int a = 0; a < fashion.Value.extras.Count; ++a)
						if(fashion.Value.extras[a] is Toggle)
							fashion.Value.extras.Remove(fashion.Value.extras[a--]);

				data.data[DataKeys[((int)LoadDataType.Data)]] = LZ4MessagePackSerializer.Serialize(dataLine, CompositeResolver.Instance);
			}
			catch(Exception e)
			{
				FashionLine_Core.Logger.Log(Error | Message, $"Could not save PluginData:\n{e.Message}");
				FashionLine_Core.Logger.Log(Error, $"\n{e.TargetSite}\n{e.StackTrace}\n");
				return null;
			}
			ctrl.SetExtendedData(data);

			return data;
		}

	}

	public class SaveLoadManagerV1 : SaveLoadManager<FashionLine_Controller, PluginData>
	{
		public new int Version => 1;
		public new string[] DataKeys => new[]
		{ "FashionData_Data" };

		public new enum LoadDataType : int
		{ Data, }

		/// <summary>
		/// creates an updated version 
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		protected override PluginData UpdateVersionFromPrev(FashionLine_Controller ctrler, PluginData data)
		{
			if(data == null)
				data = ctrler?.GetExtendedData(true);

			return data;
		}



		#region Old Classes
		public class OldCoordData
		{
			public byte[] data;
			public string name;

			public string translatedName
			{
				get
				{
					TranslationHelper.TryTranslate(name, out var trans);
					return trans ?? name;
				}
			}
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
		#endregion
	}

}
