using System;
using System.Collections.Generic;
using Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk
{
	public sealed class MyStrategy : IStrategy
	{
		const double inf = 1e20;
		const int medikitVal = 35;
		const int repairVal = 50;
		const double regularBulletStartSpeed = 16.500438538620;
		const double premiumBulletStartSpeed = 13.068000645325;
		//const double maxAngle = Math.PI / 12;
		const double maxAngleForBackwards = Math.PI / 20;
		const double regularBulletFriction = 0.995;
		const double premiumBulletFriction = 0.99;
		const double backwardPowerQuotient = 0.75;
		const double premiumShotDistance = 850;
		const int firstShootTick = 4;
		readonly double diagonalLen = Math.Sqrt(1280 * 1280 + 800 * 800);

		const int stuckDetectTickCnt = 100;
		const double stuckDist = 10;
		const int stuckAvoidTime = 45;
		const int runToCornerTime = 300;

		double[] historyX = new double[10000];
		double[] historyY = new double[10000];
		double stuckDetectedTick = -1;
		double cornerX, cornerY;

		Tank self;
		World world;
		Move move;

		//System.IO.StreamWriter file = new System.IO.StreamWriter("output.txt");

		public void Move(Tank self, World world, Move move)
		{
			this.self = self;
			this.world = world;
			this.move = move;

			//System.Threading.Thread.CurrentThread.CurrentCulture =
			//	System.Globalization.CultureInfo.InvariantCulture;
			//printInfo();

			historyX[world.Tick] = self.X;
			historyY[world.Tick] = self.Y;

			bool forward;
			Bonus bonus = GetBonus(out forward);
			Tank victim = null;

			cornerX = cornerY = -1;
			if (bonus != null && (world.Tick > runToCornerTime || bonus.Type == BonusType.AmmoCrate))
			{
				MoveTo(bonus, forward);
				victim = GetAlmostDead();
				if (victim == null)
					victim = GetVictim();
			}
			else
			{
				MoveBackwards(out cornerX, out cornerY);
				victim = GetNearest(cornerX, cornerY);
			}
			if (world.Tick <= firstShootTick)
				victim = GetVictim();
			if (victim != null)
				TurnToMovingTank(victim, false);


			int dummy, resTick;
			Unit aimPrem = self.PremiumShellCount > 0 ? EmulateShot(true, out dummy) : null;
			Unit aimReg = EmulateShot(false, out resTick);

			if (aimPrem != null)
				if (!(aimPrem is Tank) || IsDead((Tank)aimPrem) || victim != null && aimPrem.Id != victim.Id)
					aimPrem = null;
			if (aimReg != null)
				if (!(aimReg is Tank) || IsDead((Tank)aimReg) || victim != null && aimReg.Id != victim.Id)	
					aimReg = null;

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
				if(angle < Math.PI/2 * 0.8)
					move.FireType = FireType.Regular;
			}
						
			bool med = bonus != null && bonus.Type != BonusType.AmmoCrate;
			if (world.Tick > runToCornerTime && victim != null && !HaveTimeToTurn(victim) && (self.CrewHealth > 40 && self.HullDurability > 40 || !med))
				TurnToMovingTank(victim, true);

			//SimulateStuck();

			ManageStuck();
		}

		private double GetCollisionAngle(Tank tank, int resTick) //always regular bullet
		{
			double bulletSpeed = regularBulletStartSpeed;
			double angle = self.Angle + self.TurretRelativeAngle;
			double cosa = Math.Cos(angle);
			double sina = Math.Sin(angle);
			double bulletX = self.X + self.VirtualGunLength * cosa;
			double bulletY = self.Y + self.VirtualGunLength * sina;
			double dx = regularBulletStartSpeed * cosa;
			double dy = regularBulletStartSpeed * sina;

			for (int tick = 0; tick < resTick; tick++)
			{
				bulletX += dx;
				bulletY += dy;
				dx *= regularBulletFriction;
				dy *= regularBulletFriction;
				bulletSpeed *= regularBulletFriction;
			}

			double tx = tank.X+tank.SpeedX*resTick;
			double ty = tank.Y+tank.SpeedY*resTick;
			double beta = Math.Atan2(tank.Height / 2, tank.Width / 2);
			double alpha = tank.Angle;
			double D = Math.Sqrt(Util.Sqr(tank.Height/2)+Util.Sqr(tank.Width/2));
			Point t = new Point(tx,ty);
			Point me = new Point(self.X,self.Y);
			Point bullet = new Point(bulletX,bulletY);

			Point a = new Point(D * Math.Cos(alpha + beta), D * Math.Sin(alpha + beta));
			Point b = new Point(D * Math.Cos(alpha - beta), D * Math.Sin(alpha - beta));
			Point c = new Point(D * Math.Cos(alpha - Math.PI + beta), D * Math.Sin(alpha - Math.PI + beta));
			Point d = new Point(D * Math.Cos(alpha + Math.PI - beta), D * Math.Sin(alpha + Math.PI - beta));

			a = a + t;
			b = b + t;
			c = c + t;
			d = d + t;

			double va = Math.Atan2(me.y-bullet.y,me.x-bullet.x);

			if (Point.Intersect(a, b, bullet, me))
				return angleDiff(va, tank.Angle);
			if (Point.Intersect(b, c, bullet, me))
				return angleDiff(va,tank.Angle-Math.PI/2);
			if (Point.Intersect(c, d, bullet, me))
				return angleDiff(va, tank.Angle - Math.PI);
			if (Point.Intersect(d, a, bullet, me))
				return angleDiff(va, tank.Angle + Math.PI / 2);
			//throw new Exception();
			return inf;
		}

		double angleDiff(double a, double b)
		{
			while (a < b - Math.PI)
				a += 2*Math.PI;
			while (b < a - Math.PI)
				b += 2*Math.PI;
			return Math.Abs(a - b);
		}

		bool WillUsePremiumBullet(Tank tank)
		{
			return self.PremiumShellCount > 0 && self.GetDistanceTo(tank) <= premiumShotDistance;
		}

		Tank GetNearest(double x, double y)
		{
			Tank res = null;
			double mi = inf;
			foreach (Tank tank in world.Tanks)
			{
				if (tank.Id == self.Id || IsDead(tank))
					continue;
				double dist = tank.GetDistanceTo(x, y);
				if (dist < mi)
				{
					mi = dist;
					res = tank;
				}
			}
			return res;
		}

		bool HaveTimeToTurn(Unit unit)
		{
			return self.RemainingReloadingTime >= TimeToTurn(unit) - 5;
		}

		void TurnToMovingTank(Tank tank, bool mustRotateTrucks)
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

		Tank GetAlmostDead()
		{
			int damage = (self.PremiumShellCount > 0 ? 35 : 20);
			Tank res = null;
			double mi = inf;
			foreach (Tank tank in world.Tanks)
			{
				if (tank.Id == self.Id || IsDead(tank))
					continue;
				if (!EasyMoney(tank, damage))
					continue;
				if (self.GetDistanceTo(tank) > world.Height)
					continue;
				if (ObstacleBetween(tank, true))
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

		bool Inside(Unit unit, double x, double y, double precision = 1)
		{
			double d = unit.GetDistanceTo(x, y);
			double angle = unit.GetAngleTo(x, y);
			x = d * Math.Cos(angle);
			y = d * Math.Sin(angle);
			double w = unit.Width / 2 * precision;
			double h = unit.Height / 2 * precision;
			return x >= -w && x <= w &&
				   y >= -h / 2 && y <= h;
		}

		void printInfo()
		{
			foreach (var bullet in world.Shells)
			{
				//file.WriteLine(bullet.Id + " " + bullet.Type + " " + bullet.SpeedX + " " + bullet.SpeedY);
			}
		}

		void SimulateStuck()
		{
			if (world.Tick >= 100)
				MoveTo(10000, self.Y, true);
			else
				MoveTo(world.Width / 2, world.Height / 2, true);
			return;
		}

		void ManageStuck()
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
			}
		}

		bool Stuck()
		{
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

		void MoveBackwards(out double resX, out double resY)
		{
			double x, y;
			if (self.X < world.Width / 2)
				x = 0;
			else
				x = world.Width;
			if (self.Y < world.Height / 2 + 15)
				y = 0;
			else
				y = world.Height;
			resX = x;
			resY = y;

			MoveTo(x, y, false);

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

		double dist(double x1, double y1, double x2, double y2)
		{
			double dx = x1 - x2;
			double dy = y1 - y2;
			return Math.Sqrt(dx * dx + dy * dy);
		}

		Unit TestCollision(Unit[] ar, double x, double y, int tick)
		{
			foreach (var unit in ar)
			{
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
				if (unit is Tank && !IsDead((Tank)unit))
				{
					double k, b;
					//if (self.PremiumShellCount > 0)
					//	GetKB(self.VirtualGunLength, 0.9, world.Width, 0.1, out k, out b);
					//else
					GetKB(self.VirtualGunLength, 0.8, world.Width, 0.2, out k, out b);
					precision = self.GetDistanceTo(ax, ay) * k + b;
				}
				else
				{
					if (unit is Tank)
						precision = 1.2;
					else
						precision = 2;
				}

				if (Inside(unit, x - dx, y - dy, precision))
					return unit;
			}
			return null;
		}

		Unit TestCollision(double x, double y, int tick)
		{
			Unit r = TestCollision(world.Bonuses, x, y, tick);
			if (r != null)
				return r;
			r = TestCollision(world.Tanks, x, y, tick);
			if (r != null)
				return r;
			return null;
		}

		Unit EmulateShot(bool premium, out int resTick)
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

			for (int tick = 0;; tick++)
			{
				Unit unit = TestCollision(x, y, tick);
				if (unit != null)
				{
					resTick = tick;
					return unit;
				}
				x += dx;
				y += dy;
				dx *= friction;
				dy *= friction;
				bulletSpeed *= friction;
				sumDist += bulletSpeed;
				if (sumDist > needDist)
					break;
			}
			resTick = -1;
			return null;
		}

		void TurnTo(double x, double y)
		{
			double angle = self.GetAngleTo(x, y);

			if (angle > maxAngleForBackwards)
			{
				move.LeftTrackPower = 1;
				move.RightTrackPower = -1;
			}
			else if (angle < -maxAngleForBackwards)
			{
				move.LeftTrackPower = -1;
				move.RightTrackPower = 1;
			}
		}

		void MoveTo(double x, double y, bool forward)
		{
			if (!forward)
			{
				x += 2 * (self.X - x);
				y += 2 * (self.Y - y);
			}

			double r = Math.Min(self.Width, self.Height) / 2;
			if (self.GetDistanceTo(x, y) < r)
			{
				TurnTo(world.Width / 2, world.Height / 2);
				return;
			}

			double angle = self.GetAngleTo(x, y);

			if (self.GetDistanceTo(x, y) >= 2*self.Width && Math.Abs(self.GetAngleTo(x,y)) < Math.PI/4)
			//if(false)
			{
				double d = (-Math.Abs(angle) + Math.PI / 2) / (Math.PI / 2);
				d = d * d * (d > 0 ? 1 : -1);
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
			if (!forward)
			{
				double tmp = move.LeftTrackPower;
				move.LeftTrackPower = -move.RightTrackPower;
				move.RightTrackPower = -tmp;
			}
		}

		void MoveTo(Unit unit, bool forward)
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

		bool IsDead(Tank tank)
		{
			return tank.CrewHealth <= 0 || tank.HullDurability <= 0 || tank.PlayerName == "EmptyPlayer";
		}

		double TimeToTurn(Unit unit)
		{
			double angle = self.GetTurretAngleTo(unit);
			double angleSpeed = angle > 0 ? self.TurretTurnSpeed : -self.TurretTurnSpeed;
			angleSpeed += self.AngularSpeed;
			double res = angle / angleSpeed;
			return res;
		}

		Tank GetVictim()
		{
			double mi = inf;
			Tank res = null;
			foreach (var tank in world.Tanks)
			{
				if (tank.Id == self.Id || IsDead(tank))
					continue;
				double test = TimeToTurn(tank);
				if (test < 0)
					test = inf / 2;
				if (ObstacleBetween(tank, true))
					test = inf / 2;
				double flyTime = (self.GetDistanceTo(tank) - self.VirtualGunLength) / regularBulletStartSpeed;
				test += flyTime;
				//double test = Math.Abs(self.GetTurretAngleTo(tank));
				if (test < mi)
				{
					mi = test;
					res = tank;
				}
			}
			return res;
		}

		bool ObstacleBetween(Unit unit, bool bonusIsObstacle)
		{
			const int stepCnt = 100;
			double dx = (unit.X - self.X) / stepCnt;
			double dy = (unit.Y - self.Y) / stepCnt;
			double x = self.X;
			double y = self.Y;
			for (int i = 0; i <= stepCnt; i++)
			{
				Unit o = TestCollision(x, y, 0);
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

		int AliveEnemyCnt()
		{
			int r = 0;
			foreach (Tank tank in world.Tanks)
				if (tank.Id != self.Id && !IsDead(tank))
					r++;
			return r;
		}

		double Importance(Bonus bonus, bool forward)
		{
			if (ObstacleBetween(bonus, false))
				return -inf;
			int enemyCnt = AliveEnemyCnt();
			if (enemyCnt == 0)
				enemyCnt++; // possible only with EmptyPlayer
			double r = 0;
			double dist;
			if (forward)
				dist = self.GetDistanceTo(bonus) + Math.Abs(self.GetAngleTo(bonus)) / Math.PI * world.Width * 0.7 / enemyCnt;
			else
			{
				dist = self.GetDistanceTo(bonus) + (1 - Math.Abs(self.GetAngleTo(bonus)) / Math.PI) * world.Width * 0.7 / enemyCnt;
				dist /= backwardPowerQuotient;
			}

			//return 1e9 - dist;

			double f5 = world.Width / 4;
			double f1 = world.Width * 1.75;

			double k = (f5 - f1) / 4;
			double b = f1 - k;

			double test = k * AliveEnemyCnt() + b;

			if (dist > test)
				return -inf;

			if (bonus.Type == BonusType.Medikit)
			{
				int[] ar = { 20, 35, 50, 70 };
				//if (self.CrewHealth > ar[ar.Length - 1] && enemyCnt != 1)
					//return -inf;
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
				//if (self.HullDurability > self.HullMaxDurability - repairVal / 2 && enemyCnt != 1)
					//return -inf;
				for (int i = 0; i < ar.Length; i++)
					if (self.HullDurability <= ar[i])
					{
						r = ar.Length - i;
						break;
					}
			}
			else
			{
				if (self.PremiumShellCount >= 4)
					return -inf;
			}
			r *= 1e5;
			return r + (10000 - dist);
		}

		Bonus GetBonus(out bool forward)
		{
			double ma = 0;
			Bonus res = null;
			forward = true;
			foreach (var bonus in world.Bonuses)
			{
				double test = Importance(bonus, true);
				if (test > ma)
				{
					ma = test;
					res = bonus;
					forward = true;
				}
				test = Importance(bonus, false);
				if (test > ma)
				{
					ma = test;
					res = bonus;
					forward = false;
				}
			}
			return res;
		}

		void GetKB(double x1, double y1, double x2, double y2, out double k, out double b)
		{
			k = (y1 - y2) / (x1 - x2);
			b = y1 - k * x1;
		}

		public TankType SelectTank(int tankIndex, int teamSize)
		{
			return TankType.Medium;
		}
	}
}

struct Point
{
	public double x, y;
	public Point(double x = 0, double y = 0)
	{
		this.x = x;
		this.y = y;
	}
	static public Point operator - (Point a, Point b)
	{
		return new Point(a.x - b.x, a.y - b.y);
	}
	static public Point operator +(Point a, Point b)
	{
		return new Point(a.x + b.x, a.y + b.y);
	}
	static public double wp(Point a, Point b)
	{
		return a.x * b.y - a.y * b.x;
	}
	static public double wp(Point a, Point b, Point c)
	{
		return wp(b - a, c - a);
	}
	static public bool Intersect(Point a, Point b, Point c, Point d)
	{
		return Intersect(a.x, b.x, c.x, d.x) && Intersect(a.y, b.y, c.y, d.y) &&
			wp(a, b, c) * wp(a, b, d) <= 0 && wp(c, d, b) * wp(c, d, a) <= 0;
	}
	static bool Intersect(double l1, double r1, double l2, double r2)
	{
		if (l1 > r1)
			Util.Swap(ref l1, ref r1);
		if (l2 > r2)
			Util.Swap(ref l2, ref r2);
		return Math.Max(l1, l2) <= Math.Min(r1, r2);
	}
}

class Util
{
	static public void Swap<T>(ref T a, ref T b)
	{
		T tmp = a;
		a = b;
		b = tmp;
	}
	static public double Sqr(double a)
	{
		return a * a;
	}	
}