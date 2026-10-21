namespace WetWare.Core.Mathematics;

/// <summary>Eigen-decomposition of a real symmetric matrix with the cyclic Jacobi method (fine for n ≤ ~100).</summary>
public static class SymmetricEigen
{
    /// <summary>
    /// Returns eigenvalues in descending order and the matching unit eigenvectors as columns of <c>Vectors</c>.
    /// </summary>
    public static (double[] Values, double[,] Vectors) Decompose(double[,] matrix, int maxSweeps = 60)
    {
        int n = matrix.GetLength(0);
        var a = (double[,])matrix.Clone();
        var v = new double[n, n];
        for (int i = 0; i < n; i++) v[i, i] = 1;

        for (int sweep = 0; sweep < maxSweeps; sweep++)
        {
            double off = 0;
            for (int p = 0; p < n; p++)
                for (int q = p + 1; q < n; q++)
                    off += a[p, q] * a[p, q];
            if (off < 1e-20) break;

            for (int p = 0; p < n; p++)
            {
                for (int q = p + 1; q < n; q++)
                {
                    if (Math.Abs(a[p, q]) < 1e-15) continue;
                    double theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                    if (theta == 0) t = 1;
                    double c = 1 / Math.Sqrt(t * t + 1), s = t * c;

                    for (int k = 0; k < n; k++)
                    {
                        double akp = a[k, p], akq = a[k, q];
                        a[k, p] = c * akp - s * akq;
                        a[k, q] = s * akp + c * akq;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        double apk = a[p, k], aqk = a[q, k];
                        a[p, k] = c * apk - s * aqk;
                        a[q, k] = s * apk + c * aqk;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        double vkp = v[k, p], vkq = v[k, q];
                        v[k, p] = c * vkp - s * vkq;
                        v[k, q] = s * vkp + c * vkq;
                    }
                }
            }
        }

        var order = Enumerable.Range(0, n).OrderByDescending(i => a[i, i]).ToArray();
        var values = order.Select(i => a[i, i]).ToArray();
        var vectors = new double[n, n];
        for (int col = 0; col < n; col++)
            for (int row = 0; row < n; row++)
                vectors[row, col] = v[row, order[col]];
        return (values, vectors);
    }
}
