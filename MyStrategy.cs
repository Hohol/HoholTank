using System;
using System.Collections.Generic;
using Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk.Model;
using System.IO;

namespace Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk
{
	public sealed class MyStrategy : IStrategy
	{
		ActualStrategy strat;
		public TankType SelectTank(int tankIndex, int teamSize)
		{
			/*#if TEDDY_BEARS
						file = new StreamWriter("output.txt");
						file.AutoFlush = true;
						realFile = new StreamWriter("real.txt");
						realFile.AutoFlush = true;
						teorFile = new StreamWriter("teor.txt");
						teorFile.AutoFlush = true;
						System.Threading.Thread.CurrentThread.CurrentCulture 
				= System.Globalization.CultureInfo.InvariantCulture;
			#endif*/
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