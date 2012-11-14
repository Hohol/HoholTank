using System;
using System.Collections.Generic;
using Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk.Model;
using System.IO;

class TwoTankskActualStrategy : ActualStrategy
{
	Tank teammate;

	override public void Move(Tank self, World world, Move move)
	{
		this.self = self;
		this.world = world;
		this.move = move;

		historyX[world.Tick] = self.X;
		historyY[world.Tick] = self.Y;

		foreach (var tank in world.Tanks)
			if (tank.Id != self.Id && tank.IsTeammate)
				teammate = tank;

		/*if (AliveEnemyCnt() == 0)
		{
			Experiment();
			return;
		}/**/

		bool forward;
		Bonus bonus = GetBonus(out forward);
		//bonus = null;
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
			MoveTo(enemy, true);
		}

		bool bonusSaves = BonusSaves(bonus);

		if (world.Tick > runToCornerTime && victim != null && !HaveTimeToTurn(victim) && !bonusSaves)
			TurnToMovingTank(victim, true);

		//SimulateStuck();

		ManageStuck();

		AvoidBullets();
	}

	Tank GetWithSmallestDistSum()
	{
		double mi = inf;
		Tank res = null;
		foreach (var tank in world.Tanks)
		{
			if (tank.IsTeammate || IsDead(tank))
				continue;
			double test = TimeToTurn(self, tank);
			if (!IsDead(teammate))
				test += TimeToTurn(teammate, tank);
			if (test < 0)
				test = inf / 2;
			if (ObstacleBetween(tank, true))
				test = inf / 2;
			double flyTime = (self.GetDistanceTo(tank) - self.VirtualGunLength) / regularBulletStartSpeed;
			test += flyTime;
			if (!IsDead(teammate))
			{
				flyTime = (teammate.GetDistanceTo(tank) - teammate.VirtualGunLength) / regularBulletStartSpeed;
				test += flyTime;
			}

			//test = Math.Min(Math.Abs(tank.GetAngleTo(self)), angleDiff(tank.GetAngleTo(self), Math.PI));

			if (test < mi)
			{
				mi = test;
				res = tank;
			}
		}
		return res;
	}
}