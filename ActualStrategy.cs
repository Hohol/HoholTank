using System;
using System.Collections.Generic;
using Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk.Model;
using System.IO;
using System.Linq;

abstract class ActualStrategy
{
	protected const double inf = 1e20;
	protected const int medikitVal = 35;
	protected const int repairVal = 50;
	protected const double regularBulletFriction = 0.995;
	protected const double premiumBulletFriction = 0.99;
	protected const double regularBulletStartSpeed = 16.500438538620 / regularBulletFriction;
	protected const double premiumBulletStartSpeed = 13.068000645325 / premiumBulletFriction;
	const double premiumShotDistance = 850;
	protected const double ricochetAngle = Math.PI / 3;
	protected const string myName = "Hohol";
	protected readonly double diagonalLen = Math.Sqrt(1280 * 1280 + 800 * 800);
	protected const double stayPerpendicularDistance = 800 * 3/4;
	const double bulletWidth = 22.5;
	const double bulletHeight = 7.5;

	const int stuckDetectTickCnt = 100;
	const double stuckDist = 10;
	const int stuckAvoidTime = 35;
	protected const int runToCornerTime = 300;
	protected MoveType prevMove;

	//int dummy;

	public double[] historyX = new double[10000];
	public double[] historyY = new double[10000];
	double stuckDetectedTick = -1;
	protected double cornerX, cornerY;
	protected double targetX, targetY;
	public static List<MoveType> moveTypes = new List<MoveType>();

	protected Tank self;
	static protected World world;
	protected Move move;

	public static HashSet<string> smartAss;

#if TEDDY_BEARS
	public static StreamWriter file;//, teorFile, realFile;
#endif

	public abstract void Move(Tank self, World world, Move move);

	protected void TryShoot(Unit victim, bool shootOnlyToVictim)
	{
		int dummy, resTick;
		double premResX = double.NaN, premResY = double.NaN;
		double regResX, regResY;
		Unit aimPrem = self.PremiumShellCount > 0 ? EmulateShot(true, out dummy, out premResX, out premResY) : null;
		Unit aimReg = EmulateShot(false, out resTick, out regResX, out regResY);

		if (BadAim(aimReg, victim, shootOnlyToVictim, regResX, regResY, ShellType.Regular))
			aimReg = null;
		if (BadAim(aimPrem, victim, shootOnlyToVictim, premResX, premResY, ShellType.Premium))
			aimPrem = null;		

		if (aimPrem != null && ((Tank)aimPrem).HullDurability > 20)
			move.FireType = FireType.Premium;
		else if (aimReg != null)
		{
			double angle = GetCollisionAngle((Tank)aimReg, resTick);
			if (double.IsNaN(angle) || angle < ricochetAngle - Math.PI / 10)
				move.FireType = FireType.Regular;
		}
	}

	protected bool BadAim(Unit aim, Unit victim, bool shootOnlyToVictim, double x, double y, ShellType bulletType)
	{
		if (aim == null)
			return true;
		if (!(aim is Tank) || IsDead((Tank)aim) || shootOnlyToVictim && victim != null && aim.Id != victim.Id)
			return true;
		if (aim is Tank && ((Tank)aim).IsTeammate)
			return true;
		if (aim is Tank && bulletType == ShellType.Premium && CanEscape((Tank)aim, bulletType))
			return true;
		return false;
	}

	protected void MoveTo(double x, double y, double dx, double dy)
	{
		targetX = x;
		targetY = y;
		if (self.GetDistanceTo(x, y) <= self.Height / 2)
			TurnTo(self.X+dx,self.Y+dy);
		else
		{
			Point o = new Point(x, y);
			Point me = new Point(self.X, self.Y);
			Point d = new Point(dx, dy);
			Point oMe = o - me;

			if (Point.scalar(oMe, d) > 0/* && self.GetDistanceTo(x,y) > self.Width*/)
			{
				MoveTo(x, y, true);
			}
			else
				MoveTo(x, y, false);
		}
	}

	protected int AliveTeammateCnt()
	{
		int r = 0;
		foreach (var tank in world.Tanks)
			if (tank.IsTeammate && !IsDead(tank))
				r++;
		return r;
	}

	protected void StayPerpendicular(Tank tank)
	{
		if (self.GetDistanceTo(tank) < stayPerpendicularDistance)
			return;
		if (Math.Abs(tank.GetTurretAngleTo(self)) > Math.PI / 5)
			return;
		//if (tank.RemainingReloadingTime > self.ReloadingTime / 2)
//			return;
		if (ObstacleBetween(self,tank,false))
			return;
		double angle = self.GetAngleTo(tank);
		double allowedAngle = Math.PI / 4;
		if (angle > 0)
		{
			if (angle < Math.PI / 2 - allowedAngle)
				RotateCCW();
			else if (angle > Math.PI + allowedAngle)
				RotateCW();
		}
		else
		{
			if (angle > -Math.PI / 2 + allowedAngle)
				RotateCW();
			else if (angle < -Math.PI / 2 - allowedAngle)
				RotateCCW();
		}
	}

	void RotateCW()
	{
		move.LeftTrackPower = self.EngineRearPowerFactor;
		move.RightTrackPower = -1;
	}

	void RotateCCW()
	{
		move.LeftTrackPower = -1;
		move.RightTrackPower = self.EngineRearPowerFactor;
	}

	protected Tank GetWeakest()
		{
			double mi = inf;
			Tank res = null;
			foreach (var tank in world.Tanks)
			{
				if (tank.IsTeammate || IsDead(tank))
					continue;
				double test = 0;
				if (ObstacleBetween(self, tank, true))
					test = inf / 2;
				else
					test = Math.Min(tank.CrewHealth, tank.HullDurability);

				//test = Math.Min(Math.Abs(tank.GetAngleTo(self)), angleDiff(tank.GetAngleTo(self), Math.PI));

				if (test < mi)
				{
					mi = test;
					res = tank;
				}
			}
			return res;
		}
	

	protected Tank PickEnemy()
	{
		foreach (var tank in world.Tanks)
			if (!IsDead(tank) && !tank.IsTeammate)
				return tank;
		return null;
	}

	protected interface IEnemyEvaluator
	{
		double Evaluate(Tank tank);
	}

	protected void RotateForSafety()
	{
		if (cornerX == -1 || self.GetDistanceTo(cornerX, cornerY) > self.Width)
			return;
		Tank tank = GetMostAngryEnemy();
		if (tank == null)
			return;
		if (cornerX < world.Width / 2 && cornerY < world.Height / 2)
		{
			if (tank.X < tank.Y)
				TurnTo(world.Width, self.Y);
			else
				TurnTo(self.X, world.Height);
		}
		else if (cornerX < world.Width / 2 && cornerY > world.Height / 2)
		{
			if (tank.X + tank.Y < world.Height)
				TurnTo(world.Width, self.Y);
			else
				TurnTo(self.X, 0);
		}
		else if (cornerX > world.Width / 2 && cornerY < world.Height / 2)
		{
			if (tank.X + tank.Y < world.Width)
				TurnTo(self.X, world.Height);
			else
				TurnTo(0, self.Y);
		}
		else
		{
			if (tank.X < tank.Y + world.Width - world.Height)
				TurnTo(self.X, 0);
			else
				TurnTo(0, self.Y);
		}
	}

	protected Tank GetMostAngryEnemy()
	{
		double mi = inf;
		Tank r = null;
		foreach (var tank in world.Tanks)
		{
			if (tank.IsTeammate || IsDead(tank) || self.GetDistanceTo(tank) <= stayPerpendicularDistance)
				continue;
			if (ObstacleBetween(self, tank, false))
				continue;
			double test = Math.Abs(tank.GetTurretAngleTo(self));
			if (test < mi)
			{
				mi = test;
				r = tank;
			}
		}
		return r;
	}

	protected static bool IsMovingBackward(Unit unit)
	{
		double tx = 100 * Math.Cos(unit.Angle);
		double ty = 100 * Math.Sin(unit.Angle);
		return tx * unit.SpeedX + ty * unit.SpeedY < 0;
	}

	protected bool CanEscape(Tank tank, ShellType bulletType)
	{
		double bulletSpeed;
		if (bulletType == ShellType.Premium)
			bulletSpeed = premiumBulletStartSpeed;
		else
			bulletSpeed = regularBulletStartSpeed;

		double angle = self.Angle + self.TurretRelativeAngle;
		double cosa = Math.Cos(angle);
		double sina = Math.Sin(angle);
		double bulletX = self.X + self.VirtualGunLength * cosa;
		double bulletY = self.Y + self.VirtualGunLength * sina;
		double bulletSpeedX = bulletSpeed * cosa;
		double bulletSpeedY = bulletSpeed * sina;

		Point[] bounds = GetBounds(bulletX, bulletY, angle, bulletWidth, bulletHeight);

		List<MoveType> ar;
		if (smartAss.Contains(tank.PlayerName))
			ar = moveTypes;
		else
		{
			ar = new List<MoveType>();
			ar.Add(new MoveType(1, 1));
			ar.Add(new MoveType(-1, -1));
		}

		foreach (var moveType in ar)
		{
			bool can = true;
			foreach (var p in bounds)
			{
				if (Menace(tank, bulletX, bulletY, bulletSpeedX, bulletSpeedY, bulletType, moveType))
				{
					can = false;
					break;
				}
			}
			if (can)
				return true;
		}
		return false;
	}

	static bool Menace(Tank self, double bulletX, double bulletY, double bulletSpeedX, double bulletSpeedY, ShellType bulletType, MoveType moveType)
	{
		double friction;
		if (bulletType == ShellType.Regular)
			friction = regularBulletFriction;
		else
			friction = premiumBulletFriction;
		double startX = bulletX, startY = bulletY;
		MutableTank me = new MutableTank(self);
		Point[] bounds = GetBounds(self, 0);

		for (int tick = 0; tick < 100; tick++)
		{
			bulletSpeedX *= friction;
			bulletSpeedY *= friction;
			bulletX += bulletSpeedX;
			bulletY += bulletSpeedY;

			MutableTank.MoveTank(me, moveType);

			for (int i = 0; i < 4; i++)
			{
				bounds[i].x += me.SpeedX;
				bounds[i].y += me.SpeedY;
			}

			for (int i = 0; i < 4; i++)
			{
				double shiftX = 0;
				double shiftY = 0;
				if (bounds[i].x < 0)
					shiftX = -bounds[i].x;
				if (bounds[i].x > world.Width)
					shiftX = world.Width - bounds[i].x;
				if (bounds[i].y < 0)
					shiftY = -bounds[i].y;
				if (bounds[i].y > world.Height)
					shiftY = world.Height - bounds[i].y;
				me.X += shiftX;
				me.Y += shiftY;
				for (int j = 0; j < 4; j++)
				{
					bounds[j].x += shiftX;
					bounds[j].y += shiftY;
				}
			}

			/*double dummy;
			if(Inside(self,bulletX,bulletY,12))
			{
				dummy = 33;
			}*/
			double dummyX, dummyY;
			double precision;
			if (bulletType == ShellType.Premium)
				precision = 3;
			else
				precision = 0;
			double anglePrecision = Math.PI / 10;
			if (!self.IsTeammate)
			{
				precision *= -1;
				anglePrecision *= -1;
			}
			if (Inside(me, bulletX, bulletY, precision, out dummyX, out dummyY))
			{
				double collisionAngle = GetCollisionAngle(me, bulletX, bulletY, startX, startY);
				if (bulletType == ShellType.Premium || double.IsNaN(collisionAngle)
					|| collisionAngle < ricochetAngle + precision)
					return true;
				else
					return false;
/*#if !TEDDY_BEARS
					preved
					return false;
#endif*/
			}
			if (TestCollision(bulletX, bulletY, tick, -10, -7, self, out dummyX, out dummyY) != null)
				return false;
		}
		return false;
	}

	static bool Menace(Tank self, Shell bullet, MoveType moveType)
	{
		Point[] bounds = GetBounds(bullet, 0);
		foreach (var p in bounds)
		{
			if (Menace(self, p.x, p.y, bullet.SpeedX, bullet.SpeedY, bullet.Type, moveType))
				return true;
		}
		return false;
	}

	public class BulletComparer : IComparer<Shell>
	{
		Tank tank;
		public BulletComparer(Tank tank)
		{
			this.tank = tank;
		}
		public int Compare(Shell a, Shell b)
		{
			if (a.Type != b.Type)
			{
				if (a.Type == ShellType.Premium)
					return -1;
				else
					return 1;
			}
			if (tank.GetDistanceTo(a) < tank.GetDistanceTo(b))
				return -1;
			return 1;
		}
	}

	protected void AvoidBullets()
	{
		List<Shell> bullets = new List<Shell>(world.Shells);
		bullets.Sort(new BulletComparer(self));
		var curMoves = new List<MoveType>(moveTypes);
		if(prevMove != null)
			curMoves.Add(prevMove);
			
		curMoves = curMoves.OrderBy(
			m => Math.Abs(m.LeftTrackPower - move.LeftTrackPower)
			   + Math.Abs(m.RightTrackPower - move.RightTrackPower)).ToList();
		foreach (var bullet in bullets)
		{
			if (Menace(self, bullet,new MoveType(move.LeftTrackPower,move.RightTrackPower)))
			{
				foreach(var curMove in curMoves)
					if (!Menace(self, bullet, curMove))
					{
						move.LeftTrackPower = curMove.LeftTrackPower;
						move.RightTrackPower = curMove.RightTrackPower;
						return;
					}
			}
		}
	}

	int experimentTick = 0;
	bool experimentStarted = false;

	bool Piece()
	{
		return Math.Abs(self.SpeedX) < 1e-5 && Math.Abs(self.SpeedY) < 1e-5
				&& Math.Abs(self.AngularSpeed) < 1e-5;
	}

#if TEDDY_BEARS
	protected void Experiment()
	{
		if (!experimentStarted)
		{
			if (self.GetDistanceTo(world.Width / 2, world.Height / 2) > self.Width)
			{
				MoveTo(world.Width / 2, world.Height / 2, true);
				return;
			}
			/*if (Math.Abs(self.GetAngleTo(world.Width/2,world.Height/2)) > 1e-1)
			{
				TurnTo(world.Width / 2, world.Height / 2);
				return;
			}*/
			/*if (Math.Abs(self.TurretRelativeAngle-Math.PI/2) > 1e-4)
			{
				move.TurretTurn = -(self.TurretRelativeAngle-Math.PI/2);
				return;
			}*/
			experimentStarted = Piece();
		}
		//const int rotateTickCnt = 300;
		if (experimentStarted)
		{
			move.LeftTrackPower = -0.66;
			move.RightTrackPower = 0.256;
			//if (experimentTick == 0)
			//	file.WriteLine(move.LeftTrackPower + " " + move.RightTrackPower + " " + self.CrewHealth);
			file.WriteLine(self.Angle + " " + self.SpeedX + " " + self.SpeedY + " " + self.X + " " + self.Y);
			experimentTick++;
		}
	}
#endif

	void printInfo()
	{
		foreach (var bullet in world.Shells)
		{
			//file.WriteLine(bullet.Id + " " + bullet.Type + " " + bullet.SpeedX + " " + bullet.SpeedY + " " + bullet.X + " " + bullet.Y);
		}
	}

	static protected bool BonusSaves(Tank self, Bonus bonus)
	{
		return bonus != null &&
			(bonus.Type == BonusType.RepairKit && self.HullDurability <= 40 || bonus.Type == BonusType.Medikit && self.CrewHealth <= 40);
	}

	static Point[] GetBounds(Unit unit, int resTick)
	{
		return GetBounds(new MutableUnit(unit), resTick);
	}

	static Point[] GetBounds(MutableUnit unit, int resTick)
	{
		double tx = unit.X + unit.SpeedX * resTick;
		double ty = unit.Y + unit.SpeedY * resTick;
		double alpha = unit.Angle + unit.AngularSpeed * resTick;
		return GetBounds(tx, ty, alpha, unit.Width, unit.Height);
	}

	static Point[] GetBounds(double tx, double ty, double alpha, double width, double height)
	{
		double beta = Math.Atan2(height / 2, width / 2);		
		double D = Math.Sqrt(Util.Sqr(height / 2) + Util.Sqr(width / 2));
		Point t = new Point(tx, ty);

		Point a = new Point(D * Math.Cos(alpha + beta), D * Math.Sin(alpha + beta));
		Point b = new Point(D * Math.Cos(alpha - beta), D * Math.Sin(alpha - beta));
		Point c = new Point(D * Math.Cos(alpha - Math.PI + beta), D * Math.Sin(alpha - Math.PI + beta));
		Point d = new Point(D * Math.Cos(alpha + Math.PI - beta), D * Math.Sin(alpha + Math.PI - beta));

		a = a + t;
		b = b + t;
		c = c + t;
		d = d + t;

		Point[] res = new Point[4];
		res[0] = a; // front right
		res[1] = b; // front left
		res[2] = c; // back left
		res[3] = d; // back right
		return res;
	}

	protected double GetCollisionAngle(Tank tank, int resTick) //always regular bullet
	{
		double bulletSpeed = regularBulletStartSpeed;
		double angle = self.Angle + self.TurretRelativeAngle;
		double cosa = Math.Cos(angle);
		double sina = Math.Sin(angle);
		double bulletX = self.X + self.VirtualGunLength * cosa;
		double bulletY = self.Y + self.VirtualGunLength * sina;
		double startX = bulletX;
		double startY = bulletY;
		double dx = regularBulletStartSpeed * cosa;
		double dy = regularBulletStartSpeed * sina;

		for (int tick = 0; tick < resTick; tick++)
		{
			dx *= regularBulletFriction;
			dy *= regularBulletFriction;
			bulletX += dx;
			bulletY += dy;
			bulletSpeed *= regularBulletFriction;
		}

		Point me = new Point(self.X, self.Y);
		return GetCollisionAngle(new MutableUnit(tank), bulletX, bulletY, startX, startY);
	}

	static protected double GetCollisionAngle(MutableUnit tank, double bulletX, double bulletY, double startX, double startY)
	{

		double dx = bulletX - startX;
		double dy = bulletY - startY;
		double dd = Point.dist(0, 0, dx, dy);
		dx /= dd;
		dy /= dd;
		bool found = false;
		for (int i = 0; i < 50; i++)
		{
			double dummyX, dummyY;
			if (Inside(tank, bulletX, bulletY, -1, out dummyX, out dummyY))
			{
				found = true;
				break;
			}
			bulletX += dx;
			bulletY += dy;
		}
		if (!found)
			return double.NaN;

		Point bullet = new Point(bulletX, bulletY);

		Point[] ar = GetBounds(tank, 0);

		Point a = ar[0], b = ar[1], c = ar[2], d = ar[3];

		Point me = new Point(startX, startY);

		double va = Math.Atan2(me.y - bullet.y, me.x - bullet.x);

		if (Point.Intersect(a, b, bullet, me))
			return angleDiff(va, tank.Angle);
		if (Point.Intersect(b, c, bullet, me))
			return angleDiff(va, tank.Angle - Math.PI / 2);
		if (Point.Intersect(c, d, bullet, me))
			return angleDiff(va, tank.Angle - Math.PI);
		if (Point.Intersect(d, a, bullet, me))
			return angleDiff(va, tank.Angle + Math.PI / 2);
		//throw new Exception();
		return double.NaN;
	}

	static double angleDiff(double a, double b)
	{
		while (a < b - Math.PI)
			a += 2 * Math.PI;
		while (b < a - Math.PI)
			b += 2 * Math.PI;
		return Math.Abs(a - b);
	}

	bool WillUsePremiumBullet(Tank tank)
	{
		return self.PremiumShellCount > 0 && self.GetDistanceTo(tank) <= premiumShotDistance;
	}

	protected Tank GetNearest(double x, double y)
	{
		Tank res = null;
		double mi = inf;
		foreach (Tank tank in world.Tanks)
		{
			if (tank.IsTeammate || IsDead(tank))
				continue;
			double dist = tank.GetDistanceTo(x, y);
			if (ObstacleBetween(self,tank,true))
			{
				dist = inf/2;
			}
			if (dist < mi)
			{
				mi = dist;
				res = tank;
			}
		}
		return res;
	}

	protected bool HaveTimeToTurn(Unit unit)
	{
		return self.RemainingReloadingTime >= TimeToTurn(self, unit) - 5;
	}

	protected void TurnToMovingTank(Tank tank, bool mustRotateTrucks)
	{
		double t = self.GetDistanceTo(tank) / (WillUsePremiumBullet(tank) ? premiumBulletStartSpeed : regularBulletStartSpeed);
		double victimX = tank.X + tank.SpeedX * t;
		double victimY = tank.Y + tank.SpeedY * t;

		double angleDiff = self.GetTurretAngleTo(victimX, victimY);

		if (angleDiff > 0)
		{
			move.TurretTurn = inf;
			if (mustRotateTrucks)
			{
				move.LeftTrackPower = 1;
				move.RightTrackPower = -1;
			}
		}
		else
		{
			move.TurretTurn = -inf;
			if (mustRotateTrucks)
			{
				move.LeftTrackPower = -1;
				move.RightTrackPower = 1;
			}
		}
	}

	bool EasyMoney(Tank tank, int damage)
	{
		if (tank.HullDurability <= damage)
			return true;
		if (tank.CrewHealth <= damage)
			return self.PremiumShellCount > 0 || /*Math.Abs(tank.GetAngleTo(self)) > Math.PI / 2 ||*/
				self.GetDistanceTo(tank) < world.Height / 2;
		return false;
	}

	protected Tank GetAlmostDead()
	{
		int damage = (self.PremiumShellCount > 0 ? 35 : 20);
		Tank res = null;
		double mi = inf;
		foreach (Tank tank in world.Tanks)
		{
			if (tank.IsTeammate || IsDead(tank))
				continue;
			if (!EasyMoney(tank, damage))
				continue;
			if (self.GetDistanceTo(tank) > world.Height)
				continue;
			if (ObstacleBetween(self, tank, true))
				continue;
			double angle = Math.Abs(self.GetTurretAngleTo(tank));
			if (angle < mi)
			{
				mi = angle;
				res = tank;
			}
		}
		if (mi < Math.PI / 2)
			return res;
		return null;
	}



	static bool Inside(Unit unit, double x, double y, double precision)
	{
		double dummyX, dummyY;
		return Inside(unit, x, y, precision, out dummyX, out dummyY);
	}

	static bool Inside(Unit unit, double x, double y, double precision, out double resX, out double resY)
	{
		return Inside(new MutableUnit(unit), x, y, precision, out resX, out resY);
	}

	static bool Inside(MutableUnit unit, double x, double y, double precision, out double resX, out double resY)
	{
		double d = unit.GetDistanceTo(x, y);
		double angle = unit.GetAngleTo(x, y);
		x = d * Math.Cos(angle);
		y = d * Math.Sin(angle);
		double w = unit.Width / 2 + precision;
		double h = unit.Height / 2 + precision;
		double lx = -w, rx = w;
		double ly = -h, ry = h;
		resX = x;
		resY = y;
		return x >= -w && x <= w &&
			   y >= -h && y <= h;
	}

	protected void SimulateStuck()
	{
		if (world.Tick >= 100)
			MoveTo(10000, self.Y, true);
		else
			MoveTo(world.Width / 2, world.Height / 2, true);
		return;
	}

	protected void ManageStuck()
	{
		if (stuckDetectedTick != -1 && world.Tick - stuckDetectedTick > stuckAvoidTime)
			stuckDetectedTick = -1;
		if (stuckDetectedTick == -1 && Stuck())
			stuckDetectedTick = world.Tick;
		if (stuckDetectedTick != -1)
		{
			if (Math.Abs(self.GetAngleTo(world.Width / 2, world.Height / 2)) < Math.PI / 2)
			{
				move.LeftTrackPower = 1;
				move.RightTrackPower = 1;
			}
			else
			{
				move.LeftTrackPower = -1;
				move.RightTrackPower = -1;
			}
		}/**/
	}

	bool Stuck()
	{
		double mi = inf;
		foreach (var tank in world.Tanks)
			if (tank.Id != self.Id)
				mi = Math.Min(mi, self.GetDistanceTo(tank));
		mi = Math.Min(mi, DistanceToBorder());
		if (mi > self.Width / 2 + 5)
			return false;
		if (self.GetDistanceTo(targetX, targetY) < self.Height)
			return false;

		double ma = 0;
		for (int i = world.Tick - stuckDetectTickCnt; i < world.Tick; i++)
		{
			if (i < 0)
				return false;
			ma = Math.Max(ma, self.GetDistanceTo(historyX[i], historyY[i]));
		}
		if (ma > stuckDist)
			return false;

		if (cornerX >= 0 && self.GetDistanceTo(cornerX, cornerY) <= self.Width)
			return false;
		return true;
	}

	virtual protected void MoveBackwards(out double resX, out double resY)
	{
		double x, y;
		double r = 0;// self.Width / 2;
		if (self.X < world.Width / 2)
			x = r;
		else
			x = world.Width - r;
		if (self.Y < world.Height / 2 + 15)
			y = r;
		else
			y = world.Height - r;
		resX = x;
		resY = y;

		MoveTo(resX, resY, false);

		/*double x, y;
		if (self.X < world.Width / 2)
			x = world.Width;
		else
			x = 0;
		y = self.Y;
		TurnTo(x, y);
		double angle = self.GetAngleTo(x, y);
		if (Math.Abs(angle) < maxAngleForBackwards)
		{
			move.LeftTrackPower = -1;
			move.RightTrackPower = -1;
		}/**/
	}	

	static Unit TestCollision(Unit[] ar, double x, double y, int tick, double enemyPrecision, double obstaclePrecision, Unit ignoredUnit,
		out double resX, out double resY)
	{
		foreach (var unit in ar)
		{
			if (ignoredUnit != null && unit.Id == ignoredUnit.Id)
				continue;
			double r = Math.Min(unit.Width, unit.Height) / 2;
			double t = tick;

			if (unit.SpeedX > 0)
				t = Math.Min(t, (world.Width - r - unit.X) / unit.SpeedX);
			else if (unit.SpeedX < 0)
				t = Math.Min(t, (unit.X - r) / (-unit.SpeedX));
			if (unit.SpeedY > 0)
				t = Math.Min(t, (world.Height - r - unit.Y) / unit.SpeedY);
			else if (unit.SpeedY < 0)
				t = Math.Min(t, (unit.Y - r) / (-unit.SpeedY));

			if (t < 0)
				t = 0;

			double ax = unit.X + unit.SpeedX * t;
			double ay = unit.Y + unit.SpeedY * t;

			double dx = ax - unit.X;
			double dy = ay - unit.Y;

			double precision;
			if (unit is Tank && !IsDead((Tank)unit) && !((Tank)unit).IsTeammate)
			{
				/*double k, b;
				GetKB(self.VirtualGunLength, -15, world.Width, -20, out k, out b);
				precision = self.GetDistanceTo(ax, ay) * k + b;*/
				precision = enemyPrecision;
			}
			else
			{
				//precision = 10;
				precision = obstaclePrecision;
			}

			if (Inside(unit, x - dx, y - dy, precision, out resX, out resY))
				return unit;
		}
		resX = resY = double.NaN;
		return null;
	}

	static Unit TestCollision(double x, double y, int tick, double enemyPrecision, double obstaclePrecision, Unit ignoredUnit,
		out double resX, out double resY)
	{
		Unit r = TestCollision(world.Bonuses, x, y, tick, enemyPrecision, obstaclePrecision, ignoredUnit, out resX, out resY);
		if (r != null)
			return r;
		r = TestCollision(world.Tanks, x, y, tick, enemyPrecision, obstaclePrecision, ignoredUnit, out resX, out resY);
		if (r != null)
			return r;
		resX = resY = double.NaN;
		return null;
	}

	protected Unit EmulateShot(bool premium, out int resTick, out double resX, out double resY)
	{
		double bulletSpeed, friction;
		if (premium)
		{
			bulletSpeed = premiumBulletStartSpeed;
			friction = premiumBulletFriction;
		}
		else
		{
			bulletSpeed = regularBulletStartSpeed;
			friction = regularBulletFriction;
		}

		double angle = self.Angle + self.TurretRelativeAngle;
		double cosa = Math.Cos(angle);
		double sina = Math.Sin(angle);
		double x = self.X + self.VirtualGunLength * cosa;
		double y = self.Y + self.VirtualGunLength * sina;
		double dx = bulletSpeed * cosa;// +self.SpeedX;
		double dy = bulletSpeed * sina;// +self.SpeedY;

		double sumDist = 0;
		double needDist = premium ? premiumShotDistance : diagonalLen;

		Point[] bounds = GetBounds(x, y, angle, bulletWidth, bulletHeight);

		for (int tick = 0; ; tick++)
		{
			foreach (var p in bounds)
			{
				Unit unit = TestCollision(p.x, p.y, tick, -10, 2, null, out resX, out resY);
				if (unit != null)
				{
					resTick = tick;
					return unit;
				}
			}
			dx *= friction;
			dy *= friction;
			foreach (var p in bounds)
			{
				p.x += dx;
				p.y += dy;
			}
			bulletSpeed *= friction;
			sumDist += bulletSpeed;
			if (sumDist > needDist)
				break;
		}
		resX = double.NaN;
		resY = double.NaN;
		resTick = -1;
		return null;
	}

	/*void TurnTo(Point p)
	{
		TurnTo(p.x, p.y);
	}*/
	protected void TurnTo(double x, double y)
	{
		double angle = self.GetAngleTo(x, y);

		if (angle > 0)
		{
			move.LeftTrackPower = self.EngineRearPowerFactor;
			move.RightTrackPower = -1;
		}
		else if (angle < -0)
		{
			move.LeftTrackPower = -1;
			move.RightTrackPower = self.EngineRearPowerFactor;
		}
	}

	double DistanceToBorder()
	{
		return Math.Min(
				Math.Min(self.X, self.Y),
				Math.Min(world.Width - self.X, world.Height - self.Y)
			);
	}

	void MoveTo(double x, double y, bool forward)
	{
		targetX = x;
		targetY = y;
		double r = Math.Min(self.Width, self.Height) / 2;
		if (self.GetDistanceTo(x, y) < r)
		{
			TurnTo(world.Width/2,world.Height/2);
			return;
		}

		if (!forward)
		{
			x += 2 * (self.X - x);
			y += 2 * (self.Y - y);
		}
		
		Point[] ar = GetBounds(self, 0);

		Point p = new Point(x, y);
		if (Point.wp(ar[3], ar[0], p) <= 0 && Point.wp(ar[2], ar[1], p) >= 0)
		{
			const double wtf = 0.1;
			if (self.AngularSpeed > wtf)
			{
				move.LeftTrackPower = 1;
				move.RightTrackPower = -1;
			}
			else if (self.AngularSpeed < -wtf)
			{
				move.LeftTrackPower = -1;
				move.RightTrackPower = 1;
			}
			else
			{
				move.LeftTrackPower = 1;
				move.RightTrackPower = 1;
			}
		}
		else
		{
			const double qsgAngle = Math.PI / 4;
			double angle = self.GetAngleTo(x, y);

			if (world.Tick >= runToCornerTime && self.GetDistanceTo(x, y) >= 2 * self.Width && Math.Abs(self.GetAngleTo(x, y)) < qsgAngle &&
				DistanceToBorder() > self.Width / 2)
			//if(false)
			{
				double d = (-Math.Abs(angle) + Math.PI / 2) / (Math.PI / 2);
				d = d * d * d * (d > 0 ? 1 : -1);
				//d *= 3;
				if (angle > 0)
				{
					move.LeftTrackPower = 1;
					move.RightTrackPower = d;
				}
				else
				{
					move.LeftTrackPower = d;
					move.RightTrackPower = 1;
				}
			}
			else
			{
				double maxAngle = Math.PI / 6;
				if (angle > maxAngle)
				{
					move.LeftTrackPower = 1;
					move.RightTrackPower = -1;
				}
				else if (angle < -maxAngle)
				{
					move.LeftTrackPower = -1;
					move.RightTrackPower = 1;
				}
				else
				{
					move.LeftTrackPower = 1;
					move.RightTrackPower = 1;
				}
			}
		}
		if (!forward)
		{
			double tmp = move.LeftTrackPower;
			move.LeftTrackPower = -move.RightTrackPower;
			move.RightTrackPower = -tmp;
		}
	}

	protected void MoveTo(Unit unit, bool forward)
	{
		MoveTo(unit.X, unit.Y, forward);
	}

	T GetWithID<T>(T[] ar, long id) where T : Unit
	{
		foreach (var unit in ar)
			if (unit.Id == id)
				return unit;
		return null;
	}

	static protected bool IsDead(Tank tank)
	{
		return tank.CrewHealth <= 0 || tank.HullDurability <= 0 || tank.PlayerName == "EmptyPlayer"/**/;
	}

	static protected double TimeToTurn(Tank self, Unit unit)
	{
		return Math.Abs(self.GetTurretAngleTo(unit)) / self.TurretTurnSpeed;
	}

	protected Tank GetVictim(/*Func<double,Tank> eval*/)
	{
		double mi = inf;
		Tank res = null;
		foreach (var tank in world.Tanks)
		{
			if (tank.IsTeammate || IsDead(tank))
				continue;
			double test = TimeToTurn(self, tank);
			if (test < 0)
				test = inf / 2;
			if (ObstacleBetween(self, tank, true))
				test = inf / 2;
			double flyTime = (self.GetDistanceTo(tank) - self.VirtualGunLength) / regularBulletStartSpeed;
			test += flyTime;

			//test = Math.Min(Math.Abs(tank.GetAngleTo(self)), angleDiff(tank.GetAngleTo(self), Math.PI));

			if (test < mi)
			{
				mi = test;
				res = tank;
			}
		}
		return res;
	}

	static protected bool ObstacleBetween(Tank self, Unit unit, bool bonusIsObstacle)
	{
		const int stepCnt = 100;
		double dx = (unit.X - self.X) / stepCnt;
		double dy = (unit.Y - self.Y) / stepCnt;
		double x = self.X;
		double y = self.Y;
		for (int i = 0; i <= stepCnt; i++)
		{
			double dummyX, dummyY;
			Unit o = TestCollision(x, y, 0, 0, 0, null, out dummyX, out dummyY);
			x += dx;
			y += dy;

			if (o == null)
				continue;
			if (o.Id == self.Id || o.Id == unit.Id)
				continue;
			if (o is Tank)
				return true;
			if (o is Bonus && bonusIsObstacle)
				return true;
		}
		return false;
	}

	static protected int AliveEnemyCnt()
	{
		int r = 0;
		foreach (Tank tank in world.Tanks)
			if (!tank.IsTeammate && !IsDead(tank))
				r++;
		return r;
	}

	static bool DangerPath(Tank self, Bonus bonus)
	{
		if (AliveEnemyCnt() == 5)
			return false;
		if (BonusSaves(self, bonus) && self.GetDistanceTo(bonus) <= self.Width * 2)
			return false;

		foreach (var e1 in world.Tanks)
			foreach (var e2 in world.Tanks)
				if (e1.Id != e2.Id && !e1.IsTeammate && !e2.IsTeammate && !IsDead(e1) && !IsDead(e2))
				{
					if (Point.Intersect(new Point(self), new Point(bonus), new Point(e1), new Point(e2)))
						return true;
					if (Between(e1, e2, bonus) && Between(e2, e1, bonus))
						return true;
				}
		return false;
	}

	static bool Between(Tank a, Tank b, Bonus bonus)
	{
		double ang1 = Math.Atan2(b.Y - a.Y, b.X - a.X);
		double ang2 = Math.Atan2(bonus.Y - a.Y, bonus.X - a.X);
		return angleDiff(ang1, ang2) <= Math.PI / 10;
	}
	static bool VeryBad(Bonus bonus)
	{
		int r = 0;
		foreach (Tank tank in world.Tanks)
		{
			if (IsDead(tank) || tank.IsTeammate)
				continue;
			if (tank.GetDistanceTo(bonus) < world.Height / 2 && Math.Abs(tank.GetTurretAngleTo(bonus)) < Math.PI / 4)
				r++;
		}
		return r >= 2;
	}

	static double Importance(Tank self, Bonus bonus, bool forward)
	{
		if (VeryBad(bonus))
			return -inf;
		if (ObstacleBetween(self, bonus, false))
			return -inf;
		if (DangerPath(self, bonus))
			return -inf;
		int enemyCnt = AliveEnemyCnt();
		if (enemyCnt == 0) // possible only with EmptyPlayer
			enemyCnt++;
		double dist;
		if (forward)
			dist = self.GetDistanceTo(bonus) + Math.Abs(self.GetAngleTo(bonus)) / Math.PI * world.Width * 0.7 / enemyCnt;
		else
		{
			dist = self.GetDistanceTo(bonus) + (1 - Math.Abs(self.GetAngleTo(bonus)) / Math.PI) * world.Width * 0.7 / enemyCnt;
			dist /= self.EngineRearPowerFactor;
		}

		double k, b;
		GetKB(1, world.Width, 5, world.Width / 4, out k, out b);

		double test = k * enemyCnt + b;

		if (dist > test)
			return -inf;

		double r = 0;

		if (bonus.Type == BonusType.Medikit)
		{
			int[] ar = { 25, 40, 55, 75 };
			for (int i = 0; i < ar.Length; i++)
				if (self.CrewHealth <= ar[i])
				{
					r = ar.Length - i;
					break;
				}
		}
		else if (bonus.Type == BonusType.RepairKit)
		{
			int[] ar = { 20, 35, 50, 100 };
			for (int i = 0; i < ar.Length; i++)
				if (self.HullDurability <= ar[i])
				{
					r = ar.Length - i;
					break;
				}
		}
		else
		{
			if (self.PremiumShellCount < 4)
				r = 2;
		}
		r *= 1e5;
		return r + (10000 - dist);
	}

	static protected Bonus GetBonus(Tank self, out bool forward, Bonus forbidden = null)
	{
		double ma = 0;
		Bonus res = null;
		forward = true;
		foreach (var bonus in world.Bonuses)
		{
			if (bonus == forbidden)
				continue;
			double test = Importance(self, bonus, true);
			if (test > ma)
			{
				ma = test;
				res = bonus;
				forward = true;
			}
			test = Importance(self, bonus, false);
			if (test > ma)
			{
				ma = test;
				res = bonus;
				forward = false;
			}
		}
		return res;
	}

	static void GetKB(double x1, double y1, double x2, double y2, out double k, out double b)
	{
		k = (y1 - y2) / (x1 - x2);
		b = y1 - k * x1;
	}
}