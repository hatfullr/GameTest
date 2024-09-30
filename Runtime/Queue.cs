using System.Collections;
using System.Collections.Generic;

namespace UnityTest
{
    public class Queue : System.Collections.Queue
    {
        public List<Test> tests { get; private set; } = new List<Test>();

        public Queue(Test[] tests = null)
        {
            if (tests == null) tests = new Test[0];
            foreach (Test test in tests) Enqueue(test);
            this.tests = new List<Test>(tests);
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
                if (t.attribute == test.attribute) continue;
                newQueue.Enqueue(t);
            }
            while (newQueue.Count > 0) base.Enqueue(newQueue.Dequeue());
            tests.Remove(test);
        }

        public string GetString()
        {
            string[] strings = new string[Count];
            for (int i = 0; i < Count; i++)
            {
                strings[i] = tests[i].attribute.GetPath();
            }
            return string.Join('\n', strings);
        }

        public static Queue FromString(string data)
        {
            Queue queue = new Queue();
            if (string.IsNullOrEmpty(data)) return queue;

            string[] strings = data.Split('\n');
            foreach (string s in strings)
            {
                foreach (Test test in TestManager.tests.Values)
                {
                    if (test.attribute.GetPath() != s) continue;
                    queue.Enqueue(test);
                    break;
                }
            }
            return queue;
        }
    }
}