using System.Collections.Generic;

namespace UnityTest
{
    public class Queue : System.Collections.Queue
    {
        public List<Test> tests = new List<Test>();

        public Dictionary<Test, Test.Result> results = new Dictionary<Test, Test.Result>();

        public Queue(Test[] tests = null)
        {
            if (tests == null) tests = new Test[0];
            foreach (Test test in tests) Enqueue(test);
            this.tests = new List<Test>(tests);
        }

        /// <summary>
        /// Add a Test to the queue. The given test must be a Test object.
        /// </summary>
        public new void Enqueue(object test)
        {
            base.Enqueue(test);
            tests.Add(test as Test);
        }

        /// <summary>
        /// Add a Test to the queue and mark its result as the given result. Tests that are retrieved from this queue will have
        /// their results overwritten by the given result.
        /// </summary>
        public void Enqueue(object test, Test.Result result)
        {
            Enqueue(test);
            results.Add(test as Test, result);
        }

        public new Test Dequeue()
        {
            if (results.ContainsKey(tests[0])) results.Remove(tests[0]);
            tests.RemoveAt(0);
            return base.Dequeue() as Test;
        }

        public new void Clear()
        {
            base.Clear();
            tests.Clear();
            results.Clear();
        }

        public void Remove(Test test)
        {
            Queue newQueue = new Queue();
            while (Count > 0)
            {
                Test t = base.Dequeue() as Test;
                if (t.attribute == test.attribute) continue;
                if (results.ContainsKey(t)) newQueue.Enqueue(t, results[t]);
                else newQueue.Enqueue(t);
            }
            while (newQueue.Count > 0) base.Enqueue(newQueue.Dequeue());
            tests.Remove(test);
            if (results.ContainsKey(test)) results.Remove(test);
        }

        public string GetString()
        {
            string[] strings = new string[Count];
            for (int i = 0; i < Count; i++)
            {
                strings[i] = tests[i].attribute.GetPath();
            }
            string ret = string.Join('\n', strings);
            if (results.Count > 0)
            {
                string[] s = new string[results.Count];
                List<Test> keys = new List<Test>(results.Keys);
                List<Test.Result> values = new List<Test.Result>(results.Values);
                for (int i = 0; i < results.Count; i++)
                {
                    s[i] = string.Join(',', keys[i].attribute.GetPath(), (int)values[i]);
                }
                ret = string.Join("\n\n", ret, string.Join('\n', s));
            }
            return ret;
        }

        public static Queue FromString(string data)
        {
            Queue queue = new Queue();
            if (string.IsNullOrEmpty(data)) return queue;

            string testData = data;
            string resultsData;
            if (data.Contains("\n\n"))
            {
                string[] split = data.Split("\n\n");
                testData = split[0];
                resultsData = split[1];

                string[] ssplit;
                foreach (string s in resultsData.Split('\n'))
                {
                    ssplit = s.Split(',');
                    foreach (Test test in TestManager.tests.Values)
                    {
                        if (test.attribute.GetPath() != ssplit[0]) continue;
                        queue.results.Add(test, (Test.Result)int.Parse(ssplit[1]));
                    }
                }
            }

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

        public Queue Reversed()
        {
            Queue newQueue = new Queue();
            for (int i = Count - 1; i >= 0; i--)
            {
                if (results.ContainsKey(tests[i])) newQueue.Enqueue(tests[i], results[tests[i]]);
                else newQueue.Enqueue(tests[i]);
            }
            return newQueue;
        }
    }
}