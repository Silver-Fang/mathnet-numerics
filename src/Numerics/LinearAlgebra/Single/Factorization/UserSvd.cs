﻿// <copyright file="UserSvd.cs" company="Math.NET">
// Math.NET Numerics, part of the Math.NET Project
// http://numerics.mathdotnet.com
// http://github.com/mathnet/mathnet-numerics
// http://mathnetnumerics.codeplex.com
//
// Copyright (c) 2009-2010 Math.NET
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
// </copyright>

using MathNet.Numerics.Properties;
using System;

namespace MathNet.Numerics.LinearAlgebra.Single.Factorization
{
    /// <summary>
    /// <para>A class which encapsulates the functionality of the singular value decomposition (SVD) for <see cref="Matrix{T}"/>.</para>
    /// <para>Suppose M is an m-by-n matrix whose entries are real numbers. 
    /// Then there exists a factorization of the form M = UΣVT where:
    /// - U is an m-by-m unitary matrix;
    /// - Σ is m-by-n diagonal matrix with nonnegative real numbers on the diagonal;
    /// - VT denotes transpose of V, an n-by-n unitary matrix; 
    /// Such a factorization is called a singular-value decomposition of M. A common convention is to order the diagonal 
    /// entries Σ(i,i) in descending order. In this case, the diagonal matrix Σ is uniquely determined 
    /// by M (though the matrices U and V are not). The diagonal entries of Σ are known as the singular values of M.</para>
    /// </summary>
    /// <remarks>
    /// The computation of the singular value decomposition is done at construction time.
    /// </remarks>
    public class UserSvd : Svd
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserSvd"/> class. This object will compute the
        /// the singular value decomposition when the constructor is called and cache it's decomposition.
        /// </summary>
        /// <param name="matrix">The matrix to factor.</param>
        /// <param name="computeVectors">Compute the singular U and VT vectors or not.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="matrix"/> is <c>null</c>.</exception>
        /// <exception cref="NonConvergenceException"></exception>
        public UserSvd(Matrix<float> matrix, bool computeVectors)
        {
            if (matrix == null)
            {
                throw new ArgumentNullException("matrix");
            }

            ComputeVectors = computeVectors;
            var nm = Math.Min(matrix.RowCount + 1, matrix.ColumnCount);
            var matrixCopy = matrix.Clone();

            S = matrixCopy.CreateVector(nm);
            U = matrixCopy.CreateMatrix(matrixCopy.RowCount, matrixCopy.RowCount);
            VT = matrixCopy.CreateMatrix(matrixCopy.ColumnCount, matrixCopy.ColumnCount);

            const int maxiter = 1000;
            var e = new float[matrixCopy.ColumnCount];
            var work = new float[matrixCopy.RowCount];

            int i, j;
            int l, lp1;
            float t;

            var ncu = matrixCopy.RowCount;

            // Reduce matrixCopy to bidiagonal form, storing the diagonal elements
            // In s and the super-diagonal elements in e.
            var nct = Math.Min(matrixCopy.RowCount - 1, matrixCopy.ColumnCount);
            var nrt = Math.Max(0, Math.Min(matrixCopy.ColumnCount - 2, matrixCopy.RowCount));
            var lu = Math.Max(nct, nrt);
            for (l = 0; l < lu; l++)
            {
                lp1 = l + 1;
                if (l < nct)
                {
                    // Compute the transformation for the l-th column and place the l-th diagonal in VectorS[l].
                    var xnorm = Dnrm2Column(matrixCopy, matrixCopy.RowCount, l, l);
                    S[l] = xnorm;
                    if (S[l] != 0.0)
                    {
                        if (matrixCopy.At(l, l) != 0.0)
                        {
                            S[l] = Dsign(S[l], matrixCopy.At(l, l));
                        }

                        DscalColumn(matrixCopy, matrixCopy.RowCount, l, l, 1.0f / S[l]);
                        matrixCopy.At(l, l, (1.0f + matrixCopy.At(l, l)));
                    }

                    S[l] = -S[l];
                }

                for (j = lp1; j < matrixCopy.ColumnCount; j++)
                {
                    if (l < nct)
                    {
                        if (S[l] != 0.0)
                        {
                            // Apply the transformation.
                            t = -Ddot(matrixCopy, matrixCopy.RowCount, l, j, l) / matrixCopy.At(l, l);
                            for (var ii = l; ii < matrixCopy.RowCount; ii++)
                            {
                                matrixCopy.At(ii, j, matrixCopy.At(ii, j) + (t * matrixCopy.At(ii, l)));
                            }
                        }
                    }

                    // Place the l-th row of matrixCopy into  e for the
                    // Subsequent calculation of the row transformation.
                    e[j] = matrixCopy.At(l, j);
                }

                if (ComputeVectors && l < nct)
                {
                    // Place the transformation in u for subsequent back multiplication.
                    for (i = l; i < matrixCopy.RowCount; i++)
                    {
                        U.At(i, l, matrixCopy.At(i, l));
                    }
                }

                if (l >= nrt)
                {
                    continue;
                }

                // Compute the l-th row transformation and place the l-th super-diagonal in e(l).
                var enorm = Dnrm2Vector(e, lp1);
                e[l] = enorm;
                if (e[l] != 0.0)
                {
                    if (e[lp1] != 0.0)
                    {
                        e[l] = Dsign(e[l], e[lp1]);
                    }

                    DscalVector(e, lp1, 1.0f / e[l]);
                    e[lp1] = 1.0f + e[lp1];
                }

                e[l] = -e[l];
                if (lp1 < matrixCopy.RowCount && e[l] != 0.0)
                {
                    // Apply the transformation.
                    for (i = lp1; i < matrixCopy.RowCount; i++)
                    {
                        work[i] = 0.0f;
                    }

                    for (j = lp1; j < matrixCopy.ColumnCount; j++)
                    {
                        for (var ii = lp1; ii < matrixCopy.RowCount; ii++)
                        {
                            work[ii] += e[j] * matrixCopy.At(ii, j);
                        }
                    }

                    for (j = lp1; j < matrixCopy.ColumnCount; j++)
                    {
                        var ww = -e[j] / e[lp1];
                        for (var ii = lp1; ii < matrixCopy.RowCount; ii++)
                        {
                            matrixCopy.At(ii, j, matrixCopy.At(ii, j) + (ww * work[ii]));
                        }
                    }
                }

                if (ComputeVectors)
                {
                    // Place the transformation in v for subsequent back multiplication.
                    for (i = lp1; i < matrixCopy.ColumnCount; i++)
                    {
                        VT.At(i, l, e[i]);
                    }
                }
            }

            // Set up the final bidiagonal matrixCopy or order m.
            var m = Math.Min(matrixCopy.ColumnCount, matrixCopy.RowCount + 1);
            var nctp1 = nct + 1;
            var nrtp1 = nrt + 1;
            if (nct < matrixCopy.ColumnCount)
            {
                S[nctp1 - 1] = matrixCopy.At((nctp1 - 1), (nctp1 - 1));
            }

            if (matrixCopy.RowCount < m)
            {
                S[m - 1] = 0.0f;
            }

            if (nrtp1 < m)
            {
                e[nrtp1 - 1] = matrixCopy.At((nrtp1 - 1), (m - 1));
            }

            e[m - 1] = 0.0f;

            // If required, generate u.
            if (ComputeVectors)
            {
                for (j = nctp1 - 1; j < ncu; j++)
                {
                    for (i = 0; i < matrixCopy.RowCount; i++)
                    {
                        U.At(i, j, 0.0f);
                    }

                    U.At(j, j, 1.0f);
                }

                for (l = nct - 1; l >= 0; l--)
                {
                    if (S[l] != 0.0)
                    {
                        for (j = l + 1; j < ncu; j++)
                        {
                            t = -Ddot(U, matrixCopy.RowCount, l, j, l) / U.At(l, l);
                            for (var ii = l; ii < matrixCopy.RowCount; ii++)
                            {
                                U.At(ii, j, U.At(ii, j) + (t * U.At(ii, l)));
                            }
                        }

                        DscalColumn(U, matrixCopy.RowCount, l, l, -1.0f);
                        U.At(l, l, 1.0f + U.At(l, l));
                        for (i = 0; i < l; i++)
                        {
                            U.At(i, l, 0.0f);
                        }
                    }
                    else
                    {
                        for (i = 0; i < matrixCopy.RowCount; i++)
                        {
                            U.At(i, l, 0.0f);
                        }

                        U.At(l, l, 1.0f);
                    }
                }
            }

            // If it is required, generate v.
            if (ComputeVectors)
            {
                for (l = matrixCopy.ColumnCount - 1; l >= 0; l--)
                {
                    lp1 = l + 1;
                    if (l < nrt)
                    {
                        if (e[l] != 0.0)
                        {
                            for (j = lp1; j < matrixCopy.ColumnCount; j++)
                            {
                                t = -Ddot(VT, matrixCopy.ColumnCount, l, j, lp1) / VT.At(lp1, l);
                                for (var ii = l; ii < matrixCopy.ColumnCount; ii++)
                                {
                                    VT.At(ii, j, VT.At(ii, j) + (t * VT.At(ii, l)));
                                }
                            }
                        }
                    }

                    for (i = 0; i < matrixCopy.ColumnCount; i++)
                    {
                        VT.At(i, l, 0.0f);
                    }

                    VT.At(l, l, 1.0f);
                }
            }

            // Transform s and e so that they are  float .
            for (i = 0; i < m; i++)
            {
                float r;
                if (S[i] != 0.0)
                {
                    t = S[i];
                    r = S[i] / t;
                    S[i] = t;
                    if (i < m - 1)
                    {
                        e[i] = e[i] / r;
                    }

                    if (ComputeVectors)
                    {
                        DscalColumn(U, matrixCopy.RowCount, i, 0, r);
                    }
                }

                // Exit
                if (i == m - 1)
                {
                    break;
                }

                if (e[i] != 0.0)
                {
                    t = e[i];
                    r = t / e[i];
                    e[i] = t;
                    S[i + 1] = S[i + 1] * r;
                    if (ComputeVectors)
                    {
                        DscalColumn(VT, matrixCopy.ColumnCount, i + 1, 0, r);
                    }
                }
            }

            // Main iteration loop for the singular values.
            var mn = m;
            var iter = 0;

            while (m > 0)
            {
                // Quit if all the singular values have been found. If too many iterations have been performed, 
                // throw exception that Convergence Failed
                if (iter >= maxiter)
                {
                    throw new NonConvergenceException();
                }

                // This section of the program inspects for negligible elements in the s and e arrays. On
                // completion the variables kase and l are set as follows.
                // Kase = 1     if VectorS[m] and e[l-1] are negligible and l < m
                // Kase = 2     if VectorS[l] is negligible and l < m
                // Kase = 3     if e[l-1] is negligible, l < m, and VectorS[l, ..., VectorS[m] are not negligible (qr step).
                // Лase = 4     if e[m-1] is negligible (convergence).
                float ztest;
                float test;
                for (l = m - 2; l >= 0; l--)
                {
                    test = Math.Abs(S[l]) + Math.Abs(S[l + 1]);
                    ztest = test + Math.Abs(e[l]);
                    if (ztest.AlmostEqualInDecimalPlaces(test, 7))
                    {
                        e[l] = 0.0f;
                        break;
                    }
                }

                int kase;
                if (l == m - 2)
                {
                    kase = 4;
                }
                else
                {
                    int ls;
                    for (ls = m - 1; ls > l; ls--)
                    {
                        test = 0.0f;
                        if (ls != m - 1)
                        {
                            test = test + Math.Abs(e[ls]);
                        }

                        if (ls != l + 1)
                        {
                            test = test + Math.Abs(e[ls - 1]);
                        }

                        ztest = test + Math.Abs(S[ls]);
                        if (ztest.AlmostEqualInDecimalPlaces(test, 7))
                        {
                            S[ls] = 0.0f;
                            break;
                        }
                    }

                    if (ls == l)
                    {
                        kase = 3;
                    }
                    else if (ls == m - 1)
                    {
                        kase = 1;
                    }
                    else
                    {
                        kase = 2;
                        l = ls;
                    }
                }

                l = l + 1;

                // Perform the task indicated by kase.
                int k;
                float f;
                float sn;
                float cs;
                switch (kase)
                {
                    // Deflate negligible VectorS[m].
                    case 1:
                        f = e[m - 2];
                        e[m - 2] = 0.0f;
                        float t1;
                        for (var kk = l; kk < m - 1; kk++)
                        {
                            k = m - 2 - kk + l;
                            t1 = S[k];
                            Drotg(ref t1, ref f, out cs, out sn);
                            S[k] = t1;
                            if (k != l)
                            {
                                f = -sn * e[k - 1];
                                e[k - 1] = cs * e[k - 1];
                            }

                            if (ComputeVectors)
                            {
                                Drot(VT, matrixCopy.ColumnCount, k, m - 1, cs, sn);
                            }
                        }

                        break;

                    // Split at negligible VectorS[l].
                    case 2:
                        f = e[l - 1];
                        e[l - 1] = 0.0f;
                        for (k = l; k < m; k++)
                        {
                            t1 = S[k];
                            Drotg(ref t1, ref f, out cs, out sn);
                            S[k] = t1;
                            f = -sn * e[k];
                            e[k] = cs * e[k];
                            if (ComputeVectors)
                            {
                                Drot(U, matrixCopy.RowCount, k, l - 1, cs, sn);
                            }
                        }

                        break;

                    // Perform one qr step.
                    case 3:
                        // Calculate the shift.
                        var scale = 0.0f;
                        scale = Math.Max(scale, Math.Abs(S[m - 1]));
                        scale = Math.Max(scale, Math.Abs(S[m - 2]));
                        scale = Math.Max(scale, Math.Abs(e[m - 2]));
                        scale = Math.Max(scale, Math.Abs(S[l]));
                        scale = Math.Max(scale, Math.Abs(e[l]));
                        var sm = S[m - 1] / scale;
                        var smm1 = S[m - 2] / scale;
                        var emm1 = e[m - 2] / scale;
                        var sl = S[l] / scale;
                        var el = e[l] / scale;
                        var b = (((smm1 + sm) * (smm1 - sm)) + (emm1 * emm1)) / 2.0f;
                        var c = (sm * emm1) * (sm * emm1);
                        var shift = 0.0f;
                        if (b != 0.0 || c != 0.0)
                        {
                            shift = (float)Math.Sqrt((b * b) + c);
                            if (b < 0.0)
                            {
                                shift = -shift;
                            }

                            shift = c / (b + shift);
                        }

                        f = ((sl + sm) * (sl - sm)) + shift;
                        var g = sl * el;

                        // Chase zeros.
                        for (k = l; k < m - 1; k++)
                        {
                            Drotg(ref f, ref g, out cs, out sn);
                            if (k != l)
                            {
                                e[k - 1] = f;
                            }

                            f = (cs * S[k]) + (sn * e[k]);
                            e[k] = (cs * e[k]) - (sn * S[k]);
                            g = sn * S[k + 1];
                            S[k + 1] = cs * S[k + 1];
                            if (ComputeVectors)
                            {
                                Drot(VT, matrixCopy.ColumnCount, k, k + 1, cs, sn);
                            }

                            Drotg(ref f, ref g, out cs, out sn);
                            S[k] = f;
                            f = (cs * e[k]) + (sn * S[k + 1]);
                            S[k + 1] = (-sn * e[k]) + (cs * S[k + 1]);
                            g = sn * e[k + 1];
                            e[k + 1] = cs * e[k + 1];
                            if (ComputeVectors && k < matrixCopy.RowCount)
                            {
                                Drot(U, matrixCopy.RowCount, k, k + 1, cs, sn);
                            }
                        }

                        e[m - 2] = f;
                        iter = iter + 1;
                        break;

                    // Convergence.
                    case 4:
                        // Make the singular value  positive
                        if (S[l] < 0.0)
                        {
                            S[l] = -S[l];
                            if (ComputeVectors)
                            {
                                DscalColumn(VT, matrixCopy.ColumnCount, l, 0, -1.0f);
                            }
                        }

                        // Order the singular value.
                        while (l != mn - 1)
                        {
                            if (S[l] >= S[l + 1])
                            {
                                break;
                            }

                            t = S[l];
                            S[l] = S[l + 1];
                            S[l + 1] = t;
                            if (ComputeVectors && l < matrixCopy.ColumnCount)
                            {
                                Dswap(VT, matrixCopy.ColumnCount, l, l + 1);
                            }

                            if (ComputeVectors && l < matrixCopy.RowCount)
                            {
                                Dswap(U, matrixCopy.RowCount, l, l + 1);
                            }

                            l = l + 1;
                        }

                        iter = 0;
                        m = m - 1;
                        break;
                }
            }

            if (ComputeVectors)
            {
                VT = VT.Transpose();
            }

            // Adjust the size of s if rows < columns. We are using ported copy of linpack's svd code and it uses
            // a singular vector of length mRows+1 when mRows < mColumns. The last element is not used and needs to be removed.
            // we should port lapack's svd routine to remove this problem.
            if (matrixCopy.RowCount < matrixCopy.ColumnCount)
            {
                nm--;
                var tmp = matrixCopy.CreateVector(nm);
                for (i = 0; i < nm; i++)
                {
                    tmp[i] = S[i];
                }

                S = tmp;
            }
        }

        /// <summary>
        /// Calculates absolute value of <paramref name="z1"/> multiplied on signum function of <paramref name="z2"/>
        /// </summary>
        /// <param name="z1">Double value z1</param>
        /// <param name="z2">Double value z2</param>
        /// <returns>Result multiplication of signum function and absolute value</returns>
        private static float Dsign(float z1, float z2)
        {
            return Math.Abs(z1) * (z2 / Math.Abs(z2));
        }

        /// <summary>
        /// Swap column  <paramref name="columnA"/>  and  <paramref name="columnB"/>
        /// </summary>
        /// <param name="a">Source matrix</param>
        /// <param name="rowCount">The number of rows in <paramref name="a"/></param>
        /// <param name="columnA">Column A index to swap</param>
        /// <param name="columnB">Column B index to swap</param>
        private static void Dswap(Matrix<float> a, int rowCount, int columnA, int columnB)
        {
            for (var i = 0; i < rowCount; i++)
            {
                var z = a.At(i, columnA);
                a.At(i, columnA, a.At(i, columnB));
                a.At(i, columnB, z);
            }
        }

        /// <summary>
        /// Scale column <paramref name="column"/> by <paramref name="z"/> starting from row <paramref name="rowStart"/>
        /// </summary>
        /// <param name="a">Source matrix</param>
        /// <param name="rowCount">The number of rows in <paramref name="a"/> </param>
        /// <param name="column">Column to scale</param>
        /// <param name="rowStart">Row to scale from</param>
        /// <param name="z">Scale value</param>
        private static void DscalColumn(Matrix<float> a, int rowCount, int column, int rowStart, float z)
        {
            for (var i = rowStart; i < rowCount; i++)
            {
                a.At(i, column, a.At(i, column) * z);
            }
        }

        /// <summary>
        /// Scale vector <paramref name="a"/> by <paramref name="z"/> starting from index <paramref name="start"/>
        /// </summary>
        /// <param name="a">Source vector</param>
        /// <param name="start">Row to scale from</param>
        /// <param name="z">Scale value</param>
        private static void DscalVector(float[] a, int start, float z)
        {
            for (var i = start; i < a.Length; i++)
            {
                a[i] = a[i] * z;
            }
        }

        /// <summary>
        /// Given the Cartesian coordinates (da, db) of a point p, these fucntion return the parameters da, db, c, and s 
        /// associated with the Givens rotation that zeros the y-coordinate of the point.
        /// </summary>
        /// <param name="da">Provides the x-coordinate of the point p. On exit contains the parameter r associated with the Givens rotation</param>
        /// <param name="db">Provides the y-coordinate of the point p. On exit contains the parameter z associated with the Givens rotation</param>
        /// <param name="c">Contains the parameter c associated with the Givens rotation</param>
        /// <param name="s">Contains the parameter s associated with the Givens rotation</param>
        /// <remarks>This is equivalent to the DROTG LAPACK routine.</remarks>
        private static void Drotg(ref float da, ref float db, out float c, out float s)
        {
            float r, z;

            var roe = db;
            var absda = Math.Abs(da);
            var absdb = Math.Abs(db);
            if (absda > absdb)
            {
                roe = da;
            }

            var scale = absda + absdb;
            if (scale == 0.0)
            {
                c = 1.0f;
                s = 0.0f;
                r = 0.0f;
                z = 0.0f;
            }
            else
            {
                var sda = da / scale;
                var sdb = db / scale;
                r = scale * (float)Math.Sqrt((sda * sda) + (sdb * sdb));
                if (roe < 0.0)
                {
                    r = -r;
                }

                c = da / r;
                s = db / r;
                z = 1.0f;
                if (absda > absdb)
                {
                    z = s;
                }

                if (absdb >= absda && c != 0.0)
                {
                    z = 1.0f / c;
                }
            }

            da = r;
            db = z;
        }

        /// <summary>
        /// Calculate Norm 2 of the column <paramref name="column"/> in matrix <paramref name="a"/> starting from row <paramref name="rowStart"/>
        /// </summary>
        /// <param name="a">Source matrix</param>
        /// <param name="rowCount">The number of rows in <paramref name="a"/></param>
        /// <param name="column">Column index</param>
        /// <param name="rowStart">Start row index</param>
        /// <returns>Norm2 (Euclidean norm) of the column</returns>
        private static float Dnrm2Column(Matrix<float> a, int rowCount, int column, int rowStart)
        {
            float s = 0;
            for (var i = rowStart; i < rowCount; i++)
            {
                s += a.At(i, column) * a.At(i, column);
            }

            return (float)Math.Sqrt(s);
        }

        /// <summary>
        /// Calculate Norm 2 of the vector <paramref name="a"/> starting from index <paramref name="rowStart"/>
        /// </summary>
        /// <param name="a">Source vector</param>
        /// <param name="rowStart">Start index</param>
        /// <returns>Norm2 (Euclidean norm) of the vector</returns>
        private static float Dnrm2Vector(float[] a, int rowStart)
        {
            float s = 0;
            for (var i = rowStart; i < a.Length; i++)
            {
                s += a[i] * a[i];
            }

            return (float)Math.Sqrt(s);
        }

        /// <summary>
        /// Calculate dot product of <paramref name="columnA"/> and <paramref name="columnB"/>
        /// </summary>
        /// <param name="a">Source matrix</param>
        /// <param name="rowCount">The number of rows in <paramref name="a"/></param>
        /// <param name="columnA">Index of column A</param>
        /// <param name="columnB">Index of column B</param>
        /// <param name="rowStart">Starting row index</param>
        /// <returns>Dot product value</returns>
        private static float Ddot(Matrix<float> a, int rowCount, int columnA, int columnB, int rowStart)
        {
            var z = 0.0f;
            for (var i = rowStart; i < rowCount; i++)
            {
                z += a.At(i, columnB) * a.At(i, columnA);
            }

            return z;
        }

        /// <summary>
        /// Performs rotation of points in the plane. Given two vectors x <paramref name="columnA"/> and y <paramref name="columnB"/>, 
        /// each vector element of these vectors is replaced as follows: x(i) = c*x(i) + s*y(i); y(i) = c*y(i) - s*x(i)
        /// </summary>
        /// <param name="a">Source matrix</param>
        /// <param name="rowCount">The number of rows in <paramref name="a"/></param>
        /// <param name="columnA">Index of column A</param>
        /// <param name="columnB">Index of column B</param>
        /// <param name="c">Scalar "c" value</param>
        /// <param name="s">Scalar "s" value</param>
        private static void Drot(Matrix<float> a, int rowCount, int columnA, int columnB, float c, float s)
        {
            for (var i = 0; i < rowCount; i++)
            {
                var z = (c * a.At(i, columnA)) + (s * a.At(i, columnB));
                var tmp = (c * a.At(i, columnB)) - (s * a.At(i, columnA));
                a.At(i, columnB, tmp);
                a.At(i, columnA, z);
            }
        }
 
        /// <summary>
        /// Solves a system of linear equations, <b>AX = B</b>, with A SVD factorized.
        /// </summary>
        /// <param name="input">The right hand side <see cref="Matrix{T}"/>, <b>B</b>.</param>
        /// <param name="result">The left hand side <see cref="Matrix{T}"/>, <b>X</b>.</param>
        public override void Solve(Matrix<float> input, Matrix<float> result)
        {
            // Check for proper arguments.
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            if (!ComputeVectors)
            {
                throw new InvalidOperationException(Resources.SingularVectorsNotComputed);
            }

            // The solution X should have the same number of columns as B
            if (input.ColumnCount != result.ColumnCount)
            {
                throw new ArgumentException(Resources.ArgumentMatrixSameColumnDimension);
            }

            // The dimension compatibility conditions for X = A\B require the two matrices A and B to have the same number of rows
            if (U.RowCount != input.RowCount)
            {
                throw new ArgumentException(Resources.ArgumentMatrixSameRowDimension);
            }

            // The solution X row dimension is equal to the column dimension of A
            if (VT.ColumnCount != result.RowCount)
            {
                throw new ArgumentException(Resources.ArgumentMatrixSameColumnDimension);
            }

            var mn = Math.Min(U.RowCount, VT.ColumnCount);
            var bn = input.ColumnCount;

            var tmp = new float[VT.ColumnCount];

            for (var k = 0; k < bn; k++)
            {
                for (var j = 0; j < VT.ColumnCount; j++)
                {
                    float value = 0;
                    if (j < mn)
                    {
                        for (var i = 0; i < U.RowCount; i++)
                        {
                            value += U.At(i, j) * input.At(i, k);
                        }

                        value /= S[j];
                    }

                    tmp[j] = value;
                }

                for (var j = 0; j < VT.ColumnCount; j++)
                {
                    float value = 0;
                    for (var i = 0; i < VT.ColumnCount; i++)
                    {
                        value += VT.At(i, j) * tmp[i];
                    }

                    result.At(j, k, value);
                }
            }
        }

        /// <summary>
        /// Solves a system of linear equations, <b>Ax = b</b>, with A SVD factorized.
        /// </summary>
        /// <param name="input">The right hand side vector, <b>b</b>.</param>
        /// <param name="result">The left hand side <see cref="Matrix{T}"/>, <b>x</b>.</param>
        public override void Solve(Vector<float> input, Vector<float> result)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            if (!ComputeVectors)
            {
                throw new InvalidOperationException(Resources.SingularVectorsNotComputed);
            }

            // Ax=b where A is an m x n matrix
            // Check that b is a column vector with m entries
            if (U.RowCount != input.Count)
            {
                throw new ArgumentException(Resources.ArgumentVectorsSameLength);
            }

            // Check that x is a column vector with n entries
            if (VT.ColumnCount != result.Count)
            {
                throw Matrix.DimensionsDontMatch<ArgumentException>(VT, result);
            }

            var mn = Math.Min(U.RowCount, VT.ColumnCount);
            var tmp = new float[VT.ColumnCount];
            float value;
            for (var j = 0; j < VT.ColumnCount; j++)
            {
                value = 0;
                if (j < mn)
                {
                    for (var i = 0; i < U.RowCount; i++)
                    {
                        value += U.At(i, j) * input[i];
                    }

                    value /= S[j];
                }

                tmp[j] = value;
            }

            for (var j = 0; j < VT.ColumnCount; j++)
            {
                value = 0;
                for (int i = 0; i < VT.ColumnCount; i++)
                {
                    value += VT.At(i, j) * tmp[i];
                }

                result[j] = value;
            }
        }
    }
}
