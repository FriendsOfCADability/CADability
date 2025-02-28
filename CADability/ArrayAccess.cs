using System.Collections;
using System.Collections.Generic;

namespace CADability
{
    /// <summary>
    /// Interface to access a collections as an array. Mainly used for twodimensional array to access rows or columns as simple array
    /// without copying the contents. Or access a twodimensional array as a one dimensional array.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IArray<T> : IEnumerable<T>
    {
        T this[int index] { get; set; }
        int Length { get; }
        T First { get; set; }
        T Last { get; set; }
        T[] ToArray();
    }

    /// <summary>
    /// General implementation of <see cref="IArray{T}"/> providing enumerators and first and last properties.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class IArrayImpl<T> : IArray<T>
    {
        public abstract T this[int index] { get; set; }
        public abstract int Length { get; }

        public T First { get => (this as IArray<T>)[0]; set => (this as IArray<T>)[0] = value; }
        public T Last { get => (this as IArray<T>)[(this as IArray<T>).Length - 1]; set => (this as IArray<T>)[(this as IArray<T>).Length - 1] = value; }

        class EnumeratorView<TU> : IEnumerator<TU>
        {
            private readonly IArrayImpl<TU> a;
            private int ind;
            public EnumeratorView(IArrayImpl<TU> a)
            {
                this.a = a;
                ind = -1;
            }
            public TU Current => a[ind];

            object IEnumerator.Current => a[ind];

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                ++ind;
                return ind < a.Length;
            }

            public void Reset()
            {
                ind = -1;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new EnumeratorView<T>(this);
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new EnumeratorView<T>(this);
        }

        public T[] ToArray()
        {
            T[] res = new T[Length];
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = this[i];
            }
            return res;
        }
    }
    /// <summary>
    /// Convert a row of a 2 dimensional array to a one dimensional array (without copying the data)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ArrayRow<T> : IArrayImpl<T>
    {
        readonly int ind;
        T[,] a;

        public ArrayRow(T[,] a, int ind)
        {
            this.a = a;
            this.ind = ind;
        }

        public override T this[int index]
        {
            get
            {
                return a[ind, index];
            }
            set
            {
                a[ind, index] = value;
            }

        }
        public override int Length => a.GetLength(1);
    }

    /// <summary>
    /// Convert a column of a 2 dimensional array to a one dimensional array (without copying the data)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ArrayColumn<T> : IArrayImpl<T>
    {
        readonly int ind;
        T[,] a;

        public ArrayColumn(T[,] a, int ind)
        {
            this.a = a;
            this.ind = ind;
        }

        public override T this[int index]
        {
            get
            {
                return a[index, ind];
            }
            set
            {
                a[index, ind] = value;
            }

        }
        public override int Length => a.GetLength(0);
    }

    /// <summary>
    /// Convert a 2 dimensional array to a one dimensional array (without copying the data)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ArrayLinear<T> : IArrayImpl<T>
    {
        T[,] a;
        readonly int rc;
        public ArrayLinear(T[,] a)
        {
            this.a = a;
            rc = a.GetLength(1);
        }

        public override T this[int index]
        {
            get
            {
                return a[index / rc, index % rc];
            }
            set
            {
                a[index / rc, index % rc] = value;
            }

        }
        public override int Length => a.GetLength(0) * a.GetLength(1);
    }

    /// <summary>
    /// Convert a simple one dimensional array to an IArray
    /// Use someArray.ToIArray() 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ToIArray<T> : IArrayImpl<T>
    {
        T[] a;
        public ToIArray(T[] a)
        {
            this.a = a;
        }

        public override T this[int index]
        {
            get
            {
                return a[index];
            }
            set
            {
                a[index] = value;
            }

        }
        public override int Length => a.Length;
    }
    public class ListToIArray<T> : IArrayImpl<T>
    {
        List<T> a;
        public ListToIArray(List<T> a)
        {
            this.a = a;
        }

        public override T this[int index]
        {
            get
            {
                return a[index];
            }
            set
            {
                a[index] = value;
            }

        }
        public override int Length => a.Count;
    }
}
