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
		this.world = world;
		this.move = move;

		historyX[world.Tick] = self.X;
		historyY[world.Tick] = self.Y;

		/*if (AliveEnemyCnt() == 0)
		{
			Experiment();
			return;
		}/**/

		bool forward;
		Bonus bonus = GetBonus(out forward);
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
		Unit aimPrem = self.PremiumShellCount > 0 ? EmulateShot(true, out dummy) : null;
		Unit aimReg = EmulateShot(false, out resTick);

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

		bool bonusSaves = BonusSaves(bonus);

		if (world.Tick > runToCornerTime && victim != null && !HaveTimeToTurn(victim) && !bonusSaves)
			TurnToMovingTank(victim, true);


		//if (AliveEnemyCnt() == 1)
		if (world.Tick > runToCornerTime && AliveEnemyCnt() <= 3)
		{
			//var tank = PickEnemy();
			var tank = GetMostAngryEnemy();
			if (tank != null)
				StayPerpendicular(tank);
		}

		//SimulateStuck();

		ManageStuck();

		AvoidBullets();
	}
}