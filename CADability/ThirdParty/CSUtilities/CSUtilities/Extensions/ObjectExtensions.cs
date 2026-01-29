using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace CSUtilities.Extensions
{
#if PUBLIC
	public
#else
	internal
#endif
	static class ObjectExtensions
	{
		public delegate bool Check<T>(T obj);

		public static void ThrowIfNull(this object parameter)
		{
			if (parameter != null)
				return;

			throw new System.ArgumentNullException();
		}

		public static void ThrowIfNull(this object parameter, string paramName)
		{
			if (parameter != null)
				return;

			throw new System.ArgumentNullException(paramName);
		}

		public static void ThrowIfNull(this object parameter, string paramName, string message)
		{
			if (parameter != null)
				return;

			throw new System.ArgumentNullException(paramName, message);
		}

		public static void ThrowIf<T, E>(this T parameter, Check<T> check)
			where E : Exception, new()
		{
			if (check(parameter))
			{
				throw new E();
			}
		}

		public static void ThrowIf<T, E>(this T parameter, Check<T> check, string message)
			where E : Exception, new()
		{
			if (check(parameter))
			{
				throw Activator.CreateInstance(typeof(E), message) as E;
			}
		}

		public static void GreaterThan<T>(this T value, T min, bool inclusive = true, [CallerMemberName] string name = null)
			where T : struct, IComparable<T>
		{
			var down = value.CompareTo(min);

			if (down <= -1 || (!inclusive && down == 0))
			{
				throw new ArgumentOutOfRangeException(name, value, $"{name} valid values are from {min}.");
			}
		}

		public static void InRange<T>(this T value, T min, T max, bool inclusive = true, [CallerMemberName] string name = null)
			where T : struct, IComparable<T>
		{
			InRange(value, min, max, $"{name} valid values are from {min} to {max}.", inclusive, name);
		}

		public static void InRange<T>(this T value, T min, T max, string message, bool inclusive = true, [CallerMemberName] string name = null)
			where T : struct, IComparable<T>
		{
			var up = value.CompareTo(max);
			var down = value.CompareTo(min);
			bool error = !inclusive && (up == 0 || down == 0);

			if (up >= 1 || down <= -1 || error)
			{
				throw new ArgumentOutOfRangeException(name, value, message);
			}
		}
	}
}
