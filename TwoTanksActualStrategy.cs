using System;
using System.Collections.Generic;
using Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk.Model;
using System.IO;

class TwoTankskActualStrategy : ActualStrategy
{
	Tank teammate;
	Tank moreImportant;
	OneTankActualStrategy myOtherSelf = new OneTankActualStrategy();

	override public void Move(Tank self, World world, Move move)
	{
		this.self = self;
		ActualStrategy.world = world;
		this.move = move;

		historyX[world.Tick] = self.X;
		historyY[world.Tick] = self.Y;
		myOtherSelf.historyX[world.Tick] = self.X;
		myOtherSelf.historyY[world.Tick] = self.Y;

		foreach (var tank in world.Tanks)
			if (tank.Id != self.Id && tank.IsTeammate)
				teammate = tank;

		if (IsDead(teammate))
		{
			myOtherSelf.Move(self, world, move);
			return;
		}

		if (self.TeammateIndex == 0 || IsDead(teammate))
			moreImportant = self;
		else
			moreImportant = teammate;

		/*int myHP = Math.Min(self.CrewHealth, self.HullDurability);
		int teammateHP = Math.Min(teammate.CrewHealth, teammate.HullDurability);
		if (IsDead(teammate) || myHP < teammateHP || myHP == teammateHP && self.TeammateIndex == 0)
			moreImportant = self;
		else
			moreImportant = teammate;*/

		bool forward;
		Bonus bonus;
		if (self == moreImportant)
		{
			bonus = GetBonus(self, out forward);
		}
		else
		{
			Bonus forbidden = GetBonus(teammate, out forward);
			bonus = GetBonus(self, out forward, forbidden);
		}

		Tank victim = GetWithSmallestDistSum();

		bool shootOnlyToVictim = false;
		cornerX = cornerY = -1;
		if (bonus != null && (world.Tick > runToCornerTime || bonus.Type == BonusType.AmmoCrate))
		{
			MoveTo(bonus, forward);
		}
		else
		{
			MoveBackwards(out cornerX, out cornerY);
		}
		if (victim != null)
			TurnToMovingTank(victim, false);

		int dummy, resTick;
		double premResX = double.NaN, premResY = double.NaN;
		double regResX, regResY;
		Unit aimPrem = self.PremiumShellCount > 0 ? EmulateShot(true, out dummy, out premResX, out premResY) : null;
		Unit aimReg = EmulateShot(false, out resTick, out regResX, out regResY);

		if (BadAim(aimReg, victim, shootOnlyToVictim, regResX, regResY))
			aimReg = null;
		if (BadAim(aimPrem, victim, shootOnlyToVictim, premResX, premResY))
			aimPrem = null;

		if (aimPrem != null)
		{
			if (!(aimPrem is Tank) || IsDead((Tank)aimPrem) || shootOnlyToVictim && victim != null && aimPrem.Id != victim.Id)
				aimPrem = null;
			if (aimPrem is Tank && ((Tank)aimPrem).IsTeammate)
				aimPrem = null;
		}
		if (aimReg != null)
		{
			if (!(aimReg is Tank) || IsDead((Tank)aimReg) || shootOnlyToVictim && victim != null && aimReg.Id != victim.Id)
				aimReg = null;
			if (aimReg is Tank && ((Tank)aimReg).IsTeammate)
				aimReg = null;
		}

		if (aimPrem != null && ((Tank)aimPrem).HullDurability > 20)
			move.FireType = FireType.Premium;
		else if (aimReg != null)
		{
			double angle = GetCollisionAngle((Tank)aimReg, resTick);
			if (angle < ricochetAngle - Math.PI / 10)
				move.FireType = FireType.Regular;
		}

		RotateForSafety();

		//if (AliveEnemyCnt() == 1)
		if (world.Tick > runToCornerTime && AliveEnemyCnt() <= 3)
		{
			//var tank = PickEnemy();
			var tank = GetMostAngryEnemy();
			if (tank != null)
				StayPerpendicular(tank);
		}

		if (AliveEnemyCnt() == 1 && AliveTeammateCnt() > 1)
		{
			Tank enemy = PickEnemy();
			if(self.GetDistanceTo(enemy) > 3*self.Width)
				MoveTo(enemy, true);
		}

		bool bonusSaves = BonusSaves(self, bonus);

		if (world.Tick > runToCornerTime && victim != null && !HaveTimeToTurn(victim) && !bonusSaves)
			TurnToMovingTank(victim, true);

		//SimulateStuck();

		ManageStuck();

		AvoidBullets();
	}

	bool BadAim(Unit aim, Tank victim, bool shootOnlyToVictim, double x, double y)
	{
		if (BadAim(aim, victim, shootOnlyToVictim))
			return true;
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
	}

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

			/*
			double test = TimeToTurn(self, tank);
			if (!IsDead(teammate))
				test += TimeToTurn(teammate, tank);
			if (test < 0)
				test = inf / 2;
			if (ObstacleBetween(self,tank, true))
				test = inf / 2;
			double flyTime = (self.GetDistanceTo(tank) - self.VirtualGunLength) / regularBulletStartSpeed;
			test += flyTime;
			if (!IsDead(teammate))
			{
				flyTime = (teammate.GetDistanceTo(tank) - teammate.VirtualGunLength) / regularBulletStartSpeed;
				test += flyTime;
			}
			*/
			if (test < mi)
			{
				mi = test;
				res = tank;
			}
		}
		return res;
	}

	static bool Inside(Unit unit, double x, double y, double precision, bool enemy = false)
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
	}
}