using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

using UnityEngine;
using UnityEngine.UI;

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
	public abstract class SaveLoadController<B, T>
	{
		public abstract int Version { get; }
		public abstract string[] DataKeys { get; }
		public enum LoadDataType : int { }

		public SaveLoadController()
		{
			CompositeResolver.Register(
				UnityResolver.Instance,
				StandardResolver.Instance,
				BuiltinResolver.Instance,
				//default resolver
				ContractlessStandardResolver.Instance
				);
		}

		// Convert an object to a byte array
		public static byte[] ObjectToByteArray(object obj)
		{
			BinaryFormatter bf = new BinaryFormatter();
			using(var ms = new MemoryStream())
			{
				bf.Serialize(ms, obj);
				return ms.ToArray();
			}
		}

		public static T1 ByteArrayToObject<T1>(byte[] arr)
		{
			BinaryFormatter bf = new BinaryFormatter();
			using(var ms = new MemoryStream())
			{
				ms.Write(arr, 0, arr.Length);
				T1 obj = (T1)bf.Deserialize(ms);
				return obj;
			}
		}

		public abstract T Save(B ctrler, T data);
		public abstract T Load(B ctrler, T data);
		protected abstract T UpdateVersionFromPrev(B ctrler, T data);
	}

	/// <summary>
	/// saves controls from current data. make a new one if variables change
	/// </summary>
	public class CurrentSaveLoadController : SaveLoadControllerV1
	{
		public new int Version => base.Version + 1;
		public new string[] DataKeys => new[]
		{ "FashionData_Data" };

		public new enum LoadDataType : int
		{
			Data,
		}

		/// <summary>
		/// creates an updated version 
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		protected new PluginData UpdateVersionFromPrev(FashionLineController ctrler, PluginData data)
		{
			if(data == null || data?.version != Version)
			{

				data = base.Load(ctrler, data)?.Copy();
				var oldData = LZ4MessagePackSerializer.Deserialize<Dictionary<string, OldCoordData>>((byte[])data.data[DataKeys[(int)SaveLoadControllerV1.LoadDataType.Data]],CompositeResolver.Instance);

				data.data[DataKeys[(int)LoadDataType.Data]] =
					LZ4MessagePackSerializer.Serialize(oldData.ToDictionary(k => k.Key,
					v =>
					{
						var tmp = new CoordData() { data = v.Value.data, name = v.Value.name };
						tmp.extras.AddRange(v.Value.extras);
						return tmp;
					}), CompositeResolver.Instance);

				data.version= Version;
				//CharaMorpher_Core.Logger.LogDebug($"Old version: {data?.version.ToString() ?? "Don't exist..."}");
			}

			if(data == null)
				data = ctrler?.GetExtendedData(true);

			return data;
		}

		public override PluginData Load(FashionLineController ctrl, PluginData data)
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

		public override PluginData Save(FashionLineController ctrler, PluginData data = null)
		{
			if(data == null)
				data = new PluginData();
			data.version = Version;

			try
			{
				var ctrl = (FashionLineController)ctrler;

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
			ctrler.SetExtendedData(data);

			return data;
		}


	}

	public class SaveLoadControllerV1 : SaveLoadController<FashionLineController, PluginData>
	{
		public override int Version => 1;
		public override string[] DataKeys => new[]
		{ "FashionData_Data" };

		public new enum LoadDataType : int
		{
			Data,
		}

		/// <summary>
		/// creates an updated version 
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		protected override PluginData UpdateVersionFromPrev(FashionLineController ctrler, PluginData data)
		{
			var ctrl = (FashionLineController)ctrler;


			//if(data == null || data.version != Version)
			//{
			//
			//	data = base.Load(ctrler, data)?.Copy();
			//	
			//	//CharaMorpher_Core.Logger.LogDebug($"Old version: {data?.version.ToString() ?? "Don't exist..."}");
			//}

			if(data == null)
				data = ctrler?.GetExtendedData(true);

			return data;
		}

		public override PluginData Load(FashionLineController ctrler, PluginData data)
		{
			var ctrl = (FashionLineController)ctrler;

			data = UpdateVersionFromPrev(ctrler, data);// use if version goes up (i.e. 1->2)

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

		public override PluginData Save(FashionLineController ctrler, PluginData data = null)
		{
			if(data == null)
				data = new PluginData() { version = Version };

			try
			{
				var ctrl = (FashionLineController)ctrler;

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
			ctrler.SetExtendedData(data);

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
