using System;
using System.Globalization;
using System.Numerics;

namespace R3E.YaHud.Components.Widget.Radar
{
    public static class RadarCalculator
    {
        public static double[,] RotationMatrixFromEuler(R3E.Data.Vector3<double> euler)
        {
            double x = -euler.X;
            double y = -euler.Y;
            double z = -euler.Z;

            double c1 = Math.Cos(x);
            double s1 = Math.Sin(x);
            double c2 = Math.Cos(y);
            double s2 = Math.Sin(y);
            double c3 = Math.Cos(z);
            double s3 = Math.Sin(z);

            return new double[,]
            {
                { c2 * c3, -c2 * s3, s2 },
                { c1 * s3 + c3 * s1 * s2, c1 * c3 - s1 * s2 * s3, -c2 * s1 },
                { s1 * s3 - c1 * c3 * s2, c3 * s1 + c1 * s2 * s3, c1 * c2 }
            };
        }

        public static Vector3 RotateVector<T>(double[,] matrix, R3E.Data.Vector3<T> vector) where T : struct, IConvertible
        {
            double vx = Convert.ToDouble(vector.X, CultureInfo.InvariantCulture);
            double vy = Convert.ToDouble(vector.Y, CultureInfo.InvariantCulture);
            double vz = Convert.ToDouble(vector.Z, CultureInfo.InvariantCulture);
            float x = (float)(matrix[0, 0] * vx + matrix[0, 1] * vy + matrix[0, 2] * vz);
            float y = (float)(matrix[1, 0] * vx + matrix[1, 1] * vy + matrix[1, 2] * vz);
            float z = (float)(matrix[2, 0] * vx + matrix[2, 1] * vy + matrix[2, 2] * vz);

            return new Vector3(x, y, z);
        }

        public static R3E.Data.Vector3<float> SubtractVector(R3E.Data.Vector3<float> a, R3E.Data.Vector3<float> b)
        {
            return new R3E.Data.Vector3<float>
            {
                X = a.X - b.X,
                Y = a.Y - b.Y,
                Z = a.Z - b.Z
            };
        }

        public static double GetRadarPointerRotation(double d, double x, double z)
        {
            double angle = Math.Acos(Math.Abs(z) / d);

            if (x > 0 && z > 0)
            {
                return angle;
            }
            else if (x > 0 && z < 0)
            {
                return -angle;
            }
            else if (x < 0 && z > 0)
            {
                return -angle;
            }
            else
            {
                return angle;
            }
        }

        public static double MpsToKph(float mps)
        {
            return mps * 3.6;
        }


        public static bool IsCarClose(double frontBack, double leftRight, double length, double width)
        {
            var absFrontBack = Math.Abs(frontBack);
            var absLeftRight = Math.Abs(leftRight);

            return absFrontBack < absLeftRight || absFrontBack <= length;
        }

        public static double DistanceFromZero(Vector3 v)
        {
            return Math.Sqrt(v.X * v.X + v.Z * v.Z);
        }
    }
}