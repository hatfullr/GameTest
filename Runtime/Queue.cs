using System.Collections;
using System.Collections.Generic;

namespace UnityTest
{
    public class Queue : System.Collections.Queue
    {
        private const string delimiter = ".:!Queue!:."; // Some unique identifier

        public List<Test> tests { get; private set; } = new List<Test>();

        public Queue(Test[] tests)
        {
            foreach (Test test in tests) Enqueue(test);
            this.tests = new List<Test>(tests);
        }

        public Queue(string data = null)
        {
            if (!string.IsNullOrEmpty(data))
            {
                foreach (string d in data.Split(delimiter))
                {
                    Enqueue(Test.FromString(d));
                }
            }
        }

        /// <summary>
        /// Add a test to the queue. The given test must be a Test object.
        /// </summary>
        public new void Enqueue(object test)
        {
            base.Enqueue(test);
            tests.Add(test as Test);
        }

        public new Test Dequeue()
        {
            tests.RemoveAt(0);
            return base.Dequeue() as Test;
        }

        public new void Clear()
        {
            base.Clear();
            tests.Clear();
        }

        /// <summary>
        /// For saving.
        /// </summary>
        public string GetString()
        {
            string[] strings = new string[tests.Count];
            for (int i = 0; i < tests.Count; i++)
            {
                strings[i] = tests[i].GetString();
            }
            return string.Join(delimiter, strings);
        }

        /// <summary>
        /// Make this Queue and the other Queue equivalent.
        /// </summary>
        public void Sync(Queue other)
        {
            for (int i = 0; i < Count; i++) Dequeue();
            foreach (Test test in other.tests) Enqueue(test);
        }


        public void Remove(Test test)
        {
            Queue newQueue = new Queue();
            while (Count > 0)
            {
                Test t = base.Dequeue() as Test;
                if (t == test) continue;
                newQueue.Enqueue(t);
            }
            while (newQueue.Count > 0) base.Enqueue(newQueue.Dequeue());
            tests.Remove(test);
        }

        public void RemoveAt(int index)
        {
            Queue newQueue = new Queue();
            for (int i = 0; i < Count; i++)
            {
                Test t = base.Dequeue() as Test;
                if (i == index) continue;
                newQueue.Enqueue(t);
            }
            while (newQueue.Count > 0) base.Enqueue(newQueue.Dequeue());
            tests.RemoveAt(index);
        }
    }
}