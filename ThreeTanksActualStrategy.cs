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
		runToCornerTime = 0;
		myOtherSelf.historyX[world.Tick] = self.X;
		myOtherSelf.historyY[world.Tick] = self.Y;

		/*if(teammates.Count != 2)
		{
			myOtherSelf.CommonMove(self, world, move);
			return;
		}*/

		bool forward;
		Bonus bonus = GetBonus(out forward);

#if TEDDY_BEARS
		if(AliveEnemyCnt() == 0)
			bonus = null;
#endif
		bool shootOnlyToVictim = false;
		cornerX = cornerY = -1;
		if (bonus != null)
			MoveToBonus(bonus, forward);
		else
			MoveBackwards();

		Tank victim = GetVictim();//GetWithSmallestDistSum();
		if (victim != null)
			TurnToMovingTank(victim, false);

		TryShoot(victim, shootOnlyToVictim);

		/*if (AliveEnemyCnt() == 1)
		{
			Tank enemy = PickEnemy();
			double myDist = self.GetDistanceTo(enemy);
			double tmDist = teammates[0].GetDistanceTo(enemy);
			if(self.GetDistanceTo(enemy) > 4*self.Width && !(myDist < tmDist-self.Width/2))
				MoveTo(enemy, true);
		}*/

		bool bonusSaves = BonusSaves(self, bonus);

		if ((world.Tick < 100 || world.Tick > 300) && victim != null && !HaveTimeToTurn(victim) && !bonusSaves)
			TurnToMovingTank(victim, true);
	}
	void MoveBackwards()
	{
		double x = (self.X + teammates[0].X + teammates[1].X) / 3;
		bool up = self.Y < Math.Min(teammates[0].Y, teammates[1].Y);
		bool down = self.Y > Math.Max(teammates[0].Y, teammates[1].Y);
		double a = self.Width;
		double b = self.Width;
		if (x < world.Width / 2)
		{
			if (up)
				MoveToVert(b, world.Height / 5);
			else if (!down)
				MoveToVert(a, world.Height / 2);
			else
				MoveToVert(b, world.Height / 5 * 4);
		}
		else
		{
			if (up)
				MoveToVert(world.Width-b, world.Height / 5);
			else if (!down)
				MoveToVert(world.Width - a, world.Height / 2);
			else
				MoveToVert(world.Width - b, world.Height / 5 * 4);	
		}
	}

	bool WeAreUnderAttack()
	{
		if (enemies.Count != 0 && (enemies[0].PlayerName == "keika" || enemies[0].PlayerName == "Megabyte"
			|| enemies[0].PlayerName == "Mr.Smile"))
			return true;
		double x = (self.X + teammates[0].X + teammates[1].X) / 3;
		double cx = world.Width / 2;

		int cnt = enemies.Count(tank => x < cx && tank.X < cx || x > cx && tank.X > cx);

		return cnt >= 2 || AliveTeammateCnt() == 1;
	}

	protected override bool VeryBad(Tank self, Bonus bonus)
	{
		if (!WeAreUnderAttack())
		{
			if (bonus.Type == BonusType.Medikit && self.CrewHealth > self.CrewMaxHealth - medikitVal ||
				bonus.Type == BonusType.RepairKit && self.HullDurability > self.HullMaxDurability - repairVal ||
				bonus.Type == BonusType.AmmoCrate)
				return true;
			double angle = Math.Atan2(bonus.Y - self.Y, bonus.X - self.X);
			if (AngleDiff(angle, 0) < Math.PI/4 || AngleDiff(angle, Math.PI) < Math.PI/4)
				return true;
			double x = (self.X + teammates[0].X + teammates[1].X)/3;
			double cx = world.Width/2;
			double cy = world.Height/2;
			if (x > cx)
			{
				//return bonus.X < world.Width / 2;
				return bonus.X + bonus.Y < cx + cy || bonus.X - bonus.Y < cx - cy;
			}
			else
			{
				//return bonus.X > world.Width / 2;
				return bonus.X + bonus.Y > cx + cy || bonus.X - bonus.Y > cx - cy;
			}
		}
		else
		{
			if (bonus.Type == BonusType.Medikit && self.CrewHealth > self.CrewMaxHealth - 20 ||
				bonus.Type == BonusType.RepairKit && self.HullDurability > self.HullMaxDurability - repairVal ||
				bonus.Type == BonusType.AmmoCrate && self.PremiumShellCount >= 4)
				return true;
			
			double x = (self.X + teammates[0].X + teammates[1].X) / 3;
			double cx = world.Width / 2;
			double cy = world.Height / 2;
			if (x > cx)
			{
				return bonus.X < cx;
			}
			else
			{
				return bonus.X > cx;
			}
		}
	}
}