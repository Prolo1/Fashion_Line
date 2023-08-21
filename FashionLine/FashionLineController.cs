using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using MessagePack.Resolvers;
using MessagePack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using ExtensibleSaveFormat;

//using static FashionLine.FashionLine_Util;

namespace FashionLine
{
	public class FashionLineController : CharaCustomFunctionController
	{
		internal Dictionary<string, CoordData> fashionData = new Dictionary<string, CoordData>();
		PluginData pluginData = null;

		public void OnCharaReload(GameMode currentGameMode)
		{
			//reset data
			{
				fashionData.Clear();
			}

			//load new data
			pluginData = this.LoadExtData();

			//profit
		}

		public void AddFashion(string name, CoordData data)
		{
			fashionData.Add(name, data);


		}

		#region Class Overrides
		protected override void OnReload(GameMode currentGameMode, bool maintainState)
		{

			OnCharaReload(currentGameMode);
		}

		protected override void OnCardBeingSaved(GameMode currentGameMode) { }
		#endregion
	}

	public class CoordData
	{
		byte[] data;
		string name;
	}
}
