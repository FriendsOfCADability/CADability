using CADability.GeoObject;
using MathNet.Numerics.LinearAlgebra;
using System;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="BandedLinearSystem"/> replaces the dense and sparse MathNet LU in the interpolating constructors
    /// of <see cref="Nurbs{T, C}"/>. It must solve the same systems, including those that need row interchanges,
    /// and report a singular matrix with null instead of returning NaN.
    /// </summary>
    [TestClass]
    public class BandedLinearSystemTests
    {
        private static (BandedLinearSystem banded, Matrix<double> dense) RandomBandMatrix(Random r, int n, int kl, int ku, bool weakDiagonal)
        {
            BandedLinearSystem banded = new BandedLinearSystem(n);
            Matrix<double> dense = Matrix<double>.Build.Dense(n, n);
            for (int i = 0; i < n; i++)
            {
                for (int j = Math.Max(0, i - kl); j <= Math.Min(n - 1, i + ku); j++)
                {
                    double v = r.NextDouble() * 2 - 1;
                    // a zero or tiny diagonal forces the LU to interchange rows
                    if (i == j && weakDiagonal) v = (i % 3 == 0) ? 0.0 : 1e-3 * v;
                    else if (i == j) v += Math.Sign(v) * (kl + ku); // diagonally dominant, like an interpolation matrix
                    banded[i, j] = v;
                    dense[i, j] = v;
                }
            }
            return (banded, dense);
        }

        [TestMethod]
        public void solves_band_matrices_like_the_dense_LU()
        {
            Random r = new Random(17);
            foreach (int n in new[] { 1, 2, 5, 17, 300 })
                foreach ((int kl, int ku) in new[] { (0, 0), (1, 1), (3, 3), (2, 5), (4, 0) })
                    foreach (bool weakDiagonal in new[] { false, true })
                    {
                        if (weakDiagonal && (kl == 0 || ku == 0 || n == 1)) continue; // a triangular matrix with a zero on the diagonal is singular
                        (BandedLinearSystem banded, Matrix<double> dense) = RandomBandMatrix(r, n, kl, ku, weakDiagonal);
                        double[,] rhs = new double[n, 3];
                        for (int i = 0; i < n; i++)
                            for (int d = 0; d < 3; d++) rhs[i, d] = r.NextDouble() * 20 - 10;
                        double[,] x = banded.Solve(rhs);
                        Assert.IsNotNull(x, $"n={n}, kl={kl}, ku={ku}, weak diagonal {weakDiagonal}");
                        Matrix<double> expected = dense.LU().Solve(Matrix<double>.Build.DenseOfArray(rhs));
                        Matrix<double> residual = dense * Matrix<double>.Build.DenseOfArray(x) - Matrix<double>.Build.DenseOfArray(rhs);
                        double scale = Math.Max(1.0, expected.FrobeniusNorm());
                        Assert.IsTrue(residual.FrobeniusNorm() < 1e-9 * scale, $"residual {residual.FrobeniusNorm():E2}, n={n}, kl={kl}, ku={ku}, weak diagonal {weakDiagonal}");
                        Assert.IsTrue((Matrix<double>.Build.DenseOfArray(x) - expected).FrobeniusNorm() < 1e-8 * scale, $"differs from dense LU, n={n}, kl={kl}, ku={ku}, weak diagonal {weakDiagonal}");
                    }
        }

        [TestMethod]
        public void the_band_widths_follow_the_entries()
        {
            // rows set in any order and extended to both sides after the first entry
            BandedLinearSystem banded = new BandedLinearSystem(4);
            banded.SetRow(2, 1, new double[] { 1, 4, 1 });
            banded[0, 0] = 4; banded[0, 1] = 1;
            banded[3, 3] = 4; banded[3, 2] = 1;
            banded[1, 2] = 1; banded[1, 0] = 1; banded[1, 1] = 4;
            Assert.AreEqual(1.0, banded[1, 0]);
            Assert.AreEqual(0.0, banded[3, 0]);
            double[,] x = banded.Solve(new double[,] { { 5 }, { 6 }, { 6 }, { 5 } });
            for (int i = 0; i < 4; i++) Assert.AreEqual(1.0, x[i, 0], 1e-14);
        }

        [TestMethod]
        public void a_singular_matrix_gives_null()
        {
            BandedLinearSystem emptyRow = new BandedLinearSystem(3);
            emptyRow[0, 0] = 1; emptyRow[2, 2] = 1;
            Assert.IsNull(emptyRow.Solve(new double[3, 1]));

            BandedLinearSystem dependentRows = new BandedLinearSystem(3);
            dependentRows.SetRow(0, 0, new double[] { 1, 2 });
            dependentRows.SetRow(1, 0, new double[] { 2, 4 });
            dependentRows.SetRow(2, 1, new double[] { 1, 1 });
            Assert.IsNull(dependentRows.Solve(new double[,] { { 1 }, { 2 }, { 3 } }));
        }

        private static GeoPoint[] Helix(int n)
        {
            GeoPoint[] p = new GeoPoint[n];
            for (int i = 0; i < n; i++)
            {
                double t = 4 * Math.PI * i / (n - 1);
                p[i] = new GeoPoint(10 * Math.Cos(t), 10 * Math.Sin(t), 3 * t);
            }
            return p;
        }

        /// <summary>
        /// The interpolating constructors that now solve with the band LU must still pass through their points,
        /// also for sizes where the dense LU took seconds.
        /// </summary>
        [TestMethod]
        public void the_interpolation_passes_through_the_points()
        {
            foreach (int n in new[] { 4, 30, 2000 })
                foreach (int degree in new[] { 1, 3, 5 })
                {
                    GeoPoint[] points = Helix(n);
                    Nurbs<GeoPoint, GeoPointPole> byChordLength = new Nurbs<GeoPoint, GeoPointPole>(degree, points, false, out double[] param);
                    for (int i = 0; i < n; i++)
                        Assert.IsTrue((byChordLength.CurvePoint(param[i]) | points[i]) < 1e-8, $"chord length, n={n}, degree {degree}, point {i}");

                    double[] parameters = new double[n];
                    for (int i = 0; i < n; i++) parameters[i] = Math.Sqrt(i / (double)(n - 1));
                    Nurbs<GeoPoint, GeoPointPole> byParameters = new Nurbs<GeoPoint, GeoPointPole>(points, parameters, Math.Min(degree, n - 1));
                    for (int i = 0; i < n; i++)
                        Assert.IsTrue((byParameters.CurvePoint(parameters[i]) | points[i]) < 1e-8, $"given parameters, n={n}, degree {degree}, point {i}");
                }
        }
    }
}
