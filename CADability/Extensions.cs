using System;
using System.Collections;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace CADability
{
    public class CombinedEnumerable<T> : IEnumerable<T>, IEnumerator<T>, IEnumerator
    {
        IEnumerable<T>[] toEnumerate;
        int currentIndex;
        IEnumerator<T> currentEnumerator;
        public CombinedEnumerable(params IEnumerable<T>[] enums)
        {
            toEnumerate = enums;
            currentIndex = 0;
            currentEnumerator = null;
        }

        T IEnumerator<T>.Current
        {
            get
            {
                return currentEnumerator.Current;
            }
        }

        object IEnumerator.Current
        {
            get
            {
                return currentEnumerator.Current;
            }
        }

        void IDisposable.Dispose()
        {
            currentIndex = -1;
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        bool IEnumerator.MoveNext()
        {
            if (currentEnumerator == null) currentEnumerator = toEnumerate[0].GetEnumerator();
            while (!currentEnumerator.MoveNext())
            {
                ++currentIndex;
                if (currentIndex < toEnumerate.Length)
                {
                    currentEnumerator = toEnumerate[currentIndex].GetEnumerator();
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        void IEnumerator.Reset()
        {
            currentIndex = 0;
            currentEnumerator = null;
        }
        //public static IEnumerable<T> operator +(IEnumerable<T> t1, IEnumerable<T> t2)
        //{
        //    return new CombinedEnumerable(t1, t2);
        //}
    }

    public class LookedUpEnumerable<T> : IEnumerable<T>, IEnumerator<T>, IEnumerator
    {
        IEnumerable<T> toEnumerate;
        IEnumerator<T> currentEnumerator;
        Dictionary<T, T> lookUp;
        public LookedUpEnumerable(IEnumerable<T> enumerable, Dictionary<T, T> lookUp)
        {
            toEnumerate = enumerable;
            currentEnumerator = null;
            this.lookUp = lookUp;
            currentEnumerator = enumerable.GetEnumerator();
        }

        T IEnumerator<T>.Current
        {
            get
            {
                if (lookUp.TryGetValue(currentEnumerator.Current, out T val)) return val;
                else return currentEnumerator.Current;
            }
        }

        object IEnumerator.Current
        {
            get
            {
                if (lookUp.TryGetValue(currentEnumerator.Current, out T val)) return val;
                else return currentEnumerator.Current;
            }
        }

        void IDisposable.Dispose()
        {
            currentEnumerator.Dispose();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        bool IEnumerator.MoveNext()
        {
            return currentEnumerator.MoveNext();
        }

        void IEnumerator.Reset()
        {
            currentEnumerator = null;
        }
    }

    internal class SerializableHashSetString : HashSet<string>, IJsonSerialize
    {
        public SerializableHashSetString() : base() { }
        public SerializableHashSetString(IEnumerable<string> strings) : base(strings) { }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Strings", this.ToArray());
        }

        public void SetObjectData(IJsonReadData data)
        {
            string[] strings = data.GetProperty<string[]>("Strings");
            for (int i = 0; i < strings.Length; i++)
            {
                Add(strings[i]);
            }
        }
    }
    static partial class Extensions
    {
        public static IEnumerable<T> Combine<T>(params IEnumerable<T>[] enumerators)
        {
            return new CombinedEnumerable<T>(enumerators);
        }
        public static IEnumerable<T> LookUp<T>(IEnumerable<T> enumertor, Dictionary<T, T> lookUp)
        {
            return new LookedUpEnumerable<T>(enumertor, lookUp);
        }
        public static Matrix RowVector(params GeoVector[] v)
        {
            double[,] A = new double[3, v.Length];
            for (int i = 0; i < v.Length; ++i)
            {
                A[0, i] = v[i].x;
                A[1, i] = v[i].y;
                A[2, i] = v[i].z;
            }
            return DenseMatrix.OfArray(A);
        }
        public static Matrix ColumnVector(params GeoVector[] v1)
        {
            double[,] A = new double[v1.Length, 3];
            for (int i = 0; i < v1.Length; ++i)
            {
                A[i, 0] = v1[i].x;
                A[i, 1] = v1[i].y;
                A[i, 2] = v1[i].z;
            }
            return DenseMatrix.OfArray(A);
        }
        public static bool IsValid(this Matrix matrix)
        {
            return matrix.RowCount > 0 && !double.IsNaN(matrix[0, 0]) && !double.IsInfinity(matrix[0, 0]);
        }
        public static bool IsValid(this Vector v)
        {
            return v.Count > 0 && !double.IsNaN(v[0]) && !double.IsInfinity(v[0]);
        }

        public static T[] ToArray<T>(this IEnumerable<T> e)
        {
            if (e is T[] t) return t;
            if (e is List<T> l) return l.ToArray();
            List<T> r = new List<T>();
            foreach (T item in e) r.Add(item);
            return r.ToArray();
        }
    }
}
