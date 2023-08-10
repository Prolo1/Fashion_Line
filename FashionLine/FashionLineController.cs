using KKAPI;
using KKAPI.Chara;
using System;
using System.Collections.Generic;
using System.Text;

namespace FashionLine
{
	public class FashionLineController : CharaCustomFunctionController
	{


		protected override void OnReload(GameMode currentGameMode, bool maintainState)
		{


			base.OnReload(currentGameMode, maintainState);
		}



		protected override void OnCardBeingSaved(GameMode currentGameMode) {/*throw new NotImplementedException();*/}
	}
}
