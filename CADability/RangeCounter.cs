using System;
using System.Collections.Generic;
namespace CADability
{
    /// <summary>
    /// Eine Art Dictionary double->int, wobei das double unscharf ist, also ein Intervall mit maximaler Breite
    /// </summary>
    internal class RangeCounter : IEnumerable<KeyValuePair<double, int>>, IEnumerator<KeyValuePair<double, int>>
    {
        class interval : IComparable
        {
            public double min;
            public double max;

            public interval(double val)
            {
                min = max = val;
            }
            public bool acceptAndExpand(double val, double precision)
            {
                if (val >= min && val <= max) return true;
                if (val < min && max - val < precision)
                {
                    min = val;
                    return true;
                }
                if (val > max && val - min < precision)
                {
                    max = val;
                    return true;
                }
                return false;
            }
            public void adjustPeriodic(ref double val, double period, double precision)
            {
                if (period == 0.0) return;
                while (val < min - period + precision) val += period;
                while (val > max + period - precision) val -= period;
            }
            public bool accept(double val, double precision)
            {
                if (val > max - precision && val < min + precision) return true;
                return false;
            }
            public void expand(double val) // muss erlaubt eins
            {
                if (val < min)
                {
                    min = val;
                }
                if (val > max)
                {
                    max = val;
                }
            }
            int IComparable.CompareTo(object obj)
            {
                interval other = obj as interval;
                if (other != null)
                {   // muss ja immer sein
                    // die Intervalle sind immer nicht überlappend
                    return min.CompareTo(other.min);
                }
                return -1;
            }
        }

        double precision; // maximale Breite des Intervalls
        double period; // 0.0, wenn nicht periodisch, sonst die Periode
        SortedDictionary<interval, int> dictionary;
        private List<interval> currentKeys; // für den Enumerator. Es darf immer nur einer laufen, sonst müsste man eine eigene Klasse dafür machen
        private int currentIndex;

        public RangeCounter(double precision, double period)
        {
            this.precision = precision;
            this.period = period;
            dictionary = new SortedDictionary<interval, int>();
        }

        public void Add(double val)
        {
            interval lower = new interval(val - precision);
            interval upper = new interval(val + precision);
            List<interval> keysInRange = new List<interval>();
            foreach (interval k in dictionary.Keys)
            {
                if (((IComparable)k).CompareTo(lower) >= 0 && ((IComparable)k).CompareTo(upper) <= 0)
                    keysInRange.Add(k);
                else if (((IComparable)k).CompareTo(upper) > 0)
                    break;
            }
            foreach (interval intv in keysInRange)
            {
                intv.adjustPeriodic(ref val, period, precision);
                if (intv.accept(val, precision))
                {
                    int count = dictionary[intv];
                    dictionary.Remove(intv);
                    intv.expand(val);
                    dictionary[intv] = count + 1;
                    return; // fertig
                }
            }
            // es wurde nicht zugefügt
            dictionary[new interval(val)] = 1;
        }


        IEnumerator<KeyValuePair<double, int>> IEnumerable<KeyValuePair<double, int>>.GetEnumerator()
        {
            currentKeys = new List<interval>(dictionary.Keys);
            currentIndex = -1; // beginnt mit MoveNext
            return this;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            currentKeys = new List<interval>(dictionary.Keys);
            currentIndex = -1;
            return this;
        }

        KeyValuePair<double, int> IEnumerator<KeyValuePair<double, int>>.Current
        {
            get
            {
                return new KeyValuePair<double, int>((currentKeys[currentIndex].min + currentKeys[currentIndex].max) / 2, dictionary[currentKeys[currentIndex]]);
            }
        }

        void IDisposable.Dispose()
        {
            currentKeys = null;
            currentIndex = -1;
        }

        object System.Collections.IEnumerator.Current
        {
            get { return (this as IEnumerator<KeyValuePair<double, int>>).Current; }
        }

        bool System.Collections.IEnumerator.MoveNext()
        {
            ++currentIndex;
            return currentIndex < currentKeys.Count;
        }

        void System.Collections.IEnumerator.Reset()
        {
            currentIndex = 0;
        }

        internal bool isOk()
        {
            List<interval> keys = new List<interval>(dictionary.Keys);
            for (int i = 0; i < keys.Count - 1; i++)
            {
                if (keys[i].max + precision / 2.0 >= keys[i + 1].min) return false; // überlappende Intervalle, mindestens halber precision Abstand ist nötig
            }
            return true;
        }
    }
}
