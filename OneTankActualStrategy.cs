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
#if TEDDY_BEARS
		//bonus = null;
#endif
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

		TryShoot(victim, shootOnlyToVictim);

		if (world.Tick < firstShootTick)
			move.FireType = FireType.None;

		RotateForSafety();

		bool bonusSaves = BonusSaves(self, bonus);

		if (world.Tick > runToCornerTime && victim != null && !HaveTimeToTurn(victim) && !bonusSaves)
			TurnToMovingTank(victim, true);

		if (world.Tick > runToCornerTime && AliveEnemyCnt() <= 1)
		{
			var tank = GetMostAngryEnemy();
			if (tank != null)
				StayPerpendicular(tank);
		}

		ManageStuck();
		//if (victim != null)
			//TurnTo(victim.X,victim.Y);
		AvoidBullets();
		prevMove = new MoveType(move.LeftTrackPower, move.RightTrackPower);
	}

	protected override bool BadAim(Unit aim, Unit victim, bool shootOnlyToVictim, double x, double y, ShellType bulletType)
	{
		if(BadAim(aim,victim,shootOnlyToVictim,bulletType))
			return true;
		if (self.GetDistanceTo(aim) < self.Width * 3)
			return false;		
		return false;
	}
}