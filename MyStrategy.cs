using System;
using System.Collections.Generic;
using Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk.Model;
using System.IO;
using System.Linq;

namespace Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk
{
	public sealed class MyStrategy : IStrategy
	{
		ActualStrategy strat;
		public TankType SelectTank(int tankIndex, int teamSize)
		{
#if TEDDY_BEARS
			if (tankIndex == 0)
			{
				ActualStrategy.file = new StreamWriter("output.txt");
				ActualStrategy.file.AutoFlush = true;
				/*realFile = new StreamWriter("real.txt");
				realFile.AutoFlush = true;
				teorFile = new StreamWriter("teor.txt");
				teorFile.AutoFlush = true;*/
			}
			System.Threading.Thread.CurrentThread.CurrentCulture 
				= System.Globalization.CultureInfo.InvariantCulture;
#endif

			const int n = 5;
			for (int i = n; i >= -n; i--)
				for (int j = n; j >= -n; j--)
					ActualStrategy.moveTypes.Add(new MoveType(i / (double)n, j / (double)n));
			ActualStrategy.moveTypes = ActualStrategy.moveTypes.OrderBy(
					m => 2-Math.Abs(m.LeftTrackPower+m.RightTrackPower)
				).ToList();
			ActualStrategy.smartAss = new HashSet<string>()
			{
				//  ^_^
			};
			if (teamSize == 1)
				strat = new OneTankActualStrategy();
			else
				strat = new TwoTankskActualStrategy();
			return TankType.Medium;
		}
		public void Move(Tank self, World world, Move move)
		{
			strat.Move(self, world, move);
		}
	}
}

class TankPhisicsConsts
{
  public double resistMove;
  public double resistRotate;
  public double recoilRegular;
  public double recoilPremium;
  public double accMove;
  public double accRotate;
	public static TankPhisicsConsts getPhisicsConsts()
	{
		TankPhisicsConsts ret = new TankPhisicsConsts();
		ret.resistMove = 0.95;
		ret.resistRotate = 0.979487; 
		ret.recoilRegular = 1.58333;
		ret.recoilPremium = ret.recoilRegular;
		ret.accMove = 0.197917 / 2.0;
		ret.accRotate = 4.185881264522092E-4;
		return ret;
	}
};

class MutableUnit
{
	public double Angle, Y, X, Width, Height, SpeedX, SpeedY, AngularSpeed;
	public MutableUnit() { }
	public MutableUnit(Unit unit)
	{
		SpeedX = unit.SpeedX;
		SpeedY = unit.SpeedY;
		AngularSpeed = unit.AngularSpeed;
		Angle = unit.Angle;
		X = unit.X;
		Y = unit.Y;
		Angle = unit.Angle;
		Width = unit.Width;
		Height = unit.Height;
	}
	public double GetDistanceTo(double x, double y)
	{
		return Point.dist(X, Y, x, y);
	}
	public double GetAngleTo(double x, double y)
	{
		double r = Point.dist(X, Y, x, y);
		double angle = Math.Atan2(y - Y, x - X);
		angle -= Angle;
		while (angle > 2 * Math.PI)
			angle -= 2 * Math.PI;
		while (angle < -2 * Math.PI)
			angle += 2 * Math.PI;
		return angle;
	}
}

class MutableTank : MutableUnit
{	
	public double engine_rear_power_factor;
	public int crew_health;
	public MutableTank(Tank tank) : base(tank)
	{		
		engine_rear_power_factor = tank.EngineRearPowerFactor;
		crew_health = tank.CrewHealth;
	}
	public static void MoveTank(MutableTank tank, MoveType moveType)
	{  
	  TankPhisicsConsts phisics = TankPhisicsConsts.getPhisicsConsts();

	  tank.SpeedX *= phisics.resistMove;
	  tank.SpeedY *= phisics.resistMove;
	  tank.AngularSpeed *= phisics.resistRotate;

	  double life = tank.crew_health / 200.0 + 0.5;

	  double leftAcc = (moveType.LeftTrackPower >= 0 ? moveType.LeftTrackPower : moveType.LeftTrackPower * tank.engine_rear_power_factor);
	  double rightAcc = (moveType.RightTrackPower >= 0 ? moveType.RightTrackPower : moveType.RightTrackPower * tank.engine_rear_power_factor);

	  double accMove= life * phisics.accMove * (leftAcc + rightAcc);         
	  tank.SpeedX += accMove * Math.Cos(tank.Angle);
	  tank.SpeedY += accMove * Math.Sin(tank.Angle);

	  tank.X += tank.SpeedX;
	  tank.Y += tank.SpeedY;

	  double accRotate = life * phisics.accRotate * (leftAcc - rightAcc);         
	  tank.AngularSpeed += accRotate;
	  tank.Angle += tank.AngularSpeed;
	}
};

class MoveType
{
	public double LeftTrackPower, RightTrackPower;
	public MoveType(double l, double r)
	{
		LeftTrackPower = l;
		RightTrackPower = r;
	}
};

class Point
{
	public double x, y;
	public Point(double x = 0, double y = 0)
	{
		this.x = x;
		this.y = y;
	}
	public Point(Unit unit)
	{
		this.x = unit.X;
		this.y = unit.Y;
	}
	static public Point operator -(Point a, Point b)
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
	static public double scalar(Point a, Point b)
	{
		return a.x * b.x + a.y * b.y;
	}
	static public double Atan2(Point a)
	{
		return Math.Atan2(a.y, a.x);
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
	public static double dist(double x1, double y1, double x2, double y2)
	{
		double dx = x1 - x2;
		double dy = y1 - y2;
		return Math.Sqrt(dx * dx + dy * dy);
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