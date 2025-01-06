using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace AreaBucket.Mathematics.NativeCollections
{
    /// <summary>
    /// for item who has higher priority (more close to root node) than others, its CompareTo(others) should return value > 0
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct NativeHeap<T> where T : unmanaged, IComparable<T>
    {
        public int count;
        public NativeList<T> buffer;


        public NativeHeap(int initalCap, Allocator allocator = Allocator.Temp)
        {
            this.count = 0;
            buffer = new NativeList<T>(initalCap + 1, allocator);
            buffer.Resize(initalCap + 1, NativeArrayOptions.ClearMemory);
            this.buffer[0] = default;
        }


        public bool Push(T item)
        {
            if (buffer.Length < count + 1) return false;
            count++;
            SetValue(count, item);
            ShiftUp(count);
            return true;
        }


        public bool Pop(out T result)
        {
            result = default;
            if (count == 0) return false;
            result = buffer[1];
            buffer[1] = buffer[count];
            count--;
            ShiftDown(1);
            return true;
        }


        private void ShiftUp(int index1Based)
        {
            // root node does't need to shift, and buffer[0] is not a node
            if (index1Based <= 1) return;

            var current = buffer[index1Based];

            var parentIndex1Based = index1Based / 2;

            var parent = buffer[parentIndex1Based];

            if (current.CompareTo(parent) > 0)
            {
                buffer[parentIndex1Based] = current;
                buffer[index1Based] = parent;
                ShiftUp(parentIndex1Based);
            }
        }

        private void ShiftDown(int index1Based)
        {
            var leftChildIndex1Based = index1Based * 2;
            if (leftChildIndex1Based > count) return;

            var current = buffer[index1Based];

            var leftChild = buffer[leftChildIndex1Based];

            var rightChildIndex1Based = leftChildIndex1Based + 1;

            var shiftTargetIndex1Based = leftChildIndex1Based;

            if (rightChildIndex1Based <= count && (buffer[rightChildIndex1Based].CompareTo(leftChild) > 0))
            {
                shiftTargetIndex1Based = rightChildIndex1Based;
            }

            var shiftedChild = buffer[shiftTargetIndex1Based];

            if (current.CompareTo(shiftedChild) < 0)
            {
                buffer[index1Based] = shiftedChild;
                buffer[shiftTargetIndex1Based] = current;
                ShiftDown(shiftTargetIndex1Based);
            }
        }

        private void SetValue(int index, T value)
        {
            if (index < buffer.Length)
            {
                buffer[index] = value;
                return;
            }
            buffer.Add(value);
        }
    }
}
