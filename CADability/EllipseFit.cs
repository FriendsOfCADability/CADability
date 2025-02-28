using System;
using System.Collections.Generic;

namespace CADability
{
    /// <summary>
    /// aus https://github.com/seisgo/EllipseFit
    /// </summary>
    class EllipseFitData
    {
        public EllipseFitData()
        {
            algeFlag = false;
            a = b = c = d = e = f = 0;

            geomFlag = false;
            cx = cy = 0;
            rl = rs = 0;
            phi = 0;
        }

        /**
         * @brief alge2geom:    algebraic parameters to geometric parameters
         * @ref:    https://en.wikipedia.org/wiki/Ellipse#In_analytic_geometry
         *          http:homepages.inf.ed.ac.uk/rbf/CVonline/LOCAL_COPIES/FITZGIBBON/ELLIPSE/
         * @note:   The calculation of phi refer to wikipedia is not correct,
         *          refer to Bob Fisher's matlab program.
         *          What's more, the calculated geometric parameters can't back to
         *          initial algebraic parameters from geom2alge();
         */
        public void alge2geom()
        {
            if (!algeFlag)
                return;

            double tmp1 = b * b - 4 * a * c;
            double tmp2 = Math.Sqrt((a - c) * (a - c) + b * b);
            double tmp3 = a * e * e + c * d * d - b * d * e + tmp1 * f;

            double r1 = -Math.Sqrt(2 * tmp3 * (a + c + tmp2)) / tmp1;
            double r2 = -Math.Sqrt(2 * tmp3 * (a + c - tmp2)) / tmp1;
            rl = r1 >= r2 ? r1 : r2;
            rs = r1 <= r2 ? r1 : r2;

            cx = (2 * c * d - b * e) / tmp1;
            cy = (2 * a * e - b * d) / tmp1;

            phi = 0.5 * Math.Atan2(b, a - c);
            if (r1 > r2)
                phi += Math.PI * 2; //  M_PI_2;

            geomFlag = true;
        }

        /**
         * @brief geom2alge:    geometric parameters to algebraic parameters
         * @ref:    https://en.wikipedia.org/wiki/Ellipse#In_analytic_geometry
         */
        void geom2alge()
        {
            if (!geomFlag)
                return;

            a = rl * rl * Math.Sin(phi) * Math.Sin(phi) + rs * rs * Math.Cos(phi) * Math.Cos(phi);
            b = 2 * (rs * rs - rl * rl) * Math.Sin(phi) * Math.Cos(phi);
            c = rl * rl * Math.Cos(phi) * Math.Cos(phi) + rs * rs * Math.Sin(phi) * Math.Sin(phi);
            d = -2 * a * cx - b * cy;
            e = -b * cx - 2 * c * cy;
            f = a * cx * cx + b * cx * cy + c * cy * cy - rl * rl * rs * rs;

            algeFlag = true;
        }


        //algebraic parameters as coefficients of conic section
        public double a, b, c, d, e, f;
        public bool algeFlag;

        //geometric parameters
        double cx;   //centor in x coordinate
        double cy;   //centor in y coordinate
        double rl;   //semimajor: large radius
        double rs;   //semiminor: small radius
        double phi;  //azimuth angel in radian unit
        bool geomFlag;
    };

    class DirectEllipseFit
    {
        public DirectEllipseFit(List<double> xData, List<double> yData)
        {
            m_xData = xData;
            m_yData = yData;
        }

        EllipseFitData doEllipseFit()
        {
            //Data preparation: normalize data
            List<double> xData = symmetricNormalize(m_xData);
            List<double> yData = symmetricNormalize(m_yData);

            //Bulid n*6 design matrix, n is size of xData or yData
            List<List<double>> dMtrx = getDesignMatrix(xData, yData);

            //Bulid 6*6 scatter matrix
            List<List<double>> sMtrx = getScatterMatrix(dMtrx);

            //Build 6*6 constraint matrix
            List<List<double>> cMtrx = getConstraintMatrix();

            //Solve eigensystem
            List<List<double>> eigVV = new List<List<double>>();
            bool flag = solveGeneralEigens(sMtrx, cMtrx, eigVV);
            if (!flag) throw (new ApplicationException("Eigenvalue calculatin failed"));

            EllipseFitData ellip = calcEllipsePara(eigVV);

            return ellip;
        }


        private double getMeanValue(List<double> data)
        {
            double mean = 0;
            for (int i = 0; i < data.Count; ++i)
                mean += data[i];

            return mean / data.Count;
        }
        private double getMaxValue(List<double> data)
        {
            double max = data[0];
            for (int i = 1; i < data.Count; ++i)
                if (data[i] > max)
                    max = data[i];

            return max;
        }
        private double getMinValue(List<double> data)
        {
            double min = data[0];
            for (int i = 1; i < data.Count; ++i)
                if (data[i] < min)
                    min = data[i];

            return min;
        }

        private double getScaleValue(List<double> data)
        {
            return (0.5 * (getMaxValue(data) - getMinValue(data)));
        }

        private List<double> symmetricNormalize(List<double> data)
        {
            double mean = getMeanValue(data);
            double normScale = getScaleValue(data);

            List<double> symData = new List<double>();
            for (int i = 0; i < data.Count; ++i)
                symData.Add((data[i] - mean) / normScale);

            return symData;
        }

        //Make sure xData and yData are of same size
        List<double> dotMultiply(List<double> xData, List<double> yData)
        {
            List<double> product = new List<double>();
            for (int i = 0; i < xData.Count; ++i)
                product.Add(xData[i] * yData[i]);

            return product;
        }

        //Get n*6 design matrix D, make sure xData and yData are of same size
        List<List<double>> getDesignMatrix(List<double> xData,
                                         List<double> yData)
        {
            List<List<double>> designMtrx = new List<List<double>>();

            designMtrx.Add(dotMultiply(xData, xData));
            designMtrx.Add(dotMultiply(xData, yData));
            designMtrx.Add(dotMultiply(yData, yData));
            designMtrx.Add(xData);
            designMtrx.Add(yData);
            List<double> oneVec = new List<double>(xData.Count);
            for (int i = 0; i < xData.Count; i++)
            {
                xData.Add(1.0);
            }
            designMtrx.Add(oneVec);

            return designMtrx;
        }

        //Get 6*6 constraint matrix C
        List<List<double>> getConstraintMatrix()
        {
            List<double> sglVec = new List<double>(6);
            for (int i = 0; i < 6; i++)
            {
                sglVec.Add(0.0);
            }
            List<List<double>> consMtrx = new List<List<double>>(6);
            for (int i = 0; i < 6; i++)
            {
                consMtrx.Add(sglVec);
            }

            consMtrx[1][1] = 1;
            consMtrx[0][2] = -2;
            consMtrx[2][0] = -2;

            return consMtrx;
        }

        //Get 6*6 scatter matrix S from design matrix
        List<List<double>> getScatterMatrix(List<List<double>> dMtrx)
        {
            List<List<double>> tMtrx = transposeMatrix(dMtrx);
            return doMtrxMul(tMtrx, dMtrx);
        }

        //Transpose matrix
        List<List<double>> transposeMatrix(List<List<double>> mtrx)
        {
            List<List<double>> outMtrx = new List<List<double>>();

            for (int i = 0; i < mtrx[0].Count; ++i)
            {
                List<double> tmpVec = new List<double>();
                for (int j = 0; j < mtrx.Count; ++j)
                {
                    tmpVec.Add(mtrx[j][i]);
                }
                outMtrx.Add(tmpVec);
            }

            return outMtrx;
        }

        //Do matrix multiplication, mtrx1: j*l; mtrx2: l*i; return: j*i
        List<List<double>> doMtrxMul(List<List<double>> mtrx1,
                                   List<List<double>> mtrx2)
        {
            List<List<double>> mulMtrx = new List<List<double>>();

            for (int i = 0; i < mtrx2.Count; ++i)
            {
                List<double> tmpVec = new List<double>();
                for (int j = 0; j < mtrx1[0].Count; ++j)
                {
                    double tmpVal = 0;
                    //l is communal for mtrx1 and mtrx2
                    for (int l = 0; l < mtrx1.Count; ++l)
                    {
                        tmpVal += mtrx1[l][j] * mtrx2[i][l];
                    }
                    tmpVec.Add(tmpVal);
                }
                mulMtrx.Add(tmpVec);
            }

            return mulMtrx;
        }


        /**
         * @brief solveGeneralEigens:   Solve generalized eigensystem
         * @note        For real eiginsystem solving.
         * @param sMtrx:    6*6 square matrix in this application
         * @param cMtrx:    6*6 square matrix in this application
         * @param eigVV:    eigenvalues and eigenvectors, 6*7 matrix
         * @return  success or failure status
         */
        bool solveGeneralEigens(List<List<double>> sMtrx,
                            List<List<double>> cMtrx,
                            List<List<double>> eigVV)
        {
            //Parameter initialization
            int nOrder = sMtrx.Count;
            double[,] sArray = mtrx2array(sMtrx);
            double[,] cArray = mtrx2array(cMtrx);
            double[] alphaR = new double[nOrder];
            double[] alphaI = new double[nOrder];
            double[] beta = new double[nOrder];
            double[,] VL = new double[nOrder, nOrder];
            double[,] VR = new double[nOrder, nOrder];
            int lwork = 8 * nOrder;
            double[] work = new double[lwork];
            int info = 0;

            //Solve generalized eigensystem
            //dggev_(ref jobvl, jobvr, nOrder, sArray, nOrder, cArray, nOrder, alphaR,
            //       alphaI, beta, VL, nOrder, VR, nOrder, work, lwork, out info);

            //Output eigenvalues and eigenvectors
            eigVV.Clear();
            for (int i = 0; i < nOrder; ++i)
            {
                List<double> tmpVec = new List<double>();
                tmpVec.Add(alphaR[i] / beta[i]);
                for (int j = 0; j < nOrder; ++j)
                {
                    tmpVec.Add(VR[i, j]);
                }
                eigVV.Add(tmpVec);
            }


            //output calculation status
            if (info == 0)
                return true;
            else
                return false;
        }

        //Convert matrix expression from nested List to 1-order array
        double[,] mtrx2array(List<List<double>> mtrx)
        {
            int nRow = mtrx[0].Count;
            int nCol = mtrx.Count;
            double[,] array = new double[nRow, nCol];

            for (int i = 0; i < nRow; ++i)
            {
                for (int j = 0; j < nCol; ++j)
                {
                    array[i, j] = mtrx[j][i];
                }
            }

            return array;
        }


        /**
         * @brief calcEllipsePara:  calculate ellipse parameter form eigen information
         * @param eigVV:    eigenvalues and eigenvectors
         * @return ellipse parameter
         */
        EllipseFitData calcEllipsePara(List<List<double>> eigVV)
        {
            //Extract eigenvector corresponding to negative eigenvalue
            int eigIdx = -1;
            for (int i = 0; i < eigVV.Count; ++i)
            {
                double tmpV = eigVV[i][0];
                if (tmpV < 1e-6 & !double.IsInfinity(tmpV))
                {
                    eigIdx = i;
                    break;
                }
            }
            if (eigIdx < 0)
                return new EllipseFitData();

            //Unnormalize and get coefficients of conic section
            double tA = eigVV[eigIdx][1];
            double tB = eigVV[eigIdx][2];
            double tC = eigVV[eigIdx][3];
            double tD = eigVV[eigIdx][4];
            double tE = eigVV[eigIdx][5];
            double tF = eigVV[eigIdx][6];

            double mx = getMeanValue(m_xData);
            double my = getMeanValue(m_yData);
            double sx = getScaleValue(m_xData);
            double sy = getScaleValue(m_yData);

            EllipseFitData ellip = new EllipseFitData();
            ellip.a = tA * sy * sy;
            ellip.b = tB * sx * sy;
            ellip.c = tC * sx * sx;
            ellip.d = -2 * tA * sy * sy * mx - tB * sx * sy * my + tD * sx * sy * sy;
            ellip.e = -tB * sx * sy * mx - 2 * tC * sx * sx * my + tE * sx * sx * sy;
            ellip.f = tA * sy * sy * mx * mx + tB * sx * sy * mx * my + tC * sx * sx * my * my
                        - tD * sx * sy * sy * mx - tE * sx * sx * sy * my + tF * sx * sx * sy * sy;
            ellip.algeFlag = true;

            ellip.alge2geom();

            return ellip;
        }


        private List<double> m_xData, m_yData;
    };











}
