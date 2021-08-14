﻿//
// Addressables Build Layout Explorer for Unity. Copyright (c) 2021 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityAddressablesBuildLayoutExplorer
//
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Oddworm.EditorFramework.BuildLayoutExplorer
{
    public abstract class BuildLayoutTreeView : UnityEditor.IMGUI.Controls.TreeView
    {
        public System.Action<TreeViewItem> selectedItemChanged;

        protected BuildLayoutWindow m_Window;
        protected int m_UniqueId;

        int m_FirstVisibleRow;
        TreeViewItem m_CachedTree;
        List<TreeViewItem> m_CachedRows;

        public BuildLayoutTreeView(BuildLayoutWindow window, TreeViewState state, MultiColumnHeader multiColumnHeader)
                   : base(state, multiColumnHeader)
        {
            m_Window = window;

            rowHeight = 20;
            showAlternatingRowBackgrounds = true;
            showBorder = false;
            columnIndexForTreeFoldouts = 0;
            extraSpaceBeforeIconAndLabel = 0;
            baseIndent = 0;
            multiColumnHeader.sortingChanged += OnSortingChanged;
            multiColumnHeader.ResizeToFit();
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            if (m_CachedTree != null)
                return m_CachedTree;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            root.AddChild(new TreeViewItem { id = root.id + 1, depth = -1, displayName = "" });
            return root;
        }

        /// <summary>
        /// Iterates the tree and calls the specified <paramref name="callback"/> for each tree item.
        /// </summary>
        /// <param name="callback">The method that is called for each item. Return true to abort the iteration process, false to continue.</param>
        public void IterateItems(System.Func<TreeViewItem, bool> callback)
        {
            IterateItemsInternal(rootItem, callback);
        }

        /// <summary>
        /// Iterates the tree and calls the specified <paramref name="callback"/> for each item that is part of the specified <paramref name="parent"/>.
        /// </summary>
        /// <param name="callback">The method that is called for each item. Return true to abort the iteration process, false to continue.</param>
        public void IterateItems(TreeViewItem parent, System.Func<TreeViewItem, bool> callback)
        {
            IterateItemsInternal(parent, callback);
        }

        // Return value indicates to abort the iteration process. false=continue, true=abort
        bool IterateItemsInternal(TreeViewItem parent, System.Func<TreeViewItem, bool> callback)
        {
            if (parent == null)
                return false;

            if (callback.Invoke(parent))
                return true;

            if (!parent.hasChildren)
                return false;

            for (int n = 0, nend = parent.children.Count; n < nend; ++n)
            {
                var child = parent.children[n];
                if (child == null)
                    continue;

                if (!child.hasChildren)
                {
                    if (callback.Invoke(child))
                        return true;
                    continue;
                }

                if (IterateItemsInternal(child, callback))
                    return true;
            }

            return false;
        }

        public void SetBuildLayout(BuildLayout buildLayout)
        {
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };

            OnBuildTree(root, buildLayout);
            if (!root.hasChildren)
                root.AddChild(new TreeViewItem { id = root.id + 1, depth = -1, displayName = "" });

            m_CachedTree = root;
            Reload();
        }

        protected abstract void OnBuildTree(TreeViewItem rootItem, BuildLayout buildLayout);

        protected override void BeforeRowsGUI()
        {
            base.BeforeRowsGUI();

            GetFirstAndLastVisibleRows(out m_FirstVisibleRow, out _);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                var item = args.item as BaseItem;
                var rect = args.GetCellRect(i);

                if (args.row == m_FirstVisibleRow)
                {
                    var r = rect;
                    r.x += r.width + (i > 0 ? 2 : -1);
                    r.width = 1;
                    r.height = 10000;
                    var oldColor = GUI.color;
                    GUI.color = new Color(0, 0, 0, 0.15f);
                    GUI.DrawTexture(r, EditorGUIUtility.whiteTexture);
                    GUI.color = oldColor;
                }

                if (i == 0)
                {
                    rect.x += extraSpaceBeforeIconAndLabel;
                    rect.width -= extraSpaceBeforeIconAndLabel;
                    rect = IndentByDepth(item.depth, rect);

                    if (item.icon != null)
                    {
                        var r = rect;
                        r.width = 20;
                        EditorGUI.LabelField(r, new GUIContent(item.icon), Styles.iconStyle);
                        rect.x += 20;
                        rect.width -= 20;
                    }
                }

                if (item != null)
                {
                    var column = args.GetColumn(i);
                    item.OnGUI(rect, column);
                }
            }
        }

        void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            if (rootItem == null || !rootItem.hasChildren)
                return;

            Reload();
        }

        protected int CompareItem(TreeViewItem x, TreeViewItem y)
        {
            var sortingColumn = multiColumnHeader.sortedColumnIndex;
            if (sortingColumn < 0)
                sortingColumn = 0;

            var ascending = multiColumnHeader.IsSortedAscending(sortingColumn);
            var itemA = (ascending ? x : y);
            var itemB = (ascending ? y : x);

            // Some items should not be affected by the sort, for example category
            // nodes should be stable regardless of sorting up or down.
            var typedItemA = itemA as BaseItem;
            if (typedItemA != null && !typedItemA.supportsSortingOrder)
            {
                itemA = x;
                itemB = y;
            }

            var result = 0;
            if (typedItemA != null)
                result = typedItemA.CompareTo(itemB, sortingColumn);

            if (result == 0 && itemA != null && itemB != null)
                result = itemA.id.CompareTo(itemB.id);

            return result;
        }

        protected virtual void SortAndAddExpandedRows(TreeViewItem root, IList<TreeViewItem> rows)
        {
            if (!root.hasChildren)
                return;

            root.children.Sort(CompareItem);
            foreach (var child in root.children)
                GetAndSortExpandedRowsRecursive(child, rows);
        }

        void GetAndSortExpandedRowsRecursive(TreeViewItem item, IList<TreeViewItem> expandedRows)
        {
            if (item == null)
                return;

            expandedRows.Add(item);

            if (item.hasChildren && IsExpanded(item.id))
            {
                item.children.Sort(CompareItem);
                foreach (var child in item.children)
                    GetAndSortExpandedRowsRecursive(child, expandedRows);
            }
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            if (m_CachedRows == null)
                m_CachedRows = new List<TreeViewItem>(128);
            m_CachedRows.Clear();

            if (hasSearch)
            {
                SearchTree(root, searchString, m_CachedRows);
                m_CachedRows.Sort(CompareItem);
            }
            else
            {
                SortAndAddExpandedRows(root, m_CachedRows);
            }

            return m_CachedRows;
        }

        protected virtual void SearchTree(TreeViewItem root, string search, List<TreeViewItem> result)
        {
            var stack = new Stack<TreeViewItem>();

            stack.Push(root);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!current.hasChildren)
                    continue;

                foreach (var child in current.children)
                {
                    if (child == null)
                        continue;

                    var item = child as BaseItem;
                    if (item != null && !item.supportsSearch)
                        continue;

                    if (DoesItemMatchSearch(child, search))
                        result.Add(child);

                    stack.Push(child);
                }
            }
        }

        Rect IndentByDepth(int itemDepth, Rect rect)
        {
            var foldoutWidth = 14;
            var indent = itemDepth + 1;

            rect.x += indent * foldoutWidth;
            rect.width -= indent * foldoutWidth;

            return rect;
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);

            TreeViewItem selectedItem = null;

            if (selectedIds != null && selectedIds.Count > 0)
                selectedItem = FindItem(selectedIds[0], rootItem);

            selectedItemChanged?.Invoke(selectedItem);
        }

        protected static Rect SpaceL(ref Rect position, float pixels)
        {
            var r = position;
            r.width = Mathf.Min(pixels, r.width);
            position.x += r.width;
            position.width -= r.width;
            return r;
        }

        protected static Rect SpaceR(ref Rect position, float pixels)
        {
            var r = position;
            r.x = r.xMax;
            r.width = Mathf.Min(pixels, r.width); //pixels;
            r.x -= (r.width + 2);
            position.width -= (r.width + 2);
            return r;
        }

        [System.Serializable]
        protected abstract class BaseItem : TreeViewItem
        {
            public bool supportsSortingOrder = true;
            public bool supportsSearch = false;
            public BuildLayoutTreeView treeView;

            static GUIContent s_GUIContent = new GUIContent();

            protected static GUIContent CachedGUIContent(Texture image, string tooltip)
            {
                s_GUIContent.text = "";
                s_GUIContent.tooltip = tooltip;
                s_GUIContent.image = image;
                return s_GUIContent;
            }

            public abstract void OnGUI(Rect position, int column);
            public abstract int CompareTo(TreeViewItem other, int column);
        }
    }
}
