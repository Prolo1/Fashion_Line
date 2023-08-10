using ExtensibleSaveFormat;
using KKAPI.Chara;
using KKAPI.Maker;
using MessagePack.Resolvers;
using MessagePack.Unity;
using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using static BepInEx.Logging.LogLevel;


namespace FashionLine
{
	public abstract class SaveLoadController
	{
		public SaveLoadController()
		{

			CompositeResolver.Register(
				BuiltinResolver.Instance,
				StandardResolver.Instance,
				UnityResolver.Instance,
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

		public static T ByteArrayToObject<T>(byte[] arr)
		{
			BinaryFormatter bf = new BinaryFormatter();
			using(var ms = new MemoryStream())
			{
				ms.Write(arr, 0, arr.Length);
				T obj = (T)bf.Deserialize(ms);
				return obj;
			}
		}


		public abstract int Version { get; }
		public abstract string[] DataKeys { get; }

		public abstract PluginData Save(CharaCustomFunctionController ctrler);
		public abstract PluginData Load(CharaCustomFunctionController ctrler, PluginData data);
		protected abstract PluginData UpdateVersionFromPrev(CharaCustomFunctionController ctrler, PluginData data);

	}

	/// <summary>
	/// saves controls from current data. make a new one if variables change
	/// </summary>
	internal class CurrentSaveLoadController : SaveLoadController
	{
		public new int Version => 1;

		public new string[] DataKeys => new[] { "MorphData_values", "MorphData_targetCard", "MorphData_targetPng", "MorphData_ogSize" };


		/*
		 Data that can (potentially) affect the save:
		* enum MorphCalcType
		* class MorphControls
		* class MorphData
		* class MorphData.AMBXSections
		* class MorphConfig 
		* var CharaMorpher_Core.cfg.defaults
		* var CharaMorpher_Core.cfg.controlCategories
		* string CharaMorpher_Core.strDivider
		* string CharaMorpher_Core.defaultStr
		 all I can think of for now
		 */

		/// <summary>
		/// creates an updated version 
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		protected override PluginData UpdateVersionFromPrev(CharaCustomFunctionController ctrler, PluginData data)
		{
			var ctrl = (CharaMorpherController)ctrler;


			//if(data == null || data.version != Version)
			//{
			//
			//	data = base.Load(ctrler, data)?.Copy();
			//	
			//	//CharaMorpher_Core.Logger.LogDebug($"Old version: {data?.version.ToString() ?? "Don't exist..."}");
			//	if(data != null && data.version == base.Version)
			//	{
			//		//last version
			//		var values = LZ4MessagePackSerializer.Deserialize<Dictionary<string, Tuple<float, MorphCalcType>>>((byte[])data.data[DataKeys[0]], CompositeResolver.Instance);
			//
			//		var tmpVals = values.ToDictionary((k) => k.Key.Trim(), (v) => new MorphSliderData(v.Key.Trim()/*just in case*/, data: v.Value.Item1, calc: v.Value.Item2));
			//		var newValues = new MorphControls() { all = { { defaultStr, tmpVals } } };
			//		data.data[DataKeys[0]] = LZ4MessagePackSerializer.Serialize(newValues, CompositeResolver.Instance);
			//
			//		data.version = Version;
			//	}
			//	else
			//		data = null;
			//}

			if(data == null)
				data = ctrler?.GetExtendedData(ctrl.isReloading);

			return data;
		}

		public override PluginData Load(CharaCustomFunctionController ctrler, PluginData data)
		{
			var ctrl = (CharaMorpherController)ctrler;

			data = UpdateVersionFromPrev(ctrler, data);// use if version goes up (i.e. 1->2)

			if(data == null) return null;

			try
			{

				if(data.version != Version) throw new Exception($"Target card data was incorrect version: expected [V{Version}] instead of [V{data.version}]");

				
			}
			catch(Exception e)
			{
				FashionLine_Core.Logger.Log(Error | Message, $"Could not load PluginData:\n{e}\n");
				return null;
			}

			return data;
		}

		public override PluginData Save(CharaCustomFunctionController ctrler)
		{
			if(!CharaMorpher_Core.cfg.saveAsMorphData.Value) return null;
			
			PluginData data = new PluginData() { version = Version, };
			try
			{
				var ctrl = (CharaMorpherController)ctrler;
				
			}
			catch(Exception e)
			{
				FashionLine_Core.Logger.Log(Error | Message, $"Could not save PluginData: \n {e} ");
				return null;
			}
			ctrler.SetExtendedData(data);

			return data;
		}

	}

}
