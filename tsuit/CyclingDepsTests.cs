using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Extensions;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using rx = System.Text.RegularExpressions.Regex;

namespace tsuit
{
    [TestClass]
    public class CyclicDepsTests
    {
        [TestMethod]
        public void FindCycles_CyclesFound()
        {
            var cases = new Dictionary<string, string>()
            {
                {"a->b;b->a", "a.b.a"},
                {"a->b;b->c;c->a", "a.b.c.a"},
                {"a->b;b->c,d;c->d;d->a", "a.b.c.d.a"},
                {"a->b;b->c,d;d->a,e,f", "a.b.d.a" },
            };

            foreach (var test in cases)
            {
                var graphString = test.Key;
                var expectedPath = test.Value;

                var graph = ParseGraph(graphString);
                var cycles = graph.FindCycles();
                Assert.IsTrue(CompareCycle<string>(cycles, expectedPath));
            }
        }

        [TestMethod]
        public void ParseGraph_MultiLine_Success()
        {
            var s = @"
a->b;
b->c,d,e;
d->f,g;g->h;h->i,j,k;
";
            var graph = ParseGraph(s);

            var expected = new Dictionary<string, IEnumerable<string>>();
            AddGraph<string>(expected, "a", "b");
            AddGraph<string>(expected, "b", "c","d","e");
            AddGraph<string>(expected, "d", "f", "g");
            AddGraph<string>(expected, "g", "h");
            AddGraph<string>(expected, "h", "i", "j", "k");

            bool isEqual = CompareDictionaries(graph, expected);
            Assert.IsTrue(isEqual);
        }

        [TestMethod]
        public void CompareDictionaries_Identicals_Equals()
        {
            var d1 = new Dictionary<string, IEnumerable<string>>();
            AddGraph<string>(d1, "a", "b");
            AddGraph<string>(d1, "b", "c", "d", "e");
            AddGraph<string>(d1, "d", "f", "g");
            AddGraph<string>(d1, "g", "h");
            AddGraph<string>(d1, "h", "i", "j", "k");

            var d2 = new Dictionary<string, IEnumerable<string>>(d1);
            bool isEqual = CompareDictionaries(d1, d2);
            Assert.IsTrue(isEqual);
        }

        [TestMethod]
        public void CompareDictionaries_Diffrent_NotEquals()
        {
            var d1 = new Dictionary<string, IEnumerable<string>>();
            AddGraph<string>(d1, "a", "b");
            AddGraph<string>(d1, "b", "c", "d", "e");
            AddGraph<string>(d1, "d", "f", "g");
            AddGraph<string>(d1, "g", "h");
            AddGraph<string>(d1, "h", "i", "j", "k");

            var d2 = new Dictionary<string, IEnumerable<string>>(d1);
            d2["b"] = (new string[] {"x", "y"}).ToList();

            bool isEqual = CompareDictionaries(d1, d2);
            Assert.IsFalse(isEqual);
        }

        [TestMethod]
        public void FindCycles_EmptyGraph_NoCycle()
        {
            var graph = new Dictionary<string, List<string>>();
            var result = graph.FindCycles();
            Assert.IsFalse(result.Any(), $"empty graph has {result.Count()} cycles");
        }

        [TestMethod]
        public void FindCycles_OneEdgeLinkedNext_NoCycle()    // a->b,b->c,c->d
        {
            var graph = ParseGraph("a->b;b->c;c->d;d->e;e->f;");
            var result = graph.FindCycles();
            Assert.IsFalse(result.Any(), $"empty graph has {result.Count()} cycles");
        }

        [TestMethod]
        public void FindCycles_TwoNodesCircular_1Cycle()
        {
            var graph = ParseGraph("a->b;b->a;");
            var result = graph.FindCycles();

            Assert.IsNotNull(result.Single());

            Assert.IsTrue(CompareCycle<string>(result, "a.b.a"));
        }
        //________________________________________________________________________________________________________
        // helpers
        //________________________________________________________________________________________________________

        /**
         * Represnting graph with text. 
         * paths are seperated with ';' 
         * 
         * a->b,c,d;
         * b->e,f;
         * f->g; 
         * 
         *  OR    
         *  
         *  a->b,c,d;b->e,f;f->g;
         */
        static Dictionary<string, IEnumerable<string>> ParseGraph(string text)
        {
            var graph = new Dictionary<string, IEnumerable<string>>();
            var graphString = rx.Replace(text, @"(?mi)(\r\n)", "");  // as single line for easy regex

            // parsers
            Func<Match, string> parseNode = m => m.Groups["node"].Value;
            Func<Match, string[]> parseEdges = m =>
            {
                var edges = (m.Groups["edges"].Value ?? "").Split(',');
                return (from o in edges select o.Trim()).ToArray();
            };

            var allPaths = graphString.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var path in allPaths)
            {
                var m = rx.Match(path, @"(?is)^(?<node>[a-zA-Z0-9]+)->(?<edges>[a-zA-Z0-9]+(,[a-zA-Z0-9]+)*)$");
                if (!m.Success) throw new Exception("graph error path pattern");

                var node = parseNode(m);
                var edges = parseEdges(m);

                if (!edges.Any()) continue;

                AddGraph<string>(graph, node, edges.ToArray());
            }
            return graph;
        }

        static bool CompareDictionaries<T>(Dictionary<T, IEnumerable<T>> first, Dictionary<T, IEnumerable<T>> second, IEqualityComparer<T> comparer=null)
        {
            if (first == second) return true;
            if (first == null || second == null) return false;
            if (first.Count != second.Count) return false;

            foreach (var kvp in first)
            {
                IEnumerable<T> secondeList;
                if (!second.TryGetValue(kvp.Key, out secondeList)) return false;

                var except = kvp.Value.Except(secondeList, comparer ?? EqualityComparer<T>.Default);
                if (except.Any()) return false;
            }
            return true;
        }

        static bool CompareCycle<T>(IEnumerable<IEnumerable<T>> a, string s)
        {
            var x = string.Join(";", from o in (from o in a select string.Join(".", o)) orderby o select o);
            return x.Equals(s, StringComparison.InvariantCultureIgnoreCase);
        }

        static void AddGraph<T>(Dictionary<T, IEnumerable<T>> graph, T node, params T[] edges)
        {
            if (graph.ContainsKey(node)) throw new InvalidOperationException($"node {node} already exists in graph");
            if (edges == null) return;
            graph.Add(node, new List<T>(edges));
        }
    }
}
