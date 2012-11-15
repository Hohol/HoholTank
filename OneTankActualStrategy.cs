using System;
using System.Collections.Generic;
using Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk.Model;
using System.IO;

class OneTankActualStrategy : ActualStrategy
{
	const int firstShootTick = 4;
	override public void Move(Tank self, World world, Move move)
	{
		this.self = self;
		ActualStrategy.world = world;
		this.move = move;

		historyX[world.Tick] = self.X;
		historyY[world.Tick] = self.Y;

		/*if (AliveEnemyCnt() == 0)
		{
			Experiment();
			return;
		}/**/

		bool forward;
		Bonus bonus = GetBonus(self, out forward);
		//bonus = null;
		Tank victim = null;

		bool shootOnlyToVictim = false;
		cornerX = cornerY = -1;
		if (bonus != null && (world.Tick > runToCornerTime || bonus.Type == BonusType.AmmoCrate))
		{
			MoveTo(bonus, forward);
			victim = GetAlmostDead();
			if (victim == null)
				victim = GetVictim();
			else
				shootOnlyToVictim = true;
		}
		else
		{
			MoveBackwards(out cornerX, out cornerY);
			victim = GetNearest(cornerX, cornerY);
			shootOnlyToVictim = true;
		}
		if (world.Tick <= firstShootTick)
			victim = GetVictim();
		if (victim != null)
			TurnToMovingTank(victim, false);

		int dummy, resTick;
		double premResX = double.NaN, premResY = double.NaN;
		double regResX, regResY;
		Unit aimPrem = self.PremiumShellCount > 0 ? EmulateShot(true, out dummy, out premResX,out premResY) : null;
		Unit aimReg = EmulateShot(false, out resTick, out regResX, out regResY);

		if (BadAim(aimReg, victim, shootOnlyToVictim, regResX, regResY))
			aimReg = null;
		if (BadAim(aimPrem, victim, shootOnlyToVictim, premResX, premResY))
			aimPrem = null;

		if (world.Tick < firstShootTick)
		{
			aimPrem = null;
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

		//if(AliveEnemyCnt() == 0)
		MoveTo(self.Height / 2 + 5, self.Width * 1.5, 0, 1);

		bool bonusSaves = BonusSaves(self, bonus);

		if (world.Tick > runToCornerTime && victim != null && !HaveTimeToTurn(victim) && !bonusSaves)
			TurnToMovingTank(victim, true);

		/*if (world.Tick > runToCornerTime && AliveEnemyCnt() <= 3)
		{
			var tank = GetMostAngryEnemy();
			if (tank != null)
				StayPerpendicular(tank);
		}*/


		ManageStuck();

		AvoidBullets();
	}
	bool BadAim(Unit aim, Tank victim, bool shootOnlyToVictim, double x, double y)
	{
		if(BadAim(aim,victim,shootOnlyToVictim))
			return true;
		if(IsMovingBackward(victim))
		{
			if(x < 0)
				return true;
		}
		else
		{
			if(x > 0)
				return true;
		}	
		return false;
	}
}