using System;
using System.Collections.Generic;
using Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk.Model;
using System.IO;
using System.Linq;

class ThreeTanksActualStrategy : ActualStrategy
{
	TwoTankskActualStrategy myOtherSelf = new TwoTankskActualStrategy();
	override public void Move(Tank self, World world, Move move)
	{
		myOtherSelf.historyX[world.Tick] = self.X;
		myOtherSelf.historyY[world.Tick] = self.Y;

		if (IsDead(teammates[0]) || IsDead(teammates[1]))
		{
			myOtherSelf.CommonMove(self, world, move);
			return;
		}

		bool forward;
		Bonus bonus = GetBonus(out forward);

#if TEDDY_BEARS
		if(AliveEnemyCnt() == 0)
			bonus = null;
#endif
		bool shootOnlyToVictim = false;
		cornerX = cornerY = -1;
		if (bonus != null && (world.Tick > runToCornerTime || bonus.Type == BonusType.AmmoCrate))
		{
			MoveToBonus(bonus, forward);
		}
		else
		{
			MoveBackwards();
		}

		Tank victim = GetVictim();//GetWithSmallestDistSum();
		if (victim != null)
			TurnToMovingTank(victim, false);

		TryShoot(victim, shootOnlyToVictim);

		if (AliveEnemyCnt() == 1)
		{
			Tank enemy = PickEnemy();
			double myDist = self.GetDistanceTo(enemy);
			double tmDist = teammates[0].GetDistanceTo(enemy);
			if(self.GetDistanceTo(enemy) > 4*self.Width && !(myDist < tmDist-self.Width/2))
				MoveTo(enemy, true);
		}

		bool bonusSaves = BonusSaves(self, bonus);

		if (world.Tick > runToCornerTime && victim != null && !HaveTimeToTurn(victim) && !bonusSaves)
			TurnToMovingTank(victim, true);
	}
	void MoveBackwards()
	{
		double x = (self.X + teammates[0].X + teammates[1].X) / 3;
		bool up = self.Y < Math.Min(teammates[0].Y, teammates[1].Y);
		bool down = self.Y > Math.Max(teammates[0].Y, teammates[1].Y);
		double a = self.Width;
		if (x < world.Width / 2)
		{
			if (up)
				MoveTo(a, world.Height / 4, 0, 1);
			else if (down)
				MoveTo(a, world.Height / 4 * 3, 0, 1);
			else
				MoveTo(a, world.Height / 4 * 2, 0, 1);
		}
		else
		{
			if (up)
				MoveTo(world.Width-a, world.Height / 4, 0, 1);
			else if (down)
				MoveTo(world.Width - a, world.Height / 4 * 3, 0, 1);
			else
				MoveTo(world.Width - a, world.Height / 4 * 2, 0, 1);
		}
	}
	
	override protected bool VeryBad(Tank self, Bonus bonus)
	{
		if (bonus.Type == BonusType.Medikit && self.CrewHealth > self.CrewMaxHealth - medikitVal ||
		   bonus.Type == BonusType.RepairKit && self.HullDurability > self.HullMaxDurability - repairVal ||
		   bonus.Type == BonusType.AmmoCrate && self.PremiumShellCount >= 4)
			return true; ;
		double x = (self.X + teammates[0].X + teammates[1].X) / 3;
		if (x > world.Width / 2)
		{
			return bonus.X < world.Width / 2;
		}
		else
		{
			return bonus.X > world.Width / 2;
		}
	}
}