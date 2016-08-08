using System;
using System.Collections.Generic;
using System.Linq;
// ReSharper disable InconsistentNaming

namespace System.Collections.Extensions
{
    /**
     * Find cycling dependency chains.
     * 
     * This is an extension for dictionary, since we use as data structure to represent a graph. 
     * Dictionary<T, TValueList> . key = node, value list - neighbours
     *  
     *  e.g: 
     *  Dictionary<strin, List<string> = {
     *      { "a", [ "b", "c"] },
     *      { "b", [ "d", "e", "f"] },
     *      { "e", [ "g", "h", "i"] },
     *      { "g", [ "a"] },
     *      { "i", [ "b"] },
     *  }
     *  
     *  cyclic: 
     *  a.b.e.g.a
     *  b.e.i.b
     *  
     */
    public static class CyclingDeps
    {
        public static IEnumerable<IEnumerable<T>> FindCycles<T, TList>(this IDictionary<T, TList> graph)
            where TList : class, IEnumerable<T>    
            where T : IEquatable<T>                
        {
            var cycles = new List<CyclePath<T>>();
            var visitors = new Visitors<T>();
            var edges = new Func<T, IEnumerable<T>>(key => graph.ValueOrDefault(key, null) ?? Enumerable.Empty<T>());
            var graphNodes = graph.Keys;

            foreach (var node in graphNodes)
            {
                var foundCycles = FindCycles(node, edges, visitors);
                cycles.AddRange(foundCycles);   
            }

            return cycles;
        }

        class CyclePath<T> : List<T> { }
        class CycleStack<T> : Stack<KeyValuePair<T, IEnumerator<T>>> { }   
        class Visitors<T> : Dictionary<T, VisitState> { }

        enum VisitState { NotVisited, Visiting, Visited };

        static List<CyclePath<T>> FindCycles<T>(T root, Func<T, IEnumerable<T>> edges, Visitors<T> visitors)
            where T : IEquatable<T>
        {
            var cycles = new List<CyclePath<T>>();
            var stack  = new CycleStack<T>();

            Push(stack, root, edges, visitors, cycles);  // push the first
            while (stack.Count > 0)
            {
                var pair = stack.Peek();
                var it = pair.Value;   // current graph node edges iterator
                var node = pair.Key;

                if (it.MoveNext()) Push(stack, it.Current, edges, visitors, cycles);
                else { stack.Pop(); visitors[node] = VisitState.Visited; }
            }
            return cycles;
        }

        static void Push<T>(                        
            CycleStack<T> stack,                  // implement non-recursive 
            T node,                               // graph node
            Func<T, IEnumerable<T>> edges,        // graph edges.
            Visitors<T> visitors,                 // graph coloring with visited, visiting and not visited
            List<CyclePath<T>> cycles,            // aggregate found cycles
            IEqualityComparer<T> comparer = null  // graph node comparer (e.g: for strings - ignoredcase)
            ) where T : IEquatable<T>
        {
            var visit = visitors.ValueOrDefault(node, VisitState.NotVisited);
            switch (visit)
            {
                case VisitState.Visited: return;
                case VisitState.Visiting:
                    if (stack.Count == 0) throw new InvalidOperationException("oops. stack is empty");

                    var cmp = comparer ?? EqualityComparer<T>.Default;
                    var path = new CyclePath<T>();

                    var it = stack.GetEnumerator();// climb 
                    while (it.MoveNext() && !cmp.Equals(it.Current.Key, node))
                        path.Add(it.Current.Key);
                    cycles.Add(path);
                    break;
                case VisitState.NotVisited:
                    visitors[node] = VisitState.Visiting;
                    stack.Push(new KeyValuePair<T, IEnumerator<T>>(node, edges(node).GetEnumerator()));
                    break;
            }
        }
        
        /** get dictionary value OR default one **/
        static V ValueOrDefault<K, V>(this IDictionary<K, V> dictionary, K key, V defaultValue)
        {
            V value;
            return dictionary.TryGetValue(key, out value) ? value : defaultValue;
        }
    }


    class Tests
    {
        private static string test1 = @"a->b;b->c;c->d";
        private static string test2 = @"a->b;b->c;c->a";
        private static string test3 = @"a->b,c,d; b->e,c; c->e,f; f->a;";
    }
}
