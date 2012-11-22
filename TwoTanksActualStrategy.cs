using System;
using System.Collections.Generic;
using Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk.Model;
using System.IO;
using System.Linq;

class TwoTankskActualStrategy : ActualStrategy
{
	Tank teammate;
	OneTankActualStrategy myOtherSelf = new OneTankActualStrategy();

	override public void Move(Tank self, World world, Move move)
	{
		myOtherSelf.historyX[world.Tick] = self.X;
		myOtherSelf.historyY[world.Tick] = self.Y;

		teammate = teammates[0];

		if (IsDead(teammate))
		{
			myOtherSelf.CommonMove(self, world, move);
			return;
		}

		bool forward;
		Bonus bonus = GetBonus(out forward);

#if TEDDY_BEARS
		//bonus = null;
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

		RotateForSafety();

		/*if (world.Tick > runToCornerTime && AliveEnemyCnt() <= 1)
		{
			var tank = GetMostAngryEnemy();
			if (tank != null)
				StayPerpendicular(tank);
		}*/

		if (AliveEnemyCnt() == 1)
		{
			Tank enemy = PickEnemy();
			double myDist = self.GetDistanceTo(enemy);
			double tmDist = teammate.GetDistanceTo(enemy);
			if (self.GetDistanceTo(enemy) > 4 * self.Width && !(myDist < tmDist - self.Width / 2))
				MoveTo(enemy, true);
		}

		bool bonusSaves = BonusSaves(self, bonus);

		if (world.Tick > runToCornerTime && victim != null && !HaveTimeToTurn(victim) && !bonusSaves)
			TurnToMovingTank(victim, true);
	}	

	void MoveToDead()
	{
		foreach (Tank tank in world.Tanks)
		{
			if (IsDead(tank))
			{
				MoveTo(tank,true);
				return;
			}
		}
	}

	bool LeftMost()
	{
		var a = enemies.OrderBy(tank => tank.X).ToArray();
		return a.Length != 0 && Math.Max(self.X, teammate.X) < a[0].X - 60;
	}

	bool RightMost()
	{
		var a = enemies.OrderBy(tank => world.Width-tank.X).ToArray();
		return a.Length != 0 && Math.Min(self.X, teammate.X) > a[0].X + 60;
	}

	void MoveBackwards()
	{
		double firstX = self.Height*1.5;// nearest to vertical wall
		double firstY = self.Width*2.5; 
		double secondX = self.Height*3+15;
		double secondY = self.Width*1.5;
		double vertD = self.Width / 2 + self.Height / 2;
		if (LeftMost())
		{
			if (self.Y < teammate.Y)
				MoveTo(firstX, vertD, 0, 1);
			else
				MoveTo(firstX, world.Height -vertD, 0, -1);
		}
		else if (RightMost())
		{
			if (self.Y < teammate.Y)
				MoveTo(world.Width - firstX, vertD, 0, 1);
			else
				MoveTo(world.Width - firstX, world.Height - vertD, 0, -1);
		}
		else
		{
			double x = (self.X + teammate.X) / 2;
			double y = (self.Y + teammate.Y) / 2;
			if (x < world.Width / 2 && y < world.Height / 2)
			{
				if (self.X < teammate.X)
					MoveTo(firstX, firstY, 0, 1);
				else
					MoveTo(secondX, secondY, 0, 1);
			}
			else if (x < world.Width / 2 && y > world.Height / 2)
			{
				if (self.X < teammate.X)
					MoveTo(firstX, world.Height-firstY, 0, -1);
				else
					MoveTo(secondX, world.Height-secondY, 0, -1);
			}
			else if (x > world.Width / 2 && y < world.Height / 2)
			{
				if (self.X > teammate.X)
					MoveTo(world.Width - firstX, firstY, 0, 1);
				else
					MoveTo(world.Width - secondX, secondY, 0, 1);
			}
			else
			{
				if (self.X > teammate.X)
					MoveTo(world.Width - firstX, world.Height-firstY, 0, -1);
				else
					MoveTo(world.Width - secondX, world.Height-secondY, 0, -1);
			}
		}
	}

	/*protected override bool BadAim(Unit aim, Unit victim, bool shootOnlyToVictim, double x, double y, ShellType bulletType)
	{
		if (BadAim(aim, victim, shootOnlyToVictim, bulletType))
			return true;
		if (self.GetDistanceTo(aim) < self.Width * 3)
			return false;
		if (self.TeammateIndex == 0)
		{
			if (x < 0)
				return true;
		}
		else
		{
			if (x > 0)
				return true;
		}
		return false;
	}*/

	/*override protected void MoveBackwards(out double resX, out double resY)
	{
		resX = 0;
		resY = 0;
	}*/

	Tank GetWithSmallestDistSum()
	{
		double mi = inf;
		Tank res = null;
		foreach (var tank in world.Tanks)
		{
			if (tank.IsTeammate || IsDead(tank))
				continue;

			double test = self.GetDistanceTo(tank) + teammate.GetDistanceTo(tank);
			if (ObstacleBetween(self, tank, true))
				test = inf / 2;

			if (res == null || test < mi*0.8 || test < mi*1.2 && MoreSweet(tank,res))
			{
				mi = test;
				res = tank;
			}
		}
		return res;
	}

	

	/*static bool Inside(Unit unit, double x, double y, double precision, bool enemy = false)
	{
		double d = unit.GetDistanceTo(x, y);
		double angle = unit.GetAngleTo(x, y);
		x = d * Math.Cos(angle);
		y = d * Math.Sin(angle);
		double w = unit.Width / 2 + precision;
		double h = unit.Height / 2 + precision;
		double lx = -w, rx = w;
		double ly = -h, ry = h;
		if (enemy && Math.Sqrt(Util.Sqr(unit.SpeedX) + Util.Sqr(unit.SpeedY)) > 1)
		{
			if (IsMovingBackward(unit))
			{
				lx = 0;
			}
			else
			{
				rx = 0;
			}
		}
		return x >= lx && x <= rx &&
			y >= ly && y <= ry;
		//return x >= -w && x <= w &&
		//	   y >= -h && y <= h;
	}*/
}