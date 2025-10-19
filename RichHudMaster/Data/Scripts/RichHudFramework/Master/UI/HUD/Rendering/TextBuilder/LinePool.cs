using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    namespace UI
    {
        namespace Rendering.Server
        {
            public abstract partial class TextBuilder
            {
                protected class LinePool : IIndexedCollection<Line>
                {
                    public Line this[int index] => lines[index];

                    public int Count { get; private set; }

                    public int Capacity => lines.Capacity;

                    public IReadOnlyList<Line> PooledLines { get; }

                    private readonly List<Line> lines;
                    private readonly Stack<Line> stack;
                    private readonly TextBuilder builder;

                    public LinePool(TextBuilder builder)
                    {
                        this.builder = builder;
                        lines = new List<Line>();
						PooledLines = lines;
                        stack = new Stack<Line>();
                    }

                    /// <summary>
                    /// Adds a new line to the pool and returns it.
                    /// </summary>
                    public int AddNewLine(int minCapacity = 6)
                    {
                        Line line;

                        if (stack.Count > 0)
                        {
                            line = stack.Pop();
                            lines.Add(line);

                            if (line.Count > 0)
                                line.Clear();

                            if (line.Capacity < minCapacity)
                                line.Capacity = minCapacity;
                        }
                        else
                        {
                            line = new PooledLine(builder, minCapacity);
                            lines.Add(line);
                        }

                        Count = lines.Count;
                        return Count - 1;
                    }

                    /// <summary>
                    /// Pulls a line out of the pool, if one is available, and returns it. If no pooled lines
                    /// are available, a new line will be created.
                    /// </summary>
                    public Line GetNewLine(int minCapacity = 0)
                    {
                        Line line;

                        if (stack.Count > 0)
                        {
                            line = stack.Pop();

                            if (line.Count > 0)
                                line.Clear();

                            if (line.Capacity < minCapacity)
                                line.Capacity = minCapacity;
                        }
                        else
                            return new PooledLine(builder, minCapacity);

                        return line;
                    }

                    /// <summary>
                    /// Adds the given line to the end of the collection.
                    /// </summary>
                    public void Add(Line line)
                    {
                        lines.Add(line);
                        Count = lines.Count;
                    }

                    /// <summary>
                    /// Inserts the line given at the index specified.
                    /// </summary>
                    public void Insert(int index, Line line)
                    {
                        lines.Insert(index, line);
                        Count = lines.Count;
                    }

                    /// <summary>
                    /// Adds the range of lines to the end of the list.
                    /// </summary>
                    public void AddRange(IList<Line> newLines)
                    {
                        lines.EnsureCapacity(Count + newLines.Count);

                        for (int n = 0; n < newLines.Count; n++)
                            Add(newLines[n]);

                        TrimExcess();
                        Count = lines.Count;
                    }

                    /// <summary>
                    /// Inserts the range of lines at the specified index.
                    /// </summary>
                    public void InsertRange(int index, IList<Line> newLines)
                    {
                        lines.InsertRange(index, newLines);
                        Count = lines.Count;

                        TrimExcess();
                    }

                    /// <summary>
                    /// Removes the line at the specified index.
                    /// </summary>
                    public void RemoveAt(int index)
                    {
                        stack.Push(lines[index]);
                        lines.RemoveAt(index);
                        Count = lines.Count;
                    }

                    /// <summary>
                    /// Removes a range of lines from the list.
                    /// </summary>
                    public void RemoveRange(int index, int rangeSize)
                    {
                        if (rangeSize > 0)
                        {
                            // Move everything after the range being removed and move the
                            // lines being removed to the end of the list.
                            for (int i = (index + rangeSize - 1); i >= index; i--)
                            {
                                stack.Push(lines[i]);
                            }

                            lines.RemoveRange(index, rangeSize);
                            Count = lines.Count;
                        }
                    }

                    public void EnsureCapacity(int capacity) =>
                        lines.EnsureCapacity(capacity);

                    public void TrimExcess()
                    {
                        if (lines.Count > 10 && lines.Capacity > 5 * lines.Count)
                            lines.TrimExcess();
                    }

                    /// <summary>
                    /// Removes all lines from the list.
                    /// </summary>
                    public void Clear()
                    {
                        for (int i = lines.Count - 1; i >= 0; i--)
                        {
                            stack.Push(lines[i]);
                            lines.RemoveAt(i);
                        }

                        Count = lines.Count;
                    }

                    /// <summary>
                    /// Retrieves the index of the character immediately preceeding the index given. Returns
                    /// true if successful. Otherwise, the beginning of the collection has been reached.
                    /// </summary>
                    public bool TryGetLastIndex(Vector2I index, out Vector2I lastIndex)
                    {
                        if (index.Y > 0)
                            lastIndex = new Vector2I(index.X, index.Y - 1);
                        else if (index.X > 0)
                            lastIndex = new Vector2I(index.X - 1, lines[index.X - 1].Count - 1);
                        else
                        {
                            lastIndex = index;
                            return false;
                        }

                        return true;
                    }

                    /// <summary>
                    /// Retrieves the index of the character immediately following the index given. Returns
                    /// true if successful. Otherwise, the end of the collection has been reached.
                    /// </summary>
                    public bool TryGetNextIndex(Vector2I index, out Vector2I nextIndex)
                    {
                        if (index.X < Count && index.Y + 1 < lines[index.X].Count)
                            nextIndex = new Vector2I(index.X, index.Y + 1);
                        else if (index.X + 1 < Count)
                            nextIndex = new Vector2I(index.X + 1, 0);
                        else
                        {
                            nextIndex = index;
                            return false;
                        }

                        return true;
                    }

                    private class PooledLine : Line
                    {
                        public PooledLine(TextBuilder builder, int capacity = 0) : base(builder, capacity)
                        { }
                    }
                }
            }
        }
    }
}