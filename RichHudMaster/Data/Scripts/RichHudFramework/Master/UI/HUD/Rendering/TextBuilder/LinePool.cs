﻿using System;
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

                    private readonly List<Line> lines;

                    public LinePool()
                    {
                        lines = new List<Line>();
                    }

                    /// <summary>
                    /// Adds a new line to the pool and returns it.
                    /// </summary>
                    public int AddNewLine(int minCapacity = 6)
                    {
                        Line line;

                        if (Count < lines.Count)
                        {
                            int last = lines.Count - 1;
                            line = lines[last];

                            if (line.Count > 0)
                                line.Clear();

                            if (line.Capacity < minCapacity)
                                line.Capacity = minCapacity;
                        }
                        else
                        {
                            line = new PooledLine(minCapacity);
                            lines.Add(line);
                        }

                        Count++;
                        return Count - 1;
                    }

                    /// <summary>
                    /// Pulls a line out of the pool, if one is available, and returns it. If no pooled lines
                    /// are available, a new line will be created.
                    /// </summary>
                    public Line GetNewLine(int minCapacity = 6)
                    {
                        Line line;

                        if (Count < lines.Count)
                        {
                            int last = lines.Count - 1;

                            line = lines[last];
                            lines.RemoveAt(last);

                            if (line.Count > 0)
                                line.Clear();

                            if (line.Capacity < minCapacity)
                                line.Capacity = minCapacity;
                        }
                        else
                            return new PooledLine(minCapacity);

                        return line;
                    }

                    /// <summary>
                    /// Adds the given line to the end of the collection.
                    /// </summary>
                    public void Add(Line line)
                    {
                        if (Count < lines.Count)
                        {
                            Line pooledLine = lines[Count];
                            lines[Count] = line;

                            lines.Add(pooledLine);
                        }
                        else
                            lines.Add(line);

                        Count++;
                    }

                    /// <summary>
                    /// Inserts the line given at the index specified.
                    /// </summary>
                    public void Insert(int index, Line line)
                    {
                        if (index < lines.Count)
                        {
                            if (index < Count)
                            {
                                lines.Insert(index, line);
                            }
                            else
                            {
                                Line pooledLine = lines[index];
                                lines[index] = line;

                                lines.Add(pooledLine);
                            }                            
                        }
                        else
                            lines.Add(line);

                        Count++;
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
                    }

                    /// <summary>
                    /// Inserts the range of lines at the specified index.
                    /// </summary>
                    public void InsertRange(int index, IList<Line> newLines)
                    {
                        lines.InsertRange(index, newLines);
                        Count += newLines.Count;

                        TrimExcess();
                    }

                    /// <summary>
                    /// Removes the line at the specified index.
                    /// </summary>
                    public void RemoveAt(int index)
                    {
                        Line pooledLine = lines[index];

                        lines.RemoveAt(index);
                        lines.Add(pooledLine);
                        Count--;
                    }

                    /// <summary>
                    /// Removes a range of lines from the list.
                    /// </summary>
                    public void RemoveRange(int index, int rangeSize)
                    {
                        // Move everything after the range being removed and move the
                        // lines being removed to the end of the list.
                        for (int n = (index + rangeSize); n < Count; n++)
                        {
                            Line old = lines[n - rangeSize];

                            lines[n - rangeSize] = lines[n];
                            lines[n] = old;
                        }

                        Count -= rangeSize;
                    }

                    public void EnsureCapacity(int capacity) =>
                        lines.EnsureCapacity(capacity);

                    public void TrimExcess()
                    {
                        if (Count > 10 && lines.Capacity > 5 * Count)
                            lines.TrimExcess();
                    }

                    /// <summary>
                    /// Removes all lines from the list.
                    /// </summary>
                    public void Clear()
                    {
                        Count = 0;
                    }

                    public override string ToString()
                    {
                        StringBuilder sb = new StringBuilder();

                        for (int n = 0; n < Count; n++)
                        {
                            lines[n].GetText(sb);
                        }

                        return sb.ToString();
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
                        public PooledLine(int capacity = 6) : base(6)
                        { }
                    }
                }
            }
        }
    }
}