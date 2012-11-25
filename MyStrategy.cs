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
				System.Threading.Thread.CurrentThread.CurrentCulture
				= System.Globalization.CultureInfo.InvariantCulture;
			}			
#endif

			if (tankIndex == 0)
			{
				const int n = 5;
				for (int i = n; i >= -n; i--)
					for (int j = n; j >= -n; j--)
						ActualStrategy.moveTypes.Add(new MoveType(i/(double) n, j/(double) n));
				ActualStrategy.moveTypes = ActualStrategy.moveTypes.OrderBy(
					m => 2 - Math.Abs(m.LeftTrackPower + m.RightTrackPower)
					).ToList();
			}
			if (teamSize == 1)
				strat = new OneTankActualStrategy();
			else if (teamSize == 2)
				strat = new TwoTankskActualStrategy();
			else
				strat = new ThreeTanksActualStrategy();
			return TankType.Medium;
		}
		public void Move(Tank self, World world, Move move)
		{
#if TEDDY_BEARS
			/*const int startTick = 220;
			if(world.Tick < startTick)
				return;/**/
#endif
			strat.CommonMove(self, world, move);
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
		var ret = new TankPhisicsConsts();
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
	protected Point[] bounds;
	public Point[] GetBounds()
	{
		return bounds ?? (bounds = ActualStrategy.GetBounds(this));
	}

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
		return Point.Dist(X, Y, x, y);
	}
	public double GetAngleTo(double x, double y)
	{
		//double r = Point.dist(X, Y, x, y);
		double angle = Math.Atan2(y - Y, x - X);
		angle -= Angle;
		while (angle > 2 * Math.PI)
			angle -= 2 * Math.PI;
		while (angle < -2 * Math.PI)
			angle += 2 * Math.PI;
		return angle;
	}
}

class MutableBullet : MutableUnit
{
	readonly ShellType Type;
	readonly double friction;	
	public MutableBullet(Shell bullet)
		: base(bullet)
	{
		Type = bullet.Type;
		if (Type == ShellType.Regular)
			friction = ActualStrategy.regularBulletFriction;
		else
			friction = ActualStrategy.premiumBulletFriction;
	}
	public MutableBullet(Tank tank, ShellType type)
	{
		Angle = tank.Angle + tank.TurretRelativeAngle;
		var cosa = Math.Cos(Angle);
		var sina = Math.Sin(Angle);
		X = tank.X + tank.VirtualGunLength * cosa;
		Y = tank.Y + tank.VirtualGunLength * sina;
		Width = ActualStrategy.bulletWidth;
		Height = ActualStrategy.bulletHeight;
		Type = type;
		if (Type == ShellType.Regular)
		{
			friction = ActualStrategy.regularBulletFriction;
			SpeedX = ActualStrategy.regularBulletStartSpeed * cosa;
			SpeedY = ActualStrategy.regularBulletStartSpeed * sina;
		}
		else
		{
			friction = ActualStrategy.premiumBulletFriction;
			SpeedX = ActualStrategy.premiumBulletStartSpeed * cosa;
			SpeedY = ActualStrategy.premiumBulletStartSpeed * sina;
		}
	}
	public void Move()
	{
		SpeedX *= friction;
		SpeedY *= friction;
		X += SpeedX;
		Y += SpeedY;
		if(bounds != null)
			foreach (var p in bounds)
			{
				p.x += SpeedX;
				p.y += SpeedY;
			}
	}
}

class MutableTank : MutableUnit
{
	TankPhisicsConsts phisics = TankPhisicsConsts.getPhisicsConsts();
	public double EngineRearPowerFactor;
	public int CrewHealth;	
	public MutableTank(Tank tank) : base(tank)
	{		
		EngineRearPowerFactor = tank.EngineRearPowerFactor;
		CrewHealth = tank.CrewHealth;
		bounds = ActualStrategy.GetBounds(tank);
	}
	public void Move(MoveType moveType, World world)
	{ 
	  SpeedX *= phisics.resistMove;
	  SpeedY *= phisics.resistMove;
	  AngularSpeed *= phisics.resistRotate;

	  double life = CrewHealth / 200.0 + 0.5;

	  double leftAcc = (moveType.LeftTrackPower >= 0 ? moveType.LeftTrackPower : moveType.LeftTrackPower * EngineRearPowerFactor);
	  double rightAcc = (moveType.RightTrackPower >= 0 ? moveType.RightTrackPower : moveType.RightTrackPower * EngineRearPowerFactor);

	  double accMove= life * phisics.accMove * (leftAcc + rightAcc);         
	  SpeedX += accMove * Math.Cos(Angle);
	  SpeedY += accMove * Math.Sin(Angle);

	  X += SpeedX;
	  Y += SpeedY;

	  double accRotate = life * phisics.accRotate * (leftAcc - rightAcc);         
	  AngularSpeed += accRotate;
	  Angle += AngularSpeed;

		foreach (var p in bounds)
		{
			p.x += SpeedX;
			p.y += SpeedY;
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
		  X += shiftX;
		  Y += shiftY;
		  for (int j = 0; j < 4; j++)
		  {
			  bounds[j].x += shiftX;
			  bounds[j].y += shiftY;
		  }
	  }
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
		x = unit.X;
		y = unit.Y;
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
	static public double Scalar(Point a, Point b)
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
	public static double Dist(double x1, double y1, double x2, double y2)
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
		var tmp = a;
		a = b;
		b = tmp;
	}
	static public double Sqr(double a)
	{
		return a * a;
	}
}