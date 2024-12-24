using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameTest;

namespace GameTest
{
    public class AssertionTests : MonoBehaviour
    {
        #region Test classes
        /// <summary>
        /// Comparing an instance of this class to another instance of this class results in true. Comparing to null results in false.
        /// </summary>
        class AreEqualTestClass
        {
            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                return obj.GetType() == typeof(AreEqualTestClass);
            }
            public override int GetHashCode() => base.GetHashCode();
        }

        /// <summary>
        /// Comparing an instance of this class to another instance of this class or to null results in false. Comparing to anything else results in true.
        /// </summary>
        class AreNotEqualTestClass
        {
            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                if (obj.GetType() == typeof(AreNotEqualTestClass)) return false;
                return true;
            }
            public override int GetHashCode() => base.GetHashCode();
        }
        #endregion

        [Test]
        void ApproximatelyEqual(GameObject go)
        {
            Assert.AreApproximatelyEqual(0f, 0f, "message");
            Assert.AreApproximatelyEqual(0f, 0f, 0f, "message");

            Assert.AreApproximatelyEqual(0f, 0f, 0f);
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(0f, 0f, 0f);

            Assert.AreApproximatelyEqual(0f, 0f);
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(0f, 0f, Assert.tolerance);

            Assert.AreApproximatelyEqual(1f, 1f);
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(1f, 1f, Assert.tolerance);

            Assert.AreApproximatelyEqual(-1f, -1f);
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(-1f, -1f, Assert.tolerance);

            Assert.AreApproximatelyEqual(0f, 1f, 1f);
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(0f, 1f, 1f);

            Assert.AreApproximatelyEqual(1f, 0f, 1f);
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(1f, 0f, 1f);

            Assert.AreApproximatelyEqual(0f, -1f, 1f);
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(0f, -1f, 1f);

            Assert.AreApproximatelyEqual(-1f, 0f, 1f);
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(-1f, 0f, 1f);

            Assert.AreApproximatelyEqual(0f, 0.00001f, 0.00001f);
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(0f, 0.00001f, 0.00001f);

            Assert.AreApproximatelyEqual(Assert.tolerance, 0f);
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(Assert.tolerance, 0f, Assert.tolerance);

            Assert.AreApproximatelyEqual(float.MaxValue, 0f, float.MaxValue);
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(float.MaxValue, 0f, float.MaxValue);

            Assert.AreApproximatelyEqual(float.Epsilon, 0f, float.Epsilon);
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(float.Epsilon, 0f, float.Epsilon);


            Assert.AreNotApproximatelyEqual(0f, 1f, "message");
            Assert.AreNotApproximatelyEqual(0f, 1f, 0f, "message");

            Assert.AreNotApproximatelyEqual(0f, 1f);
            UnityEngine.Assertions.Assert.AreNotApproximatelyEqual(0f, 1f, Assert.tolerance);

            Assert.AreNotApproximatelyEqual(1f, 0f);
            UnityEngine.Assertions.Assert.AreNotApproximatelyEqual(1f, 0f, Assert.tolerance);

            Assert.AreNotApproximatelyEqual(0f, -1f);
            UnityEngine.Assertions.Assert.AreNotApproximatelyEqual(0f, -1f, Assert.tolerance);

            Assert.AreNotApproximatelyEqual(-1f, 0f);
            UnityEngine.Assertions.Assert.AreNotApproximatelyEqual(-1f, 0f, Assert.tolerance);

            Assert.AreNotApproximatelyEqual(float.PositiveInfinity, float.PositiveInfinity);
            UnityEngine.Assertions.Assert.AreNotApproximatelyEqual(float.PositiveInfinity, float.PositiveInfinity);

            Assert.AreNotApproximatelyEqual(float.NegativeInfinity, float.NegativeInfinity);
            UnityEngine.Assertions.Assert.AreNotApproximatelyEqual(float.NegativeInfinity, float.NegativeInfinity);
        }

        [Test]
        void Equal(GameObject go)
        {
            Assert.AreEqual(0, 0, "message");

            Assert.AreEqual(0x00, 0x00);
            UnityEngine.Assertions.Assert.AreEqual(0x00, 0x00);

            Assert.AreEqual('a', 'a');
            UnityEngine.Assertions.Assert.AreEqual('a', 'a');

            Assert.AreEqual(0, 0);
            UnityEngine.Assertions.Assert.AreEqual(0, 0);

            Assert.AreEqual(0L, 0L);
            UnityEngine.Assertions.Assert.AreEqual(0L, 0L);

            sbyte sbytea = 0, sbyteb = 0;
            Assert.AreEqual(sbytea, sbyteb);
            UnityEngine.Assertions.Assert.AreEqual(sbytea, sbyteb);

            short shorta = 0, shortb = 0;
            Assert.AreEqual(shorta, shortb);
            UnityEngine.Assertions.Assert.AreEqual(shorta, shortb);

            Assert.AreEqual(0U, 0U);
            UnityEngine.Assertions.Assert.AreEqual(0U, 0U);

            Assert.AreEqual(0UL, 0UL);
            UnityEngine.Assertions.Assert.AreEqual(0UL, 0UL);

            ushort ushorta = 0, ushortb = 0;
            Assert.AreEqual(ushorta, ushortb);
            UnityEngine.Assertions.Assert.AreEqual(ushorta, ushortb);

            GameObject objecta = new GameObject("Object a");
            Assert.AreEqual(objecta, objecta);
            UnityEngine.Assertions.Assert.AreEqual(objecta, objecta);

            Assert.AreEqual(0f, 0f);
            UnityEngine.Assertions.Assert.AreEqual(0f, 0f);

            Assert.AreEqual(true, true);
            UnityEngine.Assertions.Assert.AreEqual(true, true);

            Assert.AreEqual("a", "a");
            UnityEngine.Assertions.Assert.AreEqual("a", "a");

            Assert.AreEqual(new AreEqualTestClass(), new AreEqualTestClass());
            UnityEngine.Assertions.Assert.AreEqual(new AreEqualTestClass(), new AreEqualTestClass());


            Assert.AreNotEqual(0x00, 0x01);
            UnityEngine.Assertions.Assert.AreNotEqual(0x00, 0x01);

            Assert.AreNotEqual('a', 'b');
            UnityEngine.Assertions.Assert.AreNotEqual('a', 'b');

            Assert.AreNotEqual(0, 1);
            UnityEngine.Assertions.Assert.AreNotEqual(0, 1);

            Assert.AreNotEqual(0L, 1L);
            UnityEngine.Assertions.Assert.AreNotEqual(0L, 1L);

            sbytea = 0; sbyteb = 1;
            Assert.AreNotEqual(sbytea, sbyteb);
            UnityEngine.Assertions.Assert.AreNotEqual(sbytea, sbyteb);

            shorta = 0; shortb = 1;
            Assert.AreNotEqual(shorta, shortb);
            UnityEngine.Assertions.Assert.AreNotEqual(shorta, shortb);

            Assert.AreNotEqual(0U, 1U);
            UnityEngine.Assertions.Assert.AreNotEqual(0U, 1U);

            Assert.AreNotEqual(0UL, 1UL);
            UnityEngine.Assertions.Assert.AreNotEqual(0UL, 1UL);

            ushorta = 0; ushortb = 1;
            Assert.AreNotEqual(ushorta, ushortb);
            UnityEngine.Assertions.Assert.AreNotEqual(ushorta, ushortb);

            GameObject objectb = new GameObject("Object b");
            Assert.AreNotEqual(objecta, objectb);
            UnityEngine.Assertions.Assert.AreNotEqual(objecta, objectb);

            Assert.AreNotEqual(0f, 1f);
            UnityEngine.Assertions.Assert.AreNotEqual(0f, 1f);

            Assert.AreNotEqual(true, false);
            UnityEngine.Assertions.Assert.AreNotEqual(true, false);

            Assert.AreNotEqual("a", "b");
            UnityEngine.Assertions.Assert.AreNotEqual("a", "b");

            Assert.AreNotEqual(new AreNotEqualTestClass(), new AreNotEqualTestClass());
            UnityEngine.Assertions.Assert.AreNotEqual(new AreNotEqualTestClass(), new AreNotEqualTestClass());

            DestroyImmediate(objecta);
            DestroyImmediate(objectb);
        }

        [Test]
        void IsTrue(GameObject go)
        {
            Assert.IsTrue(true, "message");
            Assert.IsTrue(true);
            UnityEngine.Assertions.Assert.IsTrue(true);
        }

        [Test]
        void IsFalse(GameObject go)
        {
            Assert.IsFalse(false, "message");
            Assert.IsFalse(false);
            UnityEngine.Assertions.Assert.IsFalse(false);
        }

        [Test]
        void IsNull(GameObject go)
        {
            Object test = null;
            Assert.IsNull(test, "message");
            Assert.IsNull(test);
            UnityEngine.Assertions.Assert.IsNull(test);
        }

        [Test]
        void IsNotNull(GameObject go)
        {
            GameObject test = new GameObject();
            Assert.IsNotNull(test, "message");
            Assert.IsNotNull(test);
            UnityEngine.Assertions.Assert.IsNotNull(test);
            DestroyImmediate(test);
        }

        [Test]
        void IsGreater(GameObject go)
        {
            Assert.IsGreater(1f, 0f, "message");
            Assert.IsGreater(1f, 0f);
            Assert.IsGreater(0f, -1f);
            Assert.IsGreater(1, 0);
            Assert.IsGreater(0, -1);
        }

        [Test]
        void IsGreaterEqual(GameObject go)
        {
            Assert.IsGreaterEqual(1f, 0f, "message");
            Assert.IsGreaterEqual(1f, 0f);
            Assert.IsGreaterEqual(0f, -1f);
            Assert.IsGreaterEqual(1, 0);
            Assert.IsGreaterEqual(0, -1);

            Assert.IsGreaterEqual(1f, 1f);
            Assert.IsGreaterEqual(-1f, -1f);
            Assert.IsGreaterEqual(1, 1);
            Assert.IsGreaterEqual(-1, -1);
        }

        [Test]
        void IsGreaterApproximatelyEqual(GameObject go)
        {
            Assert.IsGreaterApproximatelyEqual(1f, 0f, "message");
            Assert.IsGreaterApproximatelyEqual(1f, 0f, 0f, "message");

            Assert.IsGreaterApproximatelyEqual(1f, 0f);
            Assert.IsGreaterApproximatelyEqual(0f, -1f);
            Assert.IsGreaterApproximatelyEqual(1, 0);
            Assert.IsGreaterApproximatelyEqual(0, -1);

            Assert.IsGreaterApproximatelyEqual(1f, 1f);
            Assert.IsGreaterApproximatelyEqual(-1f, -1f);
            Assert.IsGreaterApproximatelyEqual(1, 1);
            Assert.IsGreaterApproximatelyEqual(-1, -1);

            Assert.IsGreaterApproximatelyEqual(0f, 0f, 0f);
            Assert.IsGreaterApproximatelyEqual(0f, 0f);
            Assert.IsGreaterApproximatelyEqual(1f, 1f);
            Assert.IsGreaterApproximatelyEqual(-1f, -1f);
            Assert.IsGreaterApproximatelyEqual(0f, 1f, 1f);
            Assert.IsGreaterApproximatelyEqual(1f, 0f, 1f);
            Assert.IsGreaterApproximatelyEqual(0f, -1f, 1f);
            Assert.IsGreaterApproximatelyEqual(-1f, 0f, 1f);
            Assert.IsGreaterApproximatelyEqual(0f, 0.00001f, 0.00001f);
            Assert.IsGreaterApproximatelyEqual(Assert.tolerance, 0f);
            Assert.IsGreaterApproximatelyEqual(float.MaxValue, 0f, float.MaxValue);
            Assert.IsGreaterApproximatelyEqual(float.Epsilon, 0f, float.Epsilon);
        }

        [Test]
        void IsLess(GameObject go)
        {
            Assert.IsLess(-1f, 0f, "message");

            Assert.IsLess(-1f, 0f);
            Assert.IsLess(0f, 1f);
            Assert.IsLess(-1, 0);
            Assert.IsLess(0, 1);
        }

        [Test]
        void IsLessEqual(GameObject go)
        {
            Assert.IsLessEqual(-1f, 0f, "message");

            Assert.IsLessEqual(-1f, 0f);
            Assert.IsLessEqual(0f, 1f);
            Assert.IsLessEqual(-1, 0);
            Assert.IsLessEqual(0, 1);

            Assert.IsLessEqual(1f, 1f);
            Assert.IsLessEqual(-1f, -1f);
            Assert.IsLessEqual(1, 1);
            Assert.IsLessEqual(-1, -1);
        }

        [Test]
        void IsLessApproximatelyEqual(GameObject go)
        {
            Assert.IsLessApproximatelyEqual(-1f, 0f, "message");
            Assert.IsLessApproximatelyEqual(-1f, 0f, 0f, "message");

            Assert.IsLessApproximatelyEqual(-1f, 0f);
            Assert.IsLessApproximatelyEqual(0f, 1f);
            Assert.IsLessApproximatelyEqual(0, 1);
            Assert.IsLessApproximatelyEqual(-1, 0);
            
            Assert.IsLessApproximatelyEqual(1f, 1f);
            Assert.IsLessApproximatelyEqual(-1f, -1f);
            Assert.IsLessApproximatelyEqual(1, 1);
            Assert.IsLessApproximatelyEqual(-1, -1);
            
            Assert.IsLessApproximatelyEqual(0f, 0f, 0f);
            Assert.IsLessApproximatelyEqual(0f, 0f);
            Assert.IsLessApproximatelyEqual(1f, 1f);
            Assert.IsLessApproximatelyEqual(-1f, -1f);
            Assert.IsLessApproximatelyEqual(0f, 1f, 1f);
            Assert.IsLessApproximatelyEqual(1f, 0f, 1f);
            Assert.IsLessApproximatelyEqual(0f, -1f, 1f);
            Assert.IsLessApproximatelyEqual(-1f, 0f, 1f);
            Assert.IsLessApproximatelyEqual(0f, 0.00001f, 0.00001f);
            Assert.IsLessApproximatelyEqual(Assert.tolerance, 0f);
            Assert.IsLessApproximatelyEqual(float.MaxValue, 0f, float.MaxValue);
            Assert.IsLessApproximatelyEqual(float.Epsilon, 0f, float.Epsilon);
        }
    }
}