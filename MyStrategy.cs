//#define TEDDY_BEARS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk
{
	public sealed class MyStrategy : IStrategy
	{
		public void Move(Tank self, World world, Move move)
		{
			move.LeftTrackPower = -1;
			move.RightTrackPower = 1;
			move.TurretTurn = Math.PI;
			move.FireType = FireType.PremiumPreferred;
		}

		public TankType SelectTank(int tankIndex, int teamSize)
		{
			return TankType.Medium;
		}
#if DEBUG
		public static void Main(string[] a) { }
#endif
	}
}