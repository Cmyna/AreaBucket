
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;

namespace AreaBucket.Mathematics.NativeCollections
{
    internal struct SplayNode
    {
        public int fartherIndex;
        public int2 childrenIndex;
        /// <summary>
        /// sub tree size (include node itself, so >= 1)
        /// </summary>
        public int size;
    }

    /// <summary>
    /// the splay tree use comparer instead of IComparable (for values) to gain possibility of using context out of values themselves
    /// hence it is easily to create an indexed based splay tree by passing an index comparer (IComparer<int>) and insert/delete indices values
    /// 
    /// For values that are equal:
    /// insert & delete follows FIFO rule, ranks follows the insert order
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct NativeSplayTree<T, C>: IDisposable 
        where T : unmanaged
        where C : IComparer<T>
    {
        

        public struct SplayTreeDebuger
        {
            private NativeSplayTree<T, C> tree;
            public SplayTreeDebuger(NativeSplayTree<T, C> tree)
            {
                this.tree = tree;
            }

            public string Abstract()
            {
                var builder = new StringBuilder();
                builder.AppendLine("Tree: ");
                builder.AppendLine($"buffer length: {tree.values.Length}");
                builder.AppendLine($"rootNode: {tree.rootNode}");
                builder.AppendLine($"cursor: {tree.cursor}");
                builder.AppendLine("nodes info: ");
                NodeInfo(tree.rootNode, "", builder);
                return builder.ToString();
            }


            private string NodeInfo(int i, string indent, StringBuilder builder = null, bool recursive = true)
            {
                if (builder == null) builder = new StringBuilder();
                builder.AppendLine($"{indent}| node[{i}]");
                if (!tree.IsValidNode(i)) return builder.ToString();
                var value = tree.values[i];
                var node = tree.nodes[i];


                builder.AppendLine($"{indent}| value: {value}");
                builder.AppendLine($"{indent}| father: {node.fartherIndex}");
                builder.AppendLine($"{indent}| size: {node.size}");
                builder.AppendLine($"{indent}| left child: ");
                if (recursive) NodeInfo(tree.Child(i, 0), indent + "   ", builder);
                builder.AppendLine($"{indent}| right child: ");
                if (recursive) NodeInfo(tree.Child(i, 1), indent + "   ", builder);
                return builder.ToString();
            }

            public int4 Search(T value)
            {
                var kRange = tree.Search(value, out var indices);
                return new int4(kRange.x, kRange.y, indices.x, indices.y);
            }
            
        }


        /// <summary>
        /// invalid node index
        /// </summary>
        public const int INVALID = 0;
        public const int BRANCH_INVALID = -1;

        private int rootNode;
        private int cursor;
        private NativeList<T> values;
        private NativeList<SplayNode> nodes;
        private C comparer;


        public NativeSplayTree(C comparer, int initialCap, Allocator allocator = Allocator.Temp)
        {
            rootNode = INVALID;
            cursor = 1;
            this.comparer = comparer;
            values = new NativeList<T>(initialCap + 1, allocator);
            nodes = new NativeList<SplayNode>(initialCap + 1, allocator);
            values.Resize(initialCap + 1, NativeArrayOptions.ClearMemory);
            nodes.Resize(initialCap + 1, NativeArrayOptions.ClearMemory);
        }


        public int Count()
        {
            return Size(rootNode);
        }

        public bool PreValue(out T result)
        {
            var i = Pre();
            result = values[i];
            return i != INVALID;
        }


        public bool NextValue(out T result)
        {
            var i = Next();
            result = values[i];
            return i != INVALID;
        }

        public void DeleteNode(T value)
        {
            Rank(value); // splay it to root node
            // if empty tree
            if (!IsValidNode(rootNode)) return;
            // check again, the root value equals too value or not
            if (comparer.Compare(value, values[rootNode]) != 0) return;
            var oldRootNode = rootNode;
            var rootLeftChild = Child(rootNode, 0);
            var rootRightChild = Child(rootNode, 1);

            // only root node
            if (!IsValidNode(rootLeftChild) && !IsValidNode(rootRightChild))
            {
                Clear(rootNode);
                rootNode = INVALID;
                return;
            }

            // no any one side tree
            if (!IsValidNode(rootLeftChild) || !IsValidNode(rootRightChild))
            {
                var newRootNodeIndex = IsValidNode(rootLeftChild) ? rootLeftChild : rootRightChild;
                SetFarther(newRootNodeIndex, INVALID);
                rootNode = newRootNodeIndex;
                Clear(oldRootNode);
                return;
            }


            // do merge
            var leftLargest = Pre();
            SetFarther(rootRightChild, leftLargest);
            SetChild(leftLargest, 1, rootRightChild);
            Clear(oldRootNode);
            MaintainSize(leftLargest);
        }




        public bool Kth(int k, out T value)
        {
            value = default;
            if (k <= 0 || k > Size(rootNode)) return false;
            var remainK = k;
            var current = rootNode;
            while (true)
            {
                var leftChildIndex = Child(current, 0);
                var leftChildSize = Size(leftChildIndex);
                // binary search, until it is in remain right tree or no left subtree
                if (IsValidNode(leftChildIndex) && remainK <= leftChildSize)
                {
                    current = leftChildIndex;
                }
                else
                {
                    remainK -= leftChildSize + 1; // minus left tree + current node
                    if (remainK <= 0)
                    {
                        Splay(current);
                        value = values[current];
                        return true;
                    }
                    current = Child(current, 1);
                }
            }
        }




        /// <summary>
        /// return the rank for value
        /// for multiple equal values, expected to return the highest rank
        /// </summary>
        /// <param name="value"></param>
        /// <returns>
        /// 1 based rank value
        /// if returns not in valid range (<= 0 or > Count) means the value not in the tree
        /// if value is not in the tree, return rank of value just smaller than it
        /// </returns>
        public int Rank(T value)
        {
            var res = 0;
            var current = rootNode;
            int lastCompareResult = -1;
            var farther = INVALID; var grand = INVALID;
            while (IsValidNode(current))
            {
                lastCompareResult = comparer.Compare(value, values[current]);
                if (lastCompareResult < 0)
                {
                    grand = farther;
                    farther = current;
                    current = Child(current, 0);
                }
                else // compareResult >= 0
                {
                    res += Size(Child(current, 0)) + 1;
                    grand = farther;
                    farther = current;
                    current = Child(current, 1);
                }
            }
            // lastCompareResult < 0: farther is larger than value, grand equal/smaller than value
            if (lastCompareResult < 0 && IsValidNode(grand)) Splay(grand);
            else if (lastCompareResult == 0 && IsValidNode(farther)) Splay(farther);
            // if above two condition not satisfied, it means farther is rootNode or no farther, no need to splay
            return res;
        }

        /// <summary>
        /// return range of k that compare equals to value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int2 RankRange(T value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="nodeIndices">
        /// node indices of return's k, 
        /// if return's k is invalid value then it is 0(NativeSplayTree.INVALID)
        /// </param>
        /// <returns></returns>
        private int2 Search(T value, out int2 nodeIndices)
        {
            nodeIndices = default;
            int2 matchedK = new int2(int.MinValue, int.MaxValue);

            int2 currents = new int2(rootNode, rootNode);
            int2 currentsK = default;
            currentsK.x = Size(Child(rootNode, 0)) + 1;
            currentsK.y = currentsK.x;

            var nextCurrents = currents;
            var nextCurrentsK = currentsK;

            while (IsValidNode(currents.x) || IsValidNode(currents.y))
            {
                if (IsValidNode(currents.x))
                {
                    var cp1 = comparer.Compare(value, values[currents.x]);
                    if (cp1 < 0)
                    {
                        matchedK.y = math.min(matchedK.y, currentsK.x - 1);
                        nextCurrents.x = Child(currents.x, 0);
                        nextCurrents.y = nextCurrents.x;
                        nextCurrentsK.x = currentsK.x - Size(Child(nextCurrents.x, 1)) - 1;
                        nextCurrentsK.y = nextCurrentsK.x;
                    }
                    else if (cp1 == 0)
                    {
                        nextCurrents.x = Child(currents.x, 0);
                        nextCurrentsK.x = currentsK.x - Size(Child(nextCurrents.x, 1)) - 1;
                        nodeIndices.x = currents.x;
                    }
                    else // cp1 > 0
                    {
                        matchedK.x = math.max(matchedK.x, currentsK.x + 1);
                        nextCurrents.x = Child(currents.x, 1);
                        nextCurrentsK.x = currentsK.x + Size(Child(nextCurrents.x, 0)) + 1;
                    }
                }

                if (IsValidNode(currents.y))
                {
                    var cp2 = comparer.Compare(value, values[currents.y]);
                    if (cp2 > 0)
                    {
                        matchedK.x = math.max(matchedK.x, currentsK.y + 1);
                        nextCurrents.x = Child(currents.y, 1);
                        nextCurrents.y = nextCurrents.x;
                        nextCurrentsK.x = currentsK.y + Size(Child(nextCurrents.y, 0)) + 1;
                        nextCurrentsK.y = nextCurrentsK.x;
                    }
                    else if (cp2 == 0)
                    {
                        nextCurrents.y = Child(currents.y, 1);
                        nextCurrentsK.y = currentsK.y + Size(Child(nextCurrents.y, 0)) + 1;
                        nodeIndices.y = currents.y;
                    }
                    else // cp2 < 0
                    {
                        matchedK.y = math.min(matchedK.y, currentsK.y - 1);
                        nextCurrents.y = Child(currents.y, 0);
                        nextCurrentsK.y = currentsK.y - Size(Child(nextCurrents.y, 1)) - 1;
                    }
                }

                currents = nextCurrents;
                currentsK = nextCurrentsK;
            }
            return matchedK;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns>the k-th</returns>
        public int InsertNode(T value)
        {
            if (!IsValidNode(rootNode))
            {
                SetValue(cursor, value);
                rootNode = cursor;
                cursor++;
                MaintainSize(rootNode);
                return 1;
            }

            var current = rootNode;
            var fartherIndex = INVALID;
            var childBranchIndex = INVALID;
            // bin search next empty node slot
            while (IsValidNode(current))
            {
                fartherIndex = current;
                childBranchIndex = comparer.Compare(value, values[current]) < 0 ? 0 : 1;
                current = Child(current, childBranchIndex);
            }

            // do insert
            SetValue(cursor, value);
            SetFarther(cursor, fartherIndex);
            SetChild(fartherIndex, childBranchIndex, cursor);
            MaintainSize(cursor);
            MaintainSize(fartherIndex);
            Splay(cursor);
            cursor++;

            return Size(Child(rootNode, 0)) + 1;
        }

        /// <summary>
        /// find current root node left largest
        /// may tree internal structural change after calling this method
        /// </summary>
        /// <returns></returns>
        private int Pre()
        {
            var current = Child(rootNode, 0);
            if (!IsValidNode(current)) return current;
            while (IsValidNode(Child(current, 1))) current = Child(current, 1);
            Splay(current);
            return current;
        }

        /// <summary>
        /// may tree internal structural change after calling this method
        /// </summary>
        /// <returns></returns>
        private int Next()
        {
            var current = Child(rootNode, 1);
            if (!IsValidNode(current)) return current;
            while (IsValidNode(Child(current, 0))) current = Child(current, 0);
            Splay(current);
            return current;
        }


        private void Splay(int i)
        {
            while (true)
            {
                var fartherIndex = Farther(i);
                if (!IsValidNode(fartherIndex)) break;
                var grandIndex = Farther(fartherIndex);

                if (IsValidNode(grandIndex))
                {
                    // determine doing zig-zig or zig-zag 
                    // zig-zig: IndexOfChildBranch(i) === IndexOfChildBranch(f), else zig-zag
                    Rotate(IndexOfChildBranch(i) == IndexOfChildBranch(fartherIndex) ? fartherIndex : i);
                }
                Rotate(i); // both zig-zig and zig-zag second op is rotate(i)
            }
            rootNode = i;
        }


        private void Rotate(int i)
        {
            var fartherIndex = Farther(i);
            var grandIndex = Farther(fartherIndex);
            if (!IsValidNode(fartherIndex)) return;

            var iBranchIndex = IndexOfChildBranch(i);
            var shiftedChildIndex = Child(i, 1 - iBranchIndex);

            // replace i's father's child to i's child (or INVALID)
            SetChild(fartherIndex, iBranchIndex, shiftedChildIndex);
            if (IsValidNode(shiftedChildIndex)) SetFarther(shiftedChildIndex, fartherIndex);


            // farther become i's child
            SetChild(i, 1 - iBranchIndex, fartherIndex);
            SetFarther(fartherIndex, i);

            // if has grand, i becomes grand's child
            if (IsValidNode(grandIndex))
            {
                SetChild(
                    grandIndex,
                    nodes[grandIndex].childrenIndex[0] == fartherIndex ? 0 : 1,
                    i);
            }
            SetFarther(i, grandIndex);
            MaintainSize(fartherIndex);
            MaintainSize(i);
        }


        private void Clear(int i)
        {
            nodes[i] = default;
        }


        private int IndexOfChildBranch(int childNodeIndex)
        {
            // expect childNodeIndex and farther index not over node's length
            var fartherIndex = Farther(childNodeIndex);
            return nodes[fartherIndex].childrenIndex[0] == childNodeIndex ? 0 : 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MaintainSize(int i)
        {
            if (!IsValidNode(i)) return;
            var node = nodes[i];
            node.size = Size(Child(i, 0)) + Size(Child(i, 1)) + 1;
            nodes[i] = node;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Size(int i)
        {
            if (!IsValidNode(i)) return 0;
            return nodes[i].size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SetChild(int i, int childIndex, int newValue)
        {
            if (!IsValidNode(i)) return false;
            var node = nodes[i];
            node.childrenIndex[childIndex] = newValue;
            nodes[i] = node;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Child(int i, int childIndex)
        {
            if (!IsValidNode(i)) return INVALID;
            return nodes[i].childrenIndex[childIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Farther(int i)
        {
            if (!IsValidNode(i)) return INVALID;
            return nodes[i].fartherIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SetFarther(int i, int fartherIndex)
        {
            if (!IsValidNode(i)) return false;
            var node = nodes[i];
            node.fartherIndex = fartherIndex;
            nodes[i] = node;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidNode(int nodeIndex)
        {
            return nodeIndex > INVALID && nodeIndex < values.Length;
        }


        private void SetValue(int index, T value)
        {
            if (index < values.Length)
            {
                values[index] = value;
                return;
            }

            values.Add(value);
            nodes.Add(default);
        }

        public SplayTreeDebuger AsDebuger()
        {
            return new SplayTreeDebuger(this);
        }

        public void Dispose()
        {
            if (values.IsCreated) values.Dispose();
            if (nodes.IsCreated) nodes.Dispose();
        }
    }
}
