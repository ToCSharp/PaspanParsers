using System;
using System.Collections.Generic;

namespace Corpus.Basics
{
    public class Counter
    {
        private int count;

        public int Count { get; set; }

        public Counter(int start)
        {
            count = start;
        }

        public void Increment()
        {
            count = count + 1;
        }

        public int Get() => count;
    }

    public enum Color
    {
        Red,
        Green = 2,
        Blue
    }

    public interface IShape
    {
        double Area();
    }
}
