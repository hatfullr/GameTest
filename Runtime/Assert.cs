using System.Collections.Generic;
using UnityEngine;

namespace GameTest
{
    /// <summary>
    /// The Assert class contains assertion methods for setting invariants in the code.
    /// Assert throws exceptions whenever an assertion fails.
    /// </summary>
    public static class Assert
    {
        public const float tolerance = 0.00001f;

        /// <summary>
        /// This gets set by the Test class. Do not modify.
        /// </summary>
        public static string currentTestSource = null;
        /// <summary>
        /// This gets set by the Test class. Do not modify.
        /// </summary>
        public static Object currentTestScript = null;

        /// <summary>
        /// Simply raise an AssertException with a message.
        /// </summary>
        /// <param name="message">The debug message to show in the console.</param>
        [HideInCallstack] public static void Fail(string message = "")
        {
            ThrowException(message);
        }

        /// <summary>
        /// Assert the values are approximately equal. An absolute error check is used for approximate equality check (|a-b| <= tolerance). 
        /// Default tolerance is 0.00001f.
        /// </summary>
        /// <param name="expected">The assumed Assert value.</param>
        /// <param name="actual">The exact Assert value.</param>
        /// <param name="tolerance">Tolerance of approximation.</param>
        /// <param name="message">The string used to describe the Assert.</param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the given tolerance value is negative.</exception>
        [HideInCallstack] public static void AreApproximatelyEqual(float expected, float actual, float tolerance, string message = "")
        {
            if (tolerance < 0) throw new System.ArgumentOutOfRangeException("tolerance argument cannot be < 0, but got " + tolerance);
            if (!(System.Math.Abs(expected - actual) <= tolerance))
            {
                string final = "|" + expected.ToString() + " - " + actual.ToString() + "| > " + tolerance.ToString();
                if (!string.IsNullOrEmpty(message)) final += ", " + message;
                ThrowException(final);
            }
        }
        [HideInCallstack] public static void AreApproximatelyEqual(float expected, float actual, string message) => AreApproximatelyEqual(expected, actual, tolerance, message);
        [HideInCallstack] public static void AreApproximatelyEqual(float expected, float actual) => AreApproximatelyEqual(expected, actual, tolerance, "");



        /// <summary>
        /// Assert the values are not approximately equal. An absolute error check is used for approximate equality check (not |a-b| <= tolerance). 
        /// Default tolerance is 0.00001f.
        /// </summary>
        /// <param name="expected">The assumed Assert value.</param>
        /// <param name="actual">The exact Assert value.</param>
        /// <param name="tolerance">Tolerance of approximation.</param>
        /// <param name="message">The string used to describe the Assert.</param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the given tolerance value is negative.</exception>
        [HideInCallstack] public static void AreNotApproximatelyEqual(float expected, float actual, float tolerance, string message = "")
        {
            if (tolerance < 0) throw new System.ArgumentOutOfRangeException("tolerance argument cannot be < 0, but got " + tolerance);
            if (System.Math.Abs(expected - actual) <= tolerance)
            {
                string final = "|" + expected.ToString() + " - " + actual.ToString() + "| <= " + tolerance.ToString();
                if (!string.IsNullOrEmpty(message)) final += ", " + message;
                ThrowException(final);
            }
        }
        [HideInCallstack] public static void AreNotApproximatelyEqual(float expected, float actual, string message) => AreNotApproximatelyEqual(expected, actual, tolerance, message);
        [HideInCallstack] public static void AreNotApproximatelyEqual(float expected, float actual) => AreNotApproximatelyEqual(expected, actual, tolerance, "");

        /// <summary>
        /// Assert that the values are equal.
        /// Shows a message when the expected and actual are not equal. If no comparer is specified, EqualityComparer<T>.Default is used.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expected">The assumed Assert value.</param>
        /// <param name="actual">The exact Assert value.</param>
        /// <param name="message">The string used to describe the Assert.</param>
        /// <param name="comparer">Method to compare expected and actual arguments have the same value.</param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        [HideInCallstack] public static void AreEqual<T>(T expected, T actual, string message, IEqualityComparer<T> comparer)
        {
            if (!comparer.Equals(expected, actual))
            {
                string final = expected.ToString() + " != " + actual.ToString();
                if (!string.IsNullOrEmpty(message)) final += ", " + message;
                ThrowException(final);
            }
        }
        [HideInCallstack] public static void AreEqual<T>(T expected, T actual, string message) => AreEqual(expected, actual, message, EqualityComparer<T>.Default);
        [HideInCallstack] public static void AreEqual<T>(T expected, T actual, IEqualityComparer<T> comparer) => AreEqual(expected, actual, "", comparer);
        [HideInCallstack] public static void AreEqual<T>(T expected, T actual) => AreEqual(expected, actual, "", EqualityComparer<T>.Default);

        /// <summary>
        /// Assert that the values are not equal.
        /// Shows a message when the expected and actual are equal. If no comparer is specified, EqualityComparer<T>.Default is used.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expected">The assumed Assert value.</param>
        /// <param name="actual">The exact Assert value.</param>
        /// <param name="message">The string used to describe the Assert.</param>
        /// <param name="comparer">Method to compare expected and actual arguments have the same value.</param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        [HideInCallstack] public static void AreNotEqual<T>(T expected, T actual, string message, IEqualityComparer<T> comparer)
        {
            if (comparer.Equals(expected, actual))
            {
                string final = expected.ToString() + " != " + actual.ToString();
                if (!string.IsNullOrEmpty(message)) final += ", " + message;
                ThrowException(message);
            }
        }
        [HideInCallstack] public static void AreNotEqual<T>(T expected, T actual, string message) => AreNotEqual(expected, actual, message, EqualityComparer<T>.Default);
        [HideInCallstack] public static void AreNotEqual<T>(T expected, T actual, IEqualityComparer<T> comparer) => AreNotEqual(expected, actual, "", comparer);
        [HideInCallstack] public static void AreNotEqual<T>(T expected, T actual) => AreNotEqual(expected, actual, "", EqualityComparer<T>.Default);

        /// <summary>
        /// Asserts that the condition is true.
        /// </summary>
        /// <param name="condition">true or false.</param>
        /// <param name="message">The string used to describe the Assert.</param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        [HideInCallstack] public static void IsTrue(bool condition, string message = "")
        {
            if (!condition) ThrowException(message);
        }


        /// <summary>
        /// Asserts that the condition is false.
        /// </summary>
        /// <param name="condition">true or false.</param>
        /// <param name="message">The string used to describe the Assert.</param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        [HideInCallstack] public static void IsFalse(bool condition, string message = "")
        {
            if (condition) ThrowException(message);
        }


        /// <summary>
        /// Assert the value is null.
        /// </summary>
        /// <param name="value">The Object or type being checked for.</param>
        /// <param name="message">The string used to describe the Assert.</param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        [HideInCallstack] public static void IsNull(Object value, string message = "")
        {
            if (value != null) ThrowException(message);
        }
        [HideInCallstack] public static void IsNull<T>(T value, string message = "")
        {
            if (value != null) ThrowException(message);
        }

        /// <summary>
        /// Assert that the value is not null.
        /// </summary>
        /// <param name="value">The Object or type being checked for.</param>
        /// <param name="message">The string used to describe the Assert.</param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        [HideInCallstack] public static void IsNotNull(Object value, string message = "")
        {
            if (value == null) ThrowException(message);
        }
        [HideInCallstack] public static void IsNotNull<T>(T value, string message = "")
        {
            if (value == null) ThrowException(message);
        }



        /// <summary>
        /// Assert the value is greater.
        /// </summary>
        /// <param name="value">The assumed Assert value.</param>
        /// <param name="other">The number that value is expected to be greater than.</param>
        /// <param name="message"></param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        [HideInCallstack] public static void IsGreater(float value, float other, string message = "")
        {
            if (!(value > other))
            {
                string final = value.ToString() + " <= " + other.ToString();
                if (!string.IsNullOrEmpty(message)) final += ", " + message;
                ThrowException(final);
            }
        }
        [HideInCallstack] public static void IsGreater(int value, float other, string message = "") => IsGreater((float)value, (float)other, message);
        [HideInCallstack] public static void IsGreater(int value, int other, string message = "") => IsGreater((float)value, (float)other, message);
        [HideInCallstack] public static void IsGreater(float value, int other, string message = "") => IsGreater((float)value, (float)other, message);




        /// <summary>
        /// Assert the value is greater or equal.
        /// </summary>
        /// <param name="value">The assumed Assert value.</param>
        /// <param name="other">The number that value is expected to be greater than or equal to.</param>
        /// <param name="message"></param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        [HideInCallstack] public static void IsGreaterEqual(float value, float other, string message = "")
        {
            if (!(value >= other))
            {
                string final = value.ToString() + " < " + other.ToString();
                if (!string.IsNullOrEmpty(message)) final += ", " + message;
                ThrowException(final);
            }
        }
        [HideInCallstack] public static void IsGreaterEqual(int value, float other, string message = "") => IsGreaterEqual((float)value, (float)other, message);
        [HideInCallstack] public static void IsGreaterEqual(int value, int other, string message = "") => IsGreaterEqual((float)value, (float)other, message);
        [HideInCallstack] public static void IsGreaterEqual(float value, int other, string message = "") => IsGreaterEqual((float)value, (float)other, message);




        /// <summary>
        /// Assert the value is greater or approximately equal. An absolute error check is used for approximate equality check (|a-b| < tolerance). 
        /// Default tolerance is 0.00001f.
        /// </summary>
        /// <param name="value">The assumed Assert value.</param>
        /// <param name="other">The number that value is expected to be greater than or equal to.</param>
        /// <param name="message"></param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the given tolerance value is negative.</exception>
        [HideInCallstack] public static void IsGreaterApproximatelyEqual(float value, float other, float tolerance, string message = "")
        {
            if (tolerance < 0) throw new System.ArgumentOutOfRangeException("tolerance argument cannot be < 0, but got " + tolerance);
            if (!(value >= other || System.Math.Abs(value - other) <= tolerance))
            {
                string final = value.ToString() + " < " + other.ToString() + " and " + 
                    "|" + value.ToString() + " - " + other.ToString() + "| >= " + tolerance.ToString();
                if (!string.IsNullOrEmpty(message)) final += ", " + message;
                ThrowException(final);
            }
        }
        [HideInCallstack] public static void IsGreaterApproximatelyEqual(float value, float other, string message) => IsGreaterApproximatelyEqual(value, other, tolerance, message);
        [HideInCallstack] public static void IsGreaterApproximatelyEqual(float value, float other) => IsGreaterApproximatelyEqual(value, other, tolerance);



        /// <summary>
        /// Assert the value is less.
        /// </summary>
        /// <param name="value">The assumed Assert value.</param>
        /// <param name="other">The number that value is expected to be less than.</param>
        /// <param name="message"></param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        [HideInCallstack] public static void IsLess(float value, float other, string message = "")
        {
            if (!(value < other))
            {
                string final = value.ToString() + " >= " + other.ToString();
                if (!string.IsNullOrEmpty(message)) final += ", " + message;
                ThrowException(final);
            }
        }
        [HideInCallstack] public static void IsLess(int value, float other, string message = "") => IsLess((float)value, (float)other, message);
        [HideInCallstack] public static void IsLess(int value, int other, string message = "") => IsLess((float)value, (float)other, message);
        [HideInCallstack] public static void IsLess(float value, int other, string message = "") => IsLess((float)value, (float)other, message);


        /// <summary>
        /// Assert the value is less or equal.
        /// </summary>
        /// <param name="value">The assumed Assert value.</param>
        /// <param name="other">The number that value is expected to be less than or equal to.</param>
        /// <param name="message"></param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        [HideInCallstack] public static void IsLessEqual(float value, float other, string message = "")
        {
            if (!(value <= other))
            {
                string final = value.ToString() + " > " + other.ToString();
                if (!string.IsNullOrEmpty(message)) final += ", " + message;
                ThrowException(final);
            }
        }
        [HideInCallstack] public static void IsLessEqual(int value, float other, string message = "") => IsLessEqual((float)value, (float)other, message);
        [HideInCallstack] public static void IsLessEqual(int value, int other, string message = "") => IsLessEqual((float)value, (float)other, message);
        [HideInCallstack] public static void IsLessEqual(float value, int other, string message = "") => IsLessEqual((float)value, (float)other, message);




        /// <summary>
        /// Assert the value is less or approximately equal. An absolute error check is used for approximate equality check (|a-b| < tolerance). 
        /// Default tolerance is 0.00001f.
        /// </summary>
        /// <param name="value">The assumed Assert value.</param>
        /// <param name="other">The number that value is expected to be less than or equal to.</param>
        /// <param name="message"></param>
        /// <exception cref="AssertionException">Thrown when the assertion fails.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the given tolerance value is negative.</exception>
        [HideInCallstack]
        public static void IsLessApproximatelyEqual(float value, float other, float tolerance, string message = "")
        {
            if (tolerance < 0) throw new System.ArgumentOutOfRangeException("tolerance argument cannot be < 0, but got " + tolerance);
            if (!(value <= other || System.Math.Abs(value - other) <= tolerance))
            {
                string final = value.ToString() + " > " + other.ToString() + " and " +
                    "|" + value.ToString() + " - " + other.ToString() + "| >= " + tolerance.ToString();
                if (!string.IsNullOrEmpty(message)) final += ", " + message;
                ThrowException(final);
            }
        }
        [HideInCallstack] public static void IsLessApproximatelyEqual(float value, float other, string message) => IsLessApproximatelyEqual(value, other, tolerance, message);
        [HideInCallstack] public static void IsLessApproximatelyEqual(float value, float other) => IsLessApproximatelyEqual(value, other, tolerance);

        /// <summary>
        /// Throw an exception to the UnityEditor.
        /// 
        /// IF YOU ARE READING THIS: You may have double-clicked on a Debug message in the console. If so, you were brought here by
        /// Unity's untouchable internal processes. You will need to locate your test script in the stack trace manually. To find your script easier,
        /// click the triple-dot toolbar button at the top-right of the Console window and choose "Strip logging callstack". You will still be redirected
        /// here on double-click, but it should at least be easier to locate your scripts.
        /// </summary>
        [HideInCallstack]
        private static void ThrowException(string message)
        {
            AssertionException e = new AssertionException(message);
            if (!string.IsNullOrEmpty(currentTestSource)) e.Source = currentTestSource;
            if (currentTestScript != null) Logger.LogException(e, currentTestScript);
            else Logger.LogException(e);
        }


        public class AssertionException : System.Exception
        {
            public AssertionException() { }
            public AssertionException(string message) : base(message) { }
            public AssertionException(string message, System.Exception inner) : base(message, inner) { }
        }
    }
}