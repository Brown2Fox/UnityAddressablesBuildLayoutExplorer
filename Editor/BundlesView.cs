﻿//
// Addressables Build Layout Explorer for Unity. Copyright (c) 2021 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityAddressablesBuildLayoutExplorer
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq.Expressions;
using System;

namespace Oddworm.EditorFramework.BuildLayoutExplorer
{
    public class BundlesView : BuildLayoutView
    {
        [SerializeField] BuildLayoutTreeView m_TreeView;
        SearchField m_SearchField;
        string m_StatusLabel;

        public override void Awake()
        {
            base.Awake();

            m_TreeView = new BundleTreeView(window);
            m_SearchField = new SearchField(window);
        }

        public override void Rebuild(BuildLayout buildLayout)
        {
            base.Rebuild(buildLayout);

            m_TreeView.SetBuildLayout(buildLayout);

            var size = 0L;
            var count = 0;
            foreach(var group in buildLayout.groups)
            {
                foreach(var bundle in group.bundles)
                {
                    size += bundle.size;
                    count++;
                }
            }
            m_StatusLabel = $"{count} bundles making up {EditorUtility.FormatBytes(size)}";
        }

        public override void OnGUI()
        {
            var rect = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            m_TreeView.OnGUI(rect);
        }

        public override void OnToolbarGUI()
        {
            base.OnToolbarGUI();

            GUILayout.Space(10);

            if (m_SearchField.OnToolbarGUI(GUILayout.ExpandWidth(true)))
                m_TreeView.searchString = m_SearchField.text;
        }

        public override void OnStatusbarGUI()
        {
            base.OnStatusbarGUI();

            GUILayout.Label(m_StatusLabel);
        }
    }
}
