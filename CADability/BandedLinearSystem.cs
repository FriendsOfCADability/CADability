using System;

namespace CADability
{
    /// <summary>
    /// Square linear system A*X = B where A is a band matrix, as it arises from B-spline interpolation
    /// (each row holds the p+1 basis function values of one parameter). MathNet.Numerics has no band
    /// matrix type, and its dense or sparse LU costs O(n³), whereas the banded LU here costs O(n*kl*(kl+ku)).
    /// Rows are set with <see cref="SetRow"/> or the indexer; the band widths are derived from the
    /// entries, so the caller does not need to know them in advance.
    /// </summary>
    internal sealed class BandedLinearSystem
    {
        private readonly int n;
        private readonly int[] rowStart; // column of the first entry of each row
        private readonly double[][] rowValues; // entries of each row, beginning at rowStart

        public BandedLinearSystem(int size)
        {
            n = size;
            rowStart = new int[size];
            rowValues = new double[size][];
            for (int i = 0; i < size; i++) rowValues[i] = Array.Empty<double>();
        }

        public int Size => n;

        /// <summary>
        /// Sets values[k] as the entry (row, firstColumn + k). Entries of that row outside this range keep their value.
        /// </summary>
        public void SetRow(int row, int firstColumn, double[] values)
        {
            for (int k = 0; k < values.Length; k++) this[row, firstColumn + k] = values[k];
        }

        public double this[int row, int column]
        {
            get
            {
                int k = column - rowStart[row];
                double[] v = rowValues[row];
                return (k >= 0 && k < v.Length) ? v[k] : 0.0;
            }
            set
            {
                if (column < 0 || column >= n) throw new ArgumentOutOfRangeException(nameof(column));
                double[] v = rowValues[row];
                if (v.Length == 0)
                {
                    rowStart[row] = column;
                    rowValues[row] = new double[] { value };
                    return;
                }
                int start = rowStart[row];
                int newStart = Math.Min(start, column);
                int newEnd = Math.Max(start + v.Length, column + 1);
                if (newStart != start || newEnd != start + v.Length)
                {
                    double[] nv = new double[newEnd - newStart];
                    Array.Copy(v, 0, nv, start - newStart, v.Length);
                    rowStart[row] = newStart;
                    rowValues[row] = v = nv;
                    start = newStart;
                }
                v[column - start] = value;
            }
        }

        /// <summary>
        /// Solves A*X = B for a right hand side with rhs.GetLength(1) columns (e.g. the coordinates of the points).
        /// Uses LU decomposition with partial pivoting restricted to the band. Returns null, if the matrix is singular.
        /// </summary>
        public double[,] Solve(double[,] rhs)
        {
            if (rhs.GetLength(0) != n) throw new ArgumentException("rhs must have as many rows as the matrix", nameof(rhs));
            int dim = rhs.GetLength(1);
            // lower and upper band width
            int kl = 0, ku = 0;
            for (int i = 0; i < n; i++)
            {
                if (rowValues[i].Length == 0) return null; // empty row: singular
                kl = Math.Max(kl, i - rowStart[i]);
                ku = Math.Max(ku, rowStart[i] + rowValues[i].Length - 1 - i);
            }
            // Row i is stored in a[i*w ..] and covers the columns i-kl .. i+kl+ku. The additional kl columns
            // on the right take the fill-in caused by row interchanges.
            int w = 2 * kl + ku + 1;
            double[] a = new double[n * w];
            for (int i = 0; i < n; i++)
            {
                double[] v = rowValues[i];
                Array.Copy(v, 0, a, i * w + rowStart[i] - i + kl, v.Length);
            }
            double[] b = new double[n * dim];
            for (int i = 0; i < n; i++)
                for (int d = 0; d < dim; d++) b[i * dim + d] = rhs[i, d];

            // index of entry (r,c) in a is r*w + c - r + kl
            for (int k = 0; k < n; k++)
            {
                int lastRow = Math.Min(k + kl, n - 1);
                int lastCol = Math.Min(k + kl + ku, n - 1);
                int piv = k;
                double pivAbs = Math.Abs(a[k * w + kl]);
                for (int r = k + 1; r <= lastRow; r++)
                {
                    double t = Math.Abs(a[r * w + k - r + kl]);
                    if (t > pivAbs) { pivAbs = t; piv = r; }
                }
                if (pivAbs == 0.0) return null;
                if (piv != k)
                {
                    for (int c = k; c <= lastCol; c++)
                    {
                        int ik = k * w + c - k + kl, ip = piv * w + c - piv + kl;
                        double t = a[ik]; a[ik] = a[ip]; a[ip] = t;
                    }
                    for (int d = 0; d < dim; d++)
                    {
                        double t = b[k * dim + d]; b[k * dim + d] = b[piv * dim + d]; b[piv * dim + d] = t;
                    }
                }
                double pivot = a[k * w + kl];
                for (int r = k + 1; r <= lastRow; r++)
                {
                    int rOff = r * w - r + kl; // + column
                    double f = a[rOff + k] / pivot;
                    if (f == 0.0) continue;
                    a[rOff + k] = 0.0;
                    int kOff = k * w - k + kl;
                    for (int c = k + 1; c <= lastCol; c++) a[rOff + c] -= f * a[kOff + c];
                    for (int d = 0; d < dim; d++) b[r * dim + d] -= f * b[k * dim + d];
                }
            }
            // back substitution, the upper triangle has kl+ku off-diagonals
            double[,] x = new double[n, dim];
            for (int k = n - 1; k >= 0; k--)
            {
                int kOff = k * w - k + kl;
                int lastCol = Math.Min(k + kl + ku, n - 1);
                for (int d = 0; d < dim; d++)
                {
                    double s = b[k * dim + d];
                    for (int c = k + 1; c <= lastCol; c++) s -= a[kOff + c] * x[c, d];
                    x[k, d] = s / a[kOff + k];
                    if (double.IsNaN(x[k, d]) || double.IsInfinity(x[k, d])) return null;
                }
            }
            return x;
        }
    }
}
