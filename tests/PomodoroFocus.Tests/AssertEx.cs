using System;

namespace PomodoroFocus.Tests
{
    public sealed class TestAssertionException : Exception
    {
        public TestAssertionException(string message) : base(message)
        {
        }
    }

    public static class AssertEx
    {
        public static void IsTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new TestAssertionException(message ?? "Expected true.");
            }
        }

        public static void IsFalse(bool condition, string message)
        {
            if (condition)
            {
                throw new TestAssertionException(message ?? "Expected false.");
            }
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new TestAssertionException(string.Format(
                    "{0} Expected: <{1}>, Actual: <{2}>",
                    message ?? "Values not equal.",
                    expected,
                    actual));
            }
        }

        public static void NotNull(object obj, string message)
        {
            if (obj == null)
            {
                throw new TestAssertionException(message ?? "Expected non-null.");
            }
        }

        public static void IsNull(object obj, string message)
        {
            if (obj != null)
            {
                throw new TestAssertionException(message ?? "Expected null.");
            }
        }

        public static void InRange(double value, double min, double max, string message)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
            {
                throw new TestAssertionException(string.Format(
                    "{0} Value <{1:F5}> not in [{2:F5}, {3:F5}].",
                    message ?? "Out of range.",
                    value,
                    min,
                    max));
            }
        }
    }
}
