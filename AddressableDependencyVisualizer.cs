#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Text;

namespace GameCore.Editor.Addressables
{
    /// <summary>
    /// Addressable 종속성 시각화 도구
    /// 에셋 간 의존성 그래프, 순환 참조 검출, 번들 크기 예측
    /// </summary>
    public class AddressableDependencyVisualizer : EditorWindow
    {
        #region Nested Classes

        private class AssetNode
        {
            public string Address { get; set; }
            public string GroupName { get; set; }
            public string AssetPath { get; set; }
            public string Guid { get; set; }
            public long FileSize { get; set; }
            public long CompressedSize { get; set; }
            public Vector2 Position { get; set; }
            public List<AssetNode> Dependencies { get; set; } = new List<AssetNode>();
            public List<AssetNode> ReferencedBy { get; set; } = new List<AssetNode>();
            public int Depth { get; set; }
            public bool IsExpanded { get; set; } = true;
            public bool IsSelected { get; set; }
            public NodeType Type { get; set; }
            public Color NodeColor { get; set; }

            // 분석 데이터
            public int TotalDependencyCount { get; set; }
            public int DirectDependencyCount { get; set; }
            public int CircularReferenceCount { get; set; }
            public float LoadPriority { get; set; }
        }

        private enum NodeType
        {
            Root,
            Prefab,
            Texture,
            Material,
            Mesh,
            Audio,
            Scene,
            ScriptableObject,
            Other
        }

        private class DependencyLink
        {
            public AssetNode Source { get; set; }
            public AssetNode Target { get; set; }
            public LinkType Type { get; set; }
            public float Weight { get; set; }
        }

        private enum LinkType
        {
            Direct,
            Indirect,
            Circular,
            Weak,
            Strong
        }

        private class CircularReference
        {
            public List<AssetNode> Nodes { get; set; } = new List<AssetNode>();
            public int Severity { get; set; } // 1-5
            public string Description { get; set; }
        }

        private class BundleAnalysis
        {
            public string GroupName { get; set; }
            public long EstimatedSize { get; set; }
            public long CompressedSize { get; set; }
            public int AssetCount { get; set; }
            public Dictionary<NodeType, int> TypeDistribution { get; set; } = new Dictionary<NodeType, int>();
            public List<string> TopAssets { get; set; } = new List<string>();
            public float CompressionRatio { get; set; }
        }

        private enum ViewMode
        {
            Hierarchy,
            ForceDirected,
            Circular,
            Grid,
            TreeMap
        }

        private enum FilterMode
        {
            All,
            CircularOnly,
            LargeOnly,
            SelectedGroup,
            CustomFilter
        }

        #endregion

        #region Fields

        // 설정
        private AddressableAssetSettings _settings;

        // 노드 관리
        private Dictionary<string, AssetNode> _nodes = new Dictionary<string, AssetNode>();
        private List<DependencyLink> _links = new List<DependencyLink>();
        private List<CircularReference> _circularReferences = new List<CircularReference>();
        private AssetNode _selectedNode;
        private AssetNode _hoveredNode;

        // 분석 데이터
        private Dictionary<string, BundleAnalysis> _bundleAnalyses = new Dictionary<string, BundleAnalysis>();
        private long _totalProjectSize;
        private long _totalAddressableSize;
        private int _totalAssetCount;

        // UI 상태
        private ViewMode _viewMode = ViewMode.Hierarchy;
        private FilterMode _filterMode = FilterMode.All;
        private float _zoomLevel = 1.0f;
        private Vector2 _panOffset = Vector2.zero;
        private bool _isDragging;
        private Vector2 _lastMousePosition;

        // 필터링
        private string _searchFilter = "";
        private AddressableAssetGroup _selectedGroup;
        private HashSet<NodeType> _visibleTypes = new HashSet<NodeType>();
        private int _minDependencyCount = 0;
        private long _minFileSize = 0;

        // 레이아웃
        private Rect _graphRect;
        private Rect _detailsRect;
        private Rect _toolbarRect;
        private float _splitPosition = 0.7f;

        // 스타일
        private GUIStyle _nodeStyle;
        private GUIStyle _selectedNodeStyle;
        private GUIStyle _linkStyle;
        private Color _backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        private Color _gridColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        // 캐시
        private Dictionary<NodeType, Texture2D> _nodeIcons = new Dictionary<NodeType, Texture2D>();
        private bool _needsRepaint;
        private float _lastAnalysisTime;

        // 설정
        private bool _showLabels = true;
        private bool _showSizes = true;
        private bool _showGrid = true;
        private bool _animateLayout = true;
        private bool _highlightCircular = true;
        private bool _autoArrange = true;

        #endregion

        #region Window Setup

        [MenuItem("*COSMOS*/Util/Addressables/Dependency Visualizer")]
        public static void ShowWindow()
        {
            var window = GetWindow<AddressableDependencyVisualizer>("Dependency Visualizer");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        private void OnEnable()
        {
            _settings = AddressableAssetSettingsDefaultObject.Settings;

            if (_settings == null)
            {
                Debug.LogError("Addressable settings not found!");
                return;
            }

            InitializeStyles();
            LoadNodeIcons();
            InitializeVisibleTypes();

            AnalyzeDependencies();
        }

        private void OnGUI()
        {
            if (_settings == null)
            {
                EditorGUILayout.HelpBox("Addressable Settings not found. Please create or assign settings.", MessageType.Error);
                if (GUILayout.Button("Create Settings"))
                {
                    AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(
                        AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                        AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                        true, true);
                    _settings = AddressableAssetSettingsDefaultObject.Settings;
                    AnalyzeDependencies();
                }
                return;
            }

            // 레이아웃 계산
            CalculateLayout();

            // 툴바 그리기
            DrawToolbar();

            // 메인 컨텐츠
            EditorGUILayout.BeginHorizontal();

            // 그래프 영역
            DrawGraphArea();

            // 디테일 패널
            DrawDetailsPanel();

            EditorGUILayout.EndHorizontal();

            // 이벤트 처리
            ProcessEvents();

            if (_needsRepaint)
            {
                Repaint();
                _needsRepaint = false;
            }
        }

        #endregion

        #region Initialization

        private void InitializeStyles()
        {
            _nodeStyle = new GUIStyle(GUI.skin.box);
            _nodeStyle.alignment = TextAnchor.MiddleCenter;
            _nodeStyle.normal.textColor = Color.white;

            _selectedNodeStyle = new GUIStyle(_nodeStyle);
            _selectedNodeStyle.normal.background = CreateColorTexture(new Color(0.3f, 0.5f, 0.8f, 0.8f));

            _linkStyle = new GUIStyle();
            _linkStyle.normal.background = CreateColorTexture(Color.white);
        }

        private void LoadNodeIcons()
        {
            _nodeIcons[NodeType.Prefab] = EditorGUIUtility.FindTexture("Prefab Icon");
            _nodeIcons[NodeType.Texture] = EditorGUIUtility.FindTexture("Texture Icon");
            _nodeIcons[NodeType.Material] = EditorGUIUtility.FindTexture("Material Icon");
            _nodeIcons[NodeType.Mesh] = EditorGUIUtility.FindTexture("Mesh Icon");
            _nodeIcons[NodeType.Audio] = EditorGUIUtility.FindTexture("AudioClip Icon");
            _nodeIcons[NodeType.Scene] = EditorGUIUtility.FindTexture("SceneAsset Icon");
            _nodeIcons[NodeType.ScriptableObject] = EditorGUIUtility.FindTexture("ScriptableObject Icon");
        }

        private void InitializeVisibleTypes()
        {
            foreach (NodeType type in Enum.GetValues(typeof(NodeType)))
            {
                _visibleTypes.Add(type);
            }
        }

        #endregion

        #region Dependency Analysis

        private void AnalyzeDependencies()
        {
            EditorUtility.DisplayProgressBar("Analyzing Dependencies", "Collecting assets...", 0);

            try
            {
                _nodes.Clear();
                _links.Clear();
                _circularReferences.Clear();
                _bundleAnalyses.Clear();

                // 모든 Addressable 에셋 수집
                CollectAddressableAssets();

                // 종속성 분석
                AnalyzeAssetDependencies();

                // 순환 참조 검출
                DetectCircularReferences();

                // 번들 분석
                AnalyzeBundles();

                // 레이아웃 계산
                if (_autoArrange)
                {
                    ArrangeNodes();
                }

                _lastAnalysisTime = Time.realtimeSinceStartup;

                Debug.Log($"Analysis complete: {_nodes.Count} nodes, {_links.Count} links, {_circularReferences.Count} circular references");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void CollectAddressableAssets()
        {
            _totalAssetCount = 0;
            _totalAddressableSize = 0;

            foreach (var group in _settings.groups)
            {
                if (group == null) continue;

                foreach (var entry in group.entries)
                {
                    string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (string.IsNullOrEmpty(path)) continue;

                    var node = CreateAssetNode(entry, group, path);
                    _nodes[entry.address] = node;

                    _totalAssetCount++;
                    _totalAddressableSize += node.FileSize;

                    EditorUtility.DisplayProgressBar("Analyzing Dependencies",
                        $"Collecting assets... {_totalAssetCount}",
                        (float)_totalAssetCount / 1000);
                }
            }
        }

        private AssetNode CreateAssetNode(AddressableAssetEntry entry, AddressableAssetGroup group, string path)
        {
            var node = new AssetNode
            {
                Address = entry.address,
                GroupName = group.name,
                AssetPath = path,
                Guid = entry.guid,
                Type = GetNodeType(path),
                Position = new Vector2(UnityEngine.Random.Range(100, 700), UnityEngine.Random.Range(100, 500))
            };

            // 파일 크기 계산
            var fileInfo = new System.IO.FileInfo(path);
            if (fileInfo.Exists)
            {
                node.FileSize = fileInfo.Length;
                node.CompressedSize = EstimateCompressedSize(node.FileSize, node.Type);
            }

            // 노드 색상 설정
            node.NodeColor = GetNodeColor(node.Type);

            return node;
        }

        private void AnalyzeAssetDependencies()
        {
            int processedCount = 0;

            foreach (var kvp in _nodes)
            {
                var node = kvp.Value;
                var dependencies = AssetDatabase.GetDependencies(node.AssetPath, false);

                foreach (var depPath in dependencies)
                {
                    // Addressable 에셋인지 확인
                    var depGuid = AssetDatabase.AssetPathToGUID(depPath);
                    var depNode = _nodes.Values.FirstOrDefault(n => n.Guid == depGuid);

                    if (depNode != null && depNode != node)
                    {
                        node.Dependencies.Add(depNode);
                        depNode.ReferencedBy.Add(node);

                        _links.Add(new DependencyLink
                        {
                            Source = node,
                            Target = depNode,
                            Type = LinkType.Direct,
                            Weight = 1.0f
                        });
                    }
                }

                node.DirectDependencyCount = node.Dependencies.Count;
                node.TotalDependencyCount = CalculateTotalDependencies(node);

                processedCount++;
                EditorUtility.DisplayProgressBar("Analyzing Dependencies",
                    $"Processing dependencies... {processedCount}/{_nodes.Count}",
                    (float)processedCount / _nodes.Count);
            }
        }

        private int CalculateTotalDependencies(AssetNode node, HashSet<AssetNode> visited = null)
        {
            if (visited == null)
                visited = new HashSet<AssetNode>();

            if (visited.Contains(node))
                return 0;

            visited.Add(node);

            int count = node.Dependencies.Count;
            foreach (var dep in node.Dependencies)
            {
                count += CalculateTotalDependencies(dep, visited);
            }

            return count;
        }

        private void DetectCircularReferences()
        {
            var visited = new HashSet<AssetNode>();
            var recursionStack = new HashSet<AssetNode>();

            foreach (var node in _nodes.Values)
            {
                if (!visited.Contains(node))
                {
                    DetectCircularReferencesRecursive(node, visited, recursionStack, new List<AssetNode>());
                }
            }
        }

        private void DetectCircularReferencesRecursive(AssetNode node, HashSet<AssetNode> visited,
            HashSet<AssetNode> recursionStack, List<AssetNode> currentPath)
        {
            visited.Add(node);
            recursionStack.Add(node);
            currentPath.Add(node);

            foreach (var dependency in node.Dependencies)
            {
                if (!visited.Contains(dependency))
                {
                    DetectCircularReferencesRecursive(dependency, visited, recursionStack, new List<AssetNode>(currentPath));
                }
                else if (recursionStack.Contains(dependency))
                {
                    // 순환 참조 발견
                    var circularPath = new List<AssetNode>();
                    int startIndex = currentPath.IndexOf(dependency);

                    for (int i = startIndex; i < currentPath.Count; i++)
                    {
                        circularPath.Add(currentPath[i]);
                        currentPath[i].CircularReferenceCount++;
                    }

                    _circularReferences.Add(new CircularReference
                    {
                        Nodes = circularPath,
                        Severity = CalculateSeverity(circularPath),
                        Description = GenerateCircularDescription(circularPath)
                    });

                    // 순환 링크 표시
                    foreach (var link in _links)
                    {
                        if (circularPath.Contains(link.Source) && circularPath.Contains(link.Target))
                        {
                            link.Type = LinkType.Circular;
                        }
                    }
                }
            }

            recursionStack.Remove(node);
        }

        private int CalculateSeverity(List<AssetNode> circularPath)
        {
            // 순환 참조의 심각도 계산
            int severity = 1;

            if (circularPath.Count > 5) severity++;
            if (circularPath.Any(n => n.Type == NodeType.Scene)) severity++;
            if (circularPath.Sum(n => n.FileSize) > 10 * 1024 * 1024) severity++; // 10MB 이상
            if (circularPath.Any(n => n.ReferencedBy.Count > 10)) severity++;

            return Math.Min(severity, 5);
        }

        private string GenerateCircularDescription(List<AssetNode> circularPath)
        {
            var names = circularPath.Select(n => System.IO.Path.GetFileNameWithoutExtension(n.AssetPath));
            return string.Join(" → ", names) + " → " + System.IO.Path.GetFileNameWithoutExtension(circularPath[0].AssetPath);
        }

        private void AnalyzeBundles()
        {
            foreach (var group in _settings.groups)
            {
                if (group == null) continue;

                var analysis = new BundleAnalysis
                {
                    GroupName = group.name,
                    AssetCount = group.entries.Count
                };

                foreach (var entry in group.entries)
                {
                    var node = _nodes.Values.FirstOrDefault(n => n.Guid == entry.guid);
                    if (node == null) continue;

                    analysis.EstimatedSize += node.FileSize;
                    analysis.CompressedSize += node.CompressedSize;

                    if (!analysis.TypeDistribution.ContainsKey(node.Type))
                        analysis.TypeDistribution[node.Type] = 0;
                    analysis.TypeDistribution[node.Type]++;

                    if (analysis.TopAssets.Count < 5)
                    {
                        analysis.TopAssets.Add(node.Address);
                    }
                }

                if (analysis.EstimatedSize > 0)
                {
                    analysis.CompressionRatio = (float)analysis.CompressedSize / analysis.EstimatedSize;
                }

                _bundleAnalyses[group.name] = analysis;
            }
        }

        #endregion

        #region Layout

        private void ArrangeNodes()
        {
            switch (_viewMode)
            {
                case ViewMode.Hierarchy:
                    ArrangeHierarchical();
                    break;
                case ViewMode.ForceDirected:
                    ArrangeForceDirected();
                    break;
                case ViewMode.Circular:
                    ArrangeCircular();
                    break;
                case ViewMode.Grid:
                    ArrangeGrid();
                    break;
                case ViewMode.TreeMap:
                    ArrangeTreeMap();
                    break;
            }
        }

        private void ArrangeHierarchical()
        {
            // 루트 노드 찾기 (참조되지 않는 노드)
            var roots = _nodes.Values.Where(n => n.ReferencedBy.Count == 0).ToList();

            float yOffset = 50;
            float xSpacing = 150;

            foreach (var root in roots)
            {
                ArrangeHierarchicalRecursive(root, 50, yOffset, xSpacing, 0, new HashSet<AssetNode>());
                yOffset += 200;
            }
        }

        private float ArrangeHierarchicalRecursive(AssetNode node, float x, float y, float xSpacing,
            int depth, HashSet<AssetNode> visited)
        {
            if (visited.Contains(node))
                return y;

            visited.Add(node);

            node.Position = new Vector2(x + depth * xSpacing, y);
            node.Depth = depth;

            float childY = y;
            foreach (var child in node.Dependencies)
            {
                childY = ArrangeHierarchicalRecursive(child, x, childY, xSpacing, depth + 1, visited);
                childY += 50;
            }

            return Math.Max(y, childY);
        }

        private void ArrangeForceDirected()
        {
            // Force-directed 레이아웃 알고리즘
            int iterations = 100;
            float springLength = 100f;
            float springStrength = 0.1f;
            float repulsionStrength = 1000f;

            for (int i = 0; i < iterations; i++)
            {
                var forces = new Dictionary<AssetNode, Vector2>();

                foreach (var node in _nodes.Values)
                {
                    forces[node] = Vector2.zero;

                    // Repulsion forces
                    foreach (var other in _nodes.Values)
                    {
                        if (other == node) continue;

                        Vector2 diff = node.Position - other.Position;
                        float distance = diff.magnitude;

                        if (distance > 0 && distance < 200)
                        {
                            forces[node] += diff.normalized * (repulsionStrength / (distance * distance));
                        }
                    }

                    // Spring forces
                    foreach (var connected in node.Dependencies.Concat(node.ReferencedBy))
                    {
                        Vector2 diff = connected.Position - node.Position;
                        float distance = diff.magnitude;
                        float displacement = distance - springLength;

                        forces[node] += diff.normalized * (displacement * springStrength);
                    }
                }

                // Apply forces
                foreach (var kvp in forces)
                {
                    kvp.Key.Position += kvp.Value;

                    // Keep within bounds
                    kvp.Key.Position.Set(Mathf.Clamp(kvp.Key.Position.x, 50, _graphRect.width - 50), Mathf.Clamp(kvp.Key.Position.y, 50, _graphRect.height - 50));
                }
            }
        }

        private void ArrangeCircular()
        {
            var nodeList = _nodes.Values.ToList();
            float centerX = _graphRect.width / 2;
            float centerY = _graphRect.height / 2;
            float radius = Math.Min(centerX, centerY) - 100;

            for (int i = 0; i < nodeList.Count; i++)
            {
                float angle = (float)(2 * Math.PI * i / nodeList.Count);
                float x = centerX + radius * Mathf.Cos(angle);
                float y = centerY + radius * Mathf.Sin(angle);

                nodeList[i].Position = new Vector2(x, y);
            }
        }

        private void ArrangeGrid()
        {
            var nodeList = _nodes.Values.OrderByDescending(n => n.FileSize).ToList();
            int columns = Mathf.CeilToInt(Mathf.Sqrt(nodeList.Count));
            float cellWidth = (_graphRect.width - 100) / columns;
            float cellHeight = cellWidth;

            for (int i = 0; i < nodeList.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;

                float x = 50 + col * cellWidth + cellWidth / 2;
                float y = 50 + row * cellHeight + cellHeight / 2;

                nodeList[i].Position = new Vector2(x, y);
            }
        }

        private void ArrangeTreeMap()
        {
            // TreeMap 레이아웃 (크기 기반)
            var sortedNodes = _nodes.Values.OrderByDescending(n => n.FileSize).ToList();

            float x = 50;
            float y = 50;
            float width = _graphRect.width - 100;
            float height = _graphRect.height - 100;

            TreeMapLayout(sortedNodes, x, y, width, height);
        }

        private void TreeMapLayout(List<AssetNode> nodes, float x, float y, float width, float height)
        {
            if (nodes.Count == 0) return;

            if (nodes.Count == 1)
            {
                nodes[0].Position = new Vector2(x + width / 2, y + height / 2);
                return;
            }

            long totalSize = nodes.Sum(n => n.FileSize);
            float currentX = x;
            float currentY = y;

            bool horizontal = width > height;

            foreach (var node in nodes)
            {
                float ratio = (float)node.FileSize / totalSize;

                if (horizontal)
                {
                    float nodeWidth = width * ratio;
                    node.Position = new Vector2(currentX + nodeWidth / 2, y + height / 2);
                    currentX += nodeWidth;
                }
                else
                {
                    float nodeHeight = height * ratio;
                    node.Position = new Vector2(x + width / 2, currentY + nodeHeight / 2);
                    currentY += nodeHeight;
                }
            }
        }

        #endregion

        #region Drawing

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 분석 버튼
            if (GUILayout.Button("Analyze", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                AnalyzeDependencies();
            }

            // 뷰 모드
            EditorGUI.BeginChangeCheck();
            _viewMode = (ViewMode)EditorGUILayout.EnumPopup(_viewMode, EditorStyles.toolbarPopup, GUILayout.Width(100));
            if (EditorGUI.EndChangeCheck())
            {
                ArrangeNodes();
            }

            // 필터 모드
            _filterMode = (FilterMode)EditorGUILayout.EnumPopup(_filterMode, EditorStyles.toolbarPopup, GUILayout.Width(100));

            // 그룹 선택
            var groups = _settings.groups.ToList();
            var groupNames = groups.Select(g => g.name).ToList();
            groupNames.Insert(0, "All Groups");

            int selectedIndex = _selectedGroup != null ? groups.IndexOf(_selectedGroup) + 1 : 0;
            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup(selectedIndex, groupNames.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(150));
            if (EditorGUI.EndChangeCheck())
            {
                _selectedGroup = selectedIndex > 0 ? groups[selectedIndex - 1] : null;
                ApplyFilters();
            }

            // 검색
            EditorGUI.BeginChangeCheck();
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck())
            {
                ApplyFilters();
            }

            GUILayout.FlexibleSpace();

            // 옵션 토글
            _showLabels = GUILayout.Toggle(_showLabels, "Labels", EditorStyles.toolbarButton);
            _showSizes = GUILayout.Toggle(_showSizes, "Sizes", EditorStyles.toolbarButton);
            _showGrid = GUILayout.Toggle(_showGrid, "Grid", EditorStyles.toolbarButton);
            _highlightCircular = GUILayout.Toggle(_highlightCircular, "Circular", EditorStyles.toolbarButton);

            // 줌 컨트롤
            GUILayout.Label("Zoom:", EditorStyles.miniLabel);
            _zoomLevel = GUILayout.HorizontalSlider(_zoomLevel, 0.5f, 2.0f, GUILayout.Width(100));

            if (GUILayout.Button("Reset", EditorStyles.toolbarButton))
            {
                ResetView();
            }

            EditorGUILayout.EndHorizontal();

            // 통계 바
            DrawStatisticsBar();
        }

        private void DrawStatisticsBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label($"Total Assets: {_totalAssetCount}", EditorStyles.miniLabel);
            GUILayout.Label($"Total Size: {FormatBytes(_totalAddressableSize)}", EditorStyles.miniLabel);
            GUILayout.Label($"Groups: {_settings.groups.Count}", EditorStyles.miniLabel);

            if (_circularReferences.Count > 0)
            {
                GUI.color = Color.yellow;
                GUILayout.Label($"⚠ Circular References: {_circularReferences.Count}", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }

            GUILayout.FlexibleSpace();

            if (_lastAnalysisTime > 0)
            {
                GUILayout.Label($"Last Analysis: {FormatTime(Time.realtimeSinceStartup - _lastAnalysisTime)} ago", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGraphArea()
        {
            _graphRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _graphRect.width *= _splitPosition;

            GUI.Box(_graphRect, GUIContent.none);

            // 배경 그리기
            DrawBackground();

            // 그리드 그리기
            if (_showGrid)
            {
                DrawGrid();
            }

            // 변환 적용
            var matrix = GUI.matrix;
            var pivotPoint = new Vector2(_graphRect.center.x, _graphRect.center.y);
            GUIUtility.ScaleAroundPivot(new Vector2(_zoomLevel, _zoomLevel), pivotPoint);

            // 링크 그리기
            DrawLinks();

            // 노드 그리기
            DrawNodes();

            // 변환 복원
            GUI.matrix = matrix;

            // 오버레이 정보
            DrawOverlay();
        }

        private void DrawBackground()
        {
            EditorGUI.DrawRect(_graphRect, _backgroundColor);
        }

        private void DrawGrid()
        {
            float gridSize = 50 * _zoomLevel;
            int columns = Mathf.CeilToInt(_graphRect.width / gridSize);
            int rows = Mathf.CeilToInt(_graphRect.height / gridSize);

            Handles.color = _gridColor;

            for (int i = 0; i <= columns; i++)
            {
                float x = _graphRect.x + i * gridSize + _panOffset.x % gridSize;
                Handles.DrawLine(new Vector3(x, _graphRect.y), new Vector3(x, _graphRect.yMax));
            }

            for (int i = 0; i <= rows; i++)
            {
                float y = _graphRect.y + i * gridSize + _panOffset.y % gridSize;
                Handles.DrawLine(new Vector3(_graphRect.x, y), new Vector3(_graphRect.xMax, y));
            }
        }

        private void DrawLinks()
        {
            foreach (var link in _links)
            {
                if (!IsNodeVisible(link.Source) || !IsNodeVisible(link.Target))
                    continue;

                Vector2 start = _graphRect.position + link.Source.Position + _panOffset;
                Vector2 end = _graphRect.position + link.Target.Position + _panOffset;

                Color linkColor = GetLinkColor(link);
                float thickness = GetLinkThickness(link);

                if (link.Type == LinkType.Circular && _highlightCircular)
                {
                    // 순환 참조 강조
                    DrawAnimatedLine(start, end, linkColor, thickness);
                }
                else
                {
                    Handles.color = linkColor;
                    Handles.DrawLine(start, end);
                }

                // 화살표 그리기
                DrawArrow(start, end, linkColor);
            }
        }

        private void DrawNodes()
        {
            foreach (var node in _nodes.Values)
            {
                if (!IsNodeVisible(node))
                    continue;

                Vector2 position = _graphRect.position + node.Position + _panOffset;

                // 노드 크기 계산
                float nodeSize = CalculateNodeSize(node);
                Rect nodeRect = new Rect(position.x - nodeSize / 2, position.y - nodeSize / 2, nodeSize, nodeSize);

                // 선택 상태 표시
                if (node.IsSelected || node == _selectedNode)
                {
                    DrawNodeSelection(nodeRect);
                }

                // 노드 그리기
                DrawNode(nodeRect, node);

                // 레이블 그리기
                if (_showLabels)
                {
                    DrawNodeLabel(nodeRect, node);
                }

                // 크기 표시
                if (_showSizes)
                {
                    DrawNodeSize(nodeRect, node);
                }

                // 아이콘 그리기
                DrawNodeIcon(nodeRect, node);
            }
        }

        private void DrawNode(Rect rect, AssetNode node)
        {
            Color nodeColor = node.NodeColor;

            if (node.CircularReferenceCount > 0 && _highlightCircular)
            {
                // 순환 참조 노드 강조
                nodeColor = Color.Lerp(nodeColor, Color.red, 0.5f);
            }

            if (node == _hoveredNode)
            {
                nodeColor = Color.Lerp(nodeColor, Color.white, 0.3f);
            }

            EditorGUI.DrawRect(rect, nodeColor);

            // 테두리
            Handles.color = Color.black;
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear, Color.black);
        }

        private void DrawNodeLabel(Rect nodeRect, AssetNode node)
        {
            string label = System.IO.Path.GetFileNameWithoutExtension(node.AssetPath);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.alignment = TextAnchor.UpperCenter;
            labelStyle.fontSize = Mathf.RoundToInt(10 * _zoomLevel);

            Rect labelRect = new Rect(nodeRect.x, nodeRect.yMax + 2, nodeRect.width, 20);
            GUI.Label(labelRect, label, labelStyle);
        }

        private void DrawNodeSize(Rect nodeRect, AssetNode node)
        {
            string sizeText = FormatBytes(node.FileSize);

            GUIStyle sizeStyle = new GUIStyle(EditorStyles.miniLabel);
            sizeStyle.alignment = TextAnchor.LowerCenter;
            sizeStyle.fontSize = Mathf.RoundToInt(8 * _zoomLevel);

            Rect sizeRect = new Rect(nodeRect.x, nodeRect.y - 15, nodeRect.width, 15);
            GUI.Label(sizeRect, sizeText, sizeStyle);
        }

        private void DrawNodeIcon(Rect nodeRect, AssetNode node)
        {
            if (_nodeIcons.ContainsKey(node.Type))
            {
                Texture2D icon = _nodeIcons[node.Type];
                if (icon != null)
                {
                    Rect iconRect = new Rect(nodeRect.center.x - 8, nodeRect.center.y - 8, 16, 16);
                    GUI.DrawTexture(iconRect, icon);
                }
            }
        }

        private void DrawNodeSelection(Rect nodeRect)
        {
            float selectionSize = nodeRect.width + 10;
            Rect selectionRect = new Rect(nodeRect.center.x - selectionSize / 2,
                                         nodeRect.center.y - selectionSize / 2,
                                         selectionSize, selectionSize);

            Handles.color = new Color(0.3f, 0.7f, 1f, 0.5f);
            Handles.DrawSolidRectangleWithOutline(selectionRect,
                new Color(0.3f, 0.7f, 1f, 0.2f),
                new Color(0.3f, 0.7f, 1f, 1f));
        }

        private void DrawArrow(Vector2 start, Vector2 end, Color color)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 right = new Vector2(-direction.y, direction.x);

            Vector2 arrowPoint = end - direction * 20;
            Vector2 arrowLeft = arrowPoint - direction * 10 + right * 5;
            Vector2 arrowRight = arrowPoint - direction * 10 - right * 5;

            Handles.color = color;
            Handles.DrawLine(arrowPoint, arrowLeft);
            Handles.DrawLine(arrowPoint, arrowRight);
        }

        private void DrawAnimatedLine(Vector2 start, Vector2 end, Color color, float thickness)
        {
            float time = (float)EditorApplication.timeSinceStartup;
            float animSpeed = 2f;

            Color animColor = Color.Lerp(color, Color.red, Mathf.Sin(time * animSpeed) * 0.5f + 0.5f);

            Handles.color = animColor;
            Handles.DrawLine(start, end);

            _needsRepaint = true;
        }

        private void DrawOverlay()
        {
            // 미니맵
            DrawMinimap();

            // 범례
            DrawLegend();

            // 순환 참조 경고
            if (_circularReferences.Count > 0 && _highlightCircular)
            {
                DrawCircularReferenceWarning();
            }
        }

        private void DrawMinimap()
        {
            float minimapSize = 150;
            Rect minimapRect = new Rect(_graphRect.xMax - minimapSize - 10, _graphRect.y + 10, minimapSize, minimapSize);

            GUI.Box(minimapRect, GUIContent.none, EditorStyles.helpBox);

            // 미니맵 내용
            float scale = minimapSize / Mathf.Max(_graphRect.width, _graphRect.height);

            foreach (var node in _nodes.Values)
            {
                if (!IsNodeVisible(node)) continue;

                Vector2 miniPos = node.Position * scale;
                Rect miniNodeRect = new Rect(minimapRect.x + miniPos.x - 1, minimapRect.y + miniPos.y - 1, 2, 2);
                EditorGUI.DrawRect(miniNodeRect, node.NodeColor);
            }

            // 현재 뷰포트 표시
            Rect viewportRect = new Rect(
                minimapRect.x - _panOffset.x * scale,
                minimapRect.y - _panOffset.y * scale,
                _graphRect.width * scale / _zoomLevel,
                _graphRect.height * scale / _zoomLevel
            );

            Handles.color = Color.yellow;
            Handles.DrawSolidRectangleWithOutline(viewportRect, Color.clear, Color.yellow);
        }

        private void DrawLegend()
        {
            float legendWidth = 150;
            float legendHeight = 200;
            Rect legendRect = new Rect(_graphRect.x + 10, _graphRect.yMax - legendHeight - 10, legendWidth, legendHeight);

            GUI.Box(legendRect, "Legend", EditorStyles.helpBox);

            float y = legendRect.y + 20;

            foreach (NodeType type in Enum.GetValues(typeof(NodeType)))
            {
                Rect colorRect = new Rect(legendRect.x + 5, y, 15, 15);
                Rect labelRect = new Rect(legendRect.x + 25, y, legendWidth - 30, 15);

                EditorGUI.DrawRect(colorRect, GetNodeColor(type));
                GUI.Label(labelRect, type.ToString(), EditorStyles.miniLabel);

                y += 18;
            }
        }

        private void DrawCircularReferenceWarning()
        {
            Rect warningRect = new Rect(_graphRect.x + 10, _graphRect.y + 10, 300, 100);
            GUI.Box(warningRect, GUIContent.none, EditorStyles.helpBox);

            GUI.color = Color.yellow;
            GUI.Label(new Rect(warningRect.x + 5, warningRect.y + 5, warningRect.width - 10, 20),
                "⚠ Circular References Detected", EditorStyles.boldLabel);
            GUI.color = Color.white;

            float y = warningRect.y + 25;
            foreach (var circular in _circularReferences.Take(3))
            {
                string severity = new string('!', circular.Severity);
                GUI.Label(new Rect(warningRect.x + 5, y, warningRect.width - 10, 20),
                    $"{severity} {circular.Description}", EditorStyles.miniLabel);
                y += 15;
            }

            if (_circularReferences.Count > 3)
            {
                GUI.Label(new Rect(warningRect.x + 5, y, warningRect.width - 10, 20),
                    $"... and {_circularReferences.Count - 3} more", EditorStyles.miniLabel);
            }
        }

        #endregion

        #region Details Panel

        private void DrawDetailsPanel()
        {
            _detailsRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _detailsRect.width *= (1 - _splitPosition);

            GUILayout.BeginArea(_detailsRect);

            _detailsScrollPos = EditorGUILayout.BeginScrollView(_detailsScrollPos);

            if (_selectedNode != null)
            {
                DrawNodeDetails(_selectedNode);
            }
            else
            {
                DrawGeneralStatistics();
            }

            EditorGUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private Vector2 _detailsScrollPos;

        private void DrawNodeDetails(AssetNode node)
        {
            EditorGUILayout.LabelField("Asset Details", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 기본 정보
            EditorGUILayout.LabelField("Address:", node.Address);
            EditorGUILayout.LabelField("Group:", node.GroupName);
            EditorGUILayout.LabelField("Type:", node.Type.ToString());
            EditorGUILayout.LabelField("Path:", node.AssetPath);

            EditorGUILayout.Space();

            // 크기 정보
            EditorGUILayout.LabelField("Size Information", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("File Size:", FormatBytes(node.FileSize));
            EditorGUILayout.LabelField("Compressed:", FormatBytes(node.CompressedSize));
            EditorGUILayout.LabelField("Compression:", $"{(1 - (float)node.CompressedSize / node.FileSize) * 100:F1}%");

            EditorGUILayout.Space();

            // 종속성 정보
            EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Direct:", node.DirectDependencyCount.ToString());
            EditorGUILayout.LabelField("Total:", node.TotalDependencyCount.ToString());
            EditorGUILayout.LabelField("Referenced By:", node.ReferencedBy.Count.ToString());

            if (node.CircularReferenceCount > 0)
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("Circular References:", node.CircularReferenceCount.ToString());
                GUI.color = Color.white;
            }

            EditorGUILayout.Space();

            // 종속성 리스트
            if (node.Dependencies.Count > 0)
            {
                _showDependencies = EditorGUILayout.Foldout(_showDependencies, $"Dependencies ({node.Dependencies.Count})");
                if (_showDependencies)
                {
                    EditorGUI.indentLevel++;
                    foreach (var dep in node.Dependencies)
                    {
                        if (GUILayout.Button(dep.Address, EditorStyles.label))
                        {
                            SelectNode(dep);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }

            // 참조 리스트
            if (node.ReferencedBy.Count > 0)
            {
                _showReferences = EditorGUILayout.Foldout(_showReferences, $"Referenced By ({node.ReferencedBy.Count})");
                if (_showReferences)
                {
                    EditorGUI.indentLevel++;
                    foreach (var reference in node.ReferencedBy)
                    {
                        if (GUILayout.Button(reference.Address, EditorStyles.label))
                        {
                            SelectNode(reference);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space();

            // 액션 버튼
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Select in Project"))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(node.AssetPath);
            }

            if (GUILayout.Button("Ping Asset"))
            {
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(node.AssetPath));
            }

            if (GUILayout.Button("Focus on Node"))
            {
                FocusOnNode(node);
            }

            if (node.CircularReferenceCount > 0 && GUILayout.Button("Show Circular Path"))
            {
                ShowCircularPath(node);
            }
        }

        private bool _showDependencies = true;
        private bool _showReferences = true;

        private void DrawGeneralStatistics()
        {
            EditorGUILayout.LabelField("General Statistics", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 전체 통계
            EditorGUILayout.LabelField("Total Assets:", _totalAssetCount.ToString());
            EditorGUILayout.LabelField("Total Size:", FormatBytes(_totalAddressableSize));
            EditorGUILayout.LabelField("Groups:", _settings.groups.Count.ToString());
            EditorGUILayout.LabelField("Total Links:", _links.Count.ToString());

            EditorGUILayout.Space();

            // 순환 참조
            if (_circularReferences.Count > 0)
            {
                EditorGUILayout.LabelField("Circular References", EditorStyles.boldLabel);

                foreach (var circular in _circularReferences)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    GUI.color = GetSeverityColor(circular.Severity);
                    EditorGUILayout.LabelField($"Severity: {circular.Severity}/5");
                    GUI.color = Color.white;

                    EditorGUILayout.LabelField("Path:", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(circular.Description, EditorStyles.wordWrappedMiniLabel);

                    if (GUILayout.Button("Highlight", GUILayout.Width(80)))
                    {
                        HighlightCircularReference(circular);
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space();
                }
            }

            EditorGUILayout.Space();

            // 그룹별 분석
            EditorGUILayout.LabelField("Bundle Analysis", EditorStyles.boldLabel);

            foreach (var analysis in _bundleAnalyses.Values.OrderByDescending(a => a.EstimatedSize))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField(analysis.GroupName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Assets: {analysis.AssetCount}");
                EditorGUILayout.LabelField($"Size: {FormatBytes(analysis.EstimatedSize)}");
                EditorGUILayout.LabelField($"Compressed: {FormatBytes(analysis.CompressedSize)} ({analysis.CompressionRatio * 100:F1}%)");

                if (analysis.TypeDistribution.Count > 0)
                {
                    EditorGUILayout.LabelField("Type Distribution:", EditorStyles.miniLabel);
                    foreach (var kvp in analysis.TypeDistribution)
                    {
                        EditorGUILayout.LabelField($"  {kvp.Key}: {kvp.Value}", EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }

        #endregion

        #region Event Handling

        private void ProcessEvents()
        {
            Event e = Event.current;

            switch (e.type)
            {
                case EventType.MouseDown:
                    HandleMouseDown(e);
                    break;

                case EventType.MouseUp:
                    HandleMouseUp(e);
                    break;

                case EventType.MouseDrag:
                    HandleMouseDrag(e);
                    break;

                case EventType.ScrollWheel:
                    HandleScrollWheel(e);
                    break;

                case EventType.KeyDown:
                    HandleKeyDown(e);
                    break;
            }

            // 호버 처리
            if (_graphRect.Contains(e.mousePosition))
            {
                UpdateHoveredNode(e.mousePosition);
            }
        }

        private void HandleMouseDown(Event e)
        {
            if (!_graphRect.Contains(e.mousePosition))
                return;

            if (e.button == 0) // 좌클릭
            {
                // 노드 선택
                var clickedNode = GetNodeAtPosition(e.mousePosition);
                if (clickedNode != null)
                {
                    SelectNode(clickedNode);
                }
                else
                {
                    _selectedNode = null;
                    _isDragging = true;
                    _lastMousePosition = e.mousePosition;
                }
            }
            else if (e.button == 1) // 우클릭
            {
                ShowContextMenu(e.mousePosition);
            }

            e.Use();
        }

        private void HandleMouseUp(Event e)
        {
            _isDragging = false;
        }

        private void HandleMouseDrag(Event e)
        {
            if (_isDragging && e.button == 0)
            {
                _panOffset += e.mousePosition - _lastMousePosition;
                _lastMousePosition = e.mousePosition;
                Repaint();
            }
        }

        private void HandleScrollWheel(Event e)
        {
            if (_graphRect.Contains(e.mousePosition))
            {
                float zoomDelta = -e.delta.y * 0.05f;
                _zoomLevel = Mathf.Clamp(_zoomLevel + zoomDelta, 0.5f, 2.0f);
                Repaint();
            }
        }

        private void HandleKeyDown(Event e)
        {
            switch (e.keyCode)
            {
                case KeyCode.Delete:
                    if (_selectedNode != null)
                    {
                        // 선택된 노드 처리
                    }
                    break;

                case KeyCode.F:
                    if (_selectedNode != null)
                    {
                        FocusOnNode(_selectedNode);
                    }
                    break;

                case KeyCode.R:
                    ResetView();
                    break;
            }
        }

        #endregion

        #region Helper Methods

        private void CalculateLayout()
        {
            _toolbarRect = new Rect(0, 0, position.width, EditorGUIUtility.singleLineHeight * 2);
            float remainingHeight = position.height - _toolbarRect.height;

            _graphRect = new Rect(0, _toolbarRect.height, position.width * _splitPosition, remainingHeight);
            _detailsRect = new Rect(_graphRect.xMax, _toolbarRect.height, position.width * (1 - _splitPosition), remainingHeight);
        }

        private AssetNode GetNodeAtPosition(Vector2 mousePos)
        {
            Vector2 localPos = (mousePos - _graphRect.position - _panOffset) / _zoomLevel;

            foreach (var node in _nodes.Values)
            {
                float nodeSize = CalculateNodeSize(node);
                Rect nodeRect = new Rect(node.Position.x - nodeSize / 2, node.Position.y - nodeSize / 2, nodeSize, nodeSize);

                if (nodeRect.Contains(localPos))
                {
                    return node;
                }
            }

            return null;
        }

        private void UpdateHoveredNode(Vector2 mousePos)
        {
            var newHovered = GetNodeAtPosition(mousePos);
            if (newHovered != _hoveredNode)
            {
                _hoveredNode = newHovered;
                Repaint();
            }
        }

        private void SelectNode(AssetNode node)
        {
            _selectedNode = node;
            node.IsSelected = true;

            // 다른 노드 선택 해제
            foreach (var other in _nodes.Values)
            {
                if (other != node)
                {
                    other.IsSelected = false;
                }
            }

            Repaint();
        }

        private void FocusOnNode(AssetNode node)
        {
            _panOffset = -node.Position + new Vector2(_graphRect.width / 2, _graphRect.height / 2);
            _zoomLevel = 1.5f;
            Repaint();
        }

        private void ResetView()
        {
            _panOffset = Vector2.zero;
            _zoomLevel = 1.0f;
            Repaint();
        }

        private void ShowContextMenu(Vector2 position)
        {
            var menu = new GenericMenu();

            var node = GetNodeAtPosition(position);
            if (node != null)
            {
                menu.AddItem(new GUIContent("Select in Project"), false, () =>
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(node.AssetPath);
                });

                menu.AddItem(new GUIContent("Focus"), false, () => FocusOnNode(node));

                menu.AddSeparator("");

                menu.AddItem(new GUIContent("Copy Address"), false, () =>
                {
                    EditorGUIUtility.systemCopyBuffer = node.Address;
                });
            }

            menu.AddItem(new GUIContent("Reset View"), false, ResetView);
            menu.AddItem(new GUIContent("Refresh"), false, AnalyzeDependencies);

            menu.ShowAsContext();
        }

        private void HighlightCircularReference(CircularReference circular)
        {
            // 모든 노드 선택 해제
            foreach (var node in _nodes.Values)
            {
                node.IsSelected = false;
            }

            // 순환 참조 노드 선택
            foreach (var node in circular.Nodes)
            {
                node.IsSelected = true;
            }

            // 첫 번째 노드로 포커스
            if (circular.Nodes.Count > 0)
            {
                FocusOnNode(circular.Nodes[0]);
            }
        }

        private void ShowCircularPath(AssetNode startNode)
        {
            var circular = _circularReferences.FirstOrDefault(c => c.Nodes.Contains(startNode));
            if (circular != null)
            {
                HighlightCircularReference(circular);
            }
        }

        private void ApplyFilters()
        {
            // 필터 적용 로직
            Repaint();
        }

        private bool IsNodeVisible(AssetNode node)
        {
            // 필터 체크
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                if (!node.Address.ToLower().Contains(_searchFilter.ToLower()) &&
                    !node.AssetPath.ToLower().Contains(_searchFilter.ToLower()))
                {
                    return false;
                }
            }

            if (_selectedGroup != null && node.GroupName != _selectedGroup.name)
            {
                return false;
            }

            if (!_visibleTypes.Contains(node.Type))
            {
                return false;
            }

            if (_filterMode == FilterMode.CircularOnly && node.CircularReferenceCount == 0)
            {
                return false;
            }

            if (_filterMode == FilterMode.LargeOnly && node.FileSize < _minFileSize)
            {
                return false;
            }

            return true;
        }

        private float CalculateNodeSize(AssetNode node)
        {
            float baseSize = 30;
            float sizeBonus = Mathf.Log10(node.FileSize / 1024f + 1) * 10; // 크기에 따른 보너스
            return Mathf.Clamp(baseSize + sizeBonus, 20, 80);
        }

        private NodeType GetNodeType(string path)
        {
            string extension = System.IO.Path.GetExtension(path).ToLower();

            switch (extension)
            {
                case ".prefab":
                    return NodeType.Prefab;
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".psd":
                    return NodeType.Texture;
                case ".mat":
                    return NodeType.Material;
                case ".fbx":
                case ".obj":
                case ".mesh":
                    return NodeType.Mesh;
                case ".wav":
                case ".mp3":
                case ".ogg":
                    return NodeType.Audio;
                case ".unity":
                    return NodeType.Scene;
                case ".asset":
                    return NodeType.ScriptableObject;
                default:
                    return NodeType.Other;
            }
        }

        private Color GetNodeColor(NodeType type)
        {
            switch (type)
            {
                case NodeType.Prefab:
                    return new Color(0.2f, 0.6f, 1f, 0.8f);
                case NodeType.Texture:
                    return new Color(1f, 0.6f, 0.2f, 0.8f);
                case NodeType.Material:
                    return new Color(0.8f, 0.2f, 0.8f, 0.8f);
                case NodeType.Mesh:
                    return new Color(0.2f, 0.8f, 0.2f, 0.8f);
                case NodeType.Audio:
                    return new Color(1f, 1f, 0.2f, 0.8f);
                case NodeType.Scene:
                    return new Color(0.6f, 0.2f, 0.2f, 0.8f);
                case NodeType.ScriptableObject:
                    return new Color(0.2f, 0.8f, 0.8f, 0.8f);
                default:
                    return new Color(0.5f, 0.5f, 0.5f, 0.8f);
            }
        }

        private Color GetLinkColor(DependencyLink link)
        {
            switch (link.Type)
            {
                case LinkType.Circular:
                    return Color.red;
                case LinkType.Direct:
                    return new Color(0.5f, 0.5f, 0.5f, 0.5f);
                case LinkType.Indirect:
                    return new Color(0.3f, 0.3f, 0.3f, 0.3f);
                case LinkType.Strong:
                    return new Color(0.7f, 0.7f, 0.7f, 0.8f);
                case LinkType.Weak:
                    return new Color(0.3f, 0.3f, 0.3f, 0.2f);
                default:
                    return Color.gray;
            }
        }

        private float GetLinkThickness(DependencyLink link)
        {
            switch (link.Type)
            {
                case LinkType.Circular:
                    return 3f;
                case LinkType.Strong:
                    return 2f;
                case LinkType.Direct:
                    return 1.5f;
                default:
                    return 1f;
            }
        }

        private Color GetSeverityColor(int severity)
        {
            switch (severity)
            {
                case 1:
                    return Color.green;
                case 2:
                    return Color.yellow;
                case 3:
                    return new Color(1f, 0.5f, 0f); // Orange
                case 4:
                    return new Color(1f, 0.3f, 0f); // Dark Orange
                case 5:
                    return Color.red;
                default:
                    return Color.white;
            }
        }

        private long EstimateCompressedSize(long originalSize, NodeType type)
        {
            float compressionRatio = type switch
            {
                NodeType.Texture => 0.2f,  // 텍스처는 압축률이 높음
                NodeType.Audio => 0.3f,     // 오디오도 압축 가능
                NodeType.Mesh => 0.5f,      // 메시는 중간 정도
                NodeType.Prefab => 0.7f,    // 프리팹은 압축률이 낮음
                NodeType.Scene => 0.6f,     // 씬은 중간
                _ => 0.8f                   // 기타
            };

            return (long)(originalSize * compressionRatio);
        }

        private Texture2D CreateColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        private string FormatTime(float seconds)
        {
            if (seconds < 60)
                return $"{seconds:F0}s";
            if (seconds < 3600)
                return $"{seconds / 60:F0}m";
            return $"{seconds / 3600:F1}h";
        }

        #endregion

        #region Export & Report

        /// <summary>
        /// 분석 결과 내보내기
        /// </summary>
        [MenuItem("Tools/Addressables/Export Dependency Report")]
        public static void ExportReport()
        {
            var window = GetWindow<AddressableDependencyVisualizer>();
            window.GenerateReport();
        }

        private void GenerateReport()
        {
            string reportPath = EditorUtility.SaveFilePanel("Save Dependency Report", "", "dependency_report", "html");
            if (string.IsNullOrEmpty(reportPath))
                return;

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<title>Addressable Dependency Report</title>");
            html.AppendLine("<style>");
            html.AppendLine(@"
                body { font-family: Arial, sans-serif; margin: 20px; }
                h1 { color: #333; }
                h2 { color: #666; border-bottom: 1px solid #ccc; padding-bottom: 5px; }
                table { border-collapse: collapse; width: 100%; margin: 20px 0; }
                th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
                th { background-color: #4CAF50; color: white; }
                tr:nth-child(even) { background-color: #f2f2f2; }
                .warning { background-color: #fff3cd; }
                .error { background-color: #f8d7da; }
                .circular { color: red; font-weight: bold; }
                .chart { margin: 20px 0; }
                .progress-bar { width: 100%; height: 20px; background-color: #f0f0f0; border-radius: 10px; }
                .progress-fill { height: 100%; background-color: #4CAF50; border-radius: 10px; }
            ");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            // 헤더
            html.AppendLine($"<h1>Addressable Dependency Report</h1>");
            html.AppendLine($"<p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            html.AppendLine($"<p>Unity Version: {Application.unityVersion}</p>");

            // 요약
            html.AppendLine("<h2>Summary</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Metric</th><th>Value</th></tr>");
            html.AppendLine($"<tr><td>Total Assets</td><td>{_totalAssetCount}</td></tr>");
            html.AppendLine($"<tr><td>Total Size</td><td>{FormatBytes(_totalAddressableSize)}</td></tr>");
            html.AppendLine($"<tr><td>Groups</td><td>{_settings.groups.Count}</td></tr>");
            html.AppendLine($"<tr><td>Total Dependencies</td><td>{_links.Count}</td></tr>");
            html.AppendLine($"<tr class='error'><td>Circular References</td><td>{_circularReferences.Count}</td></tr>");
            html.AppendLine("</table>");

            // 그룹별 분석
            html.AppendLine("<h2>Group Analysis</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Group</th><th>Assets</th><th>Size</th><th>Compressed</th><th>Ratio</th></tr>");

            foreach (var analysis in _bundleAnalyses.Values.OrderByDescending(a => a.EstimatedSize))
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{analysis.GroupName}</td>");
                html.AppendLine($"<td>{analysis.AssetCount}</td>");
                html.AppendLine($"<td>{FormatBytes(analysis.EstimatedSize)}</td>");
                html.AppendLine($"<td>{FormatBytes(analysis.CompressedSize)}</td>");
                html.AppendLine($"<td>{analysis.CompressionRatio * 100:F1}%</td>");
                html.AppendLine("</tr>");
            }
            html.AppendLine("</table>");

            // 순환 참조
            if (_circularReferences.Count > 0)
            {
                html.AppendLine("<h2 class='error'>Circular References</h2>");
                html.AppendLine("<table>");
                html.AppendLine("<tr><th>Severity</th><th>Path</th><th>Assets Involved</th></tr>");

                foreach (var circular in _circularReferences.OrderByDescending(c => c.Severity))
                {
                    string rowClass = circular.Severity >= 4 ? "error" : circular.Severity >= 2 ? "warning" : "";
                    html.AppendLine($"<tr class='{rowClass}'>");
                    html.AppendLine($"<td>{circular.Severity}/5</td>");
                    html.AppendLine($"<td class='circular'>{circular.Description}</td>");
                    html.AppendLine($"<td>{circular.Nodes.Count}</td>");
                    html.AppendLine("</tr>");
                }
                html.AppendLine("</table>");
            }

            // 대용량 에셋
            html.AppendLine("<h2>Largest Assets</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Address</th><th>Type</th><th>Size</th><th>Dependencies</th><th>Referenced By</th></tr>");

            var largestAssets = _nodes.Values.OrderByDescending(n => n.FileSize).Take(20);
            foreach (var node in largestAssets)
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{node.Address}</td>");
                html.AppendLine($"<td>{node.Type}</td>");
                html.AppendLine($"<td>{FormatBytes(node.FileSize)}</td>");
                html.AppendLine($"<td>{node.TotalDependencyCount}</td>");
                html.AppendLine($"<td>{node.ReferencedBy.Count}</td>");
                html.AppendLine("</tr>");
            }
            html.AppendLine("</table>");

            // 타입별 분포
            html.AppendLine("<h2>Asset Type Distribution</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Type</th><th>Count</th><th>Total Size</th><th>Average Size</th></tr>");

            var typeGroups = _nodes.Values.GroupBy(n => n.Type);
            foreach (var group in typeGroups.OrderByDescending(g => g.Sum(n => n.FileSize)))
            {
                long totalSize = group.Sum(n => n.FileSize);
                int count = group.Count();
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{group.Key}</td>");
                html.AppendLine($"<td>{count}</td>");
                html.AppendLine($"<td>{FormatBytes(totalSize)}</td>");
                html.AppendLine($"<td>{FormatBytes(totalSize / count)}</td>");
                html.AppendLine("</tr>");
            }
            html.AppendLine("</table>");

            // 추천사항
            html.AppendLine("<h2>Recommendations</h2>");
            html.AppendLine("<ul>");

            if (_circularReferences.Count > 0)
            {
                html.AppendLine($"<li class='error'>Fix {_circularReferences.Count} circular references to improve loading performance</li>");
            }

            var oversizedAssets = _nodes.Values.Where(n => n.FileSize > 10 * 1024 * 1024).ToList();
            if (oversizedAssets.Count > 0)
            {
                html.AppendLine($"<li class='warning'>Consider optimizing {oversizedAssets.Count} assets larger than 10MB</li>");
            }

            var heavilyReferenced = _nodes.Values.Where(n => n.ReferencedBy.Count > 20).ToList();
            if (heavilyReferenced.Count > 0)
            {
                html.AppendLine($"<li>Review {heavilyReferenced.Count} heavily referenced assets for potential shared group placement</li>");
            }

            html.AppendLine("</ul>");

            // JavaScript for interactivity
            html.AppendLine(@"
                <script>
                    // Add sorting to tables
                    document.querySelectorAll('table').forEach(table => {
                        const headers = table.querySelectorAll('th');
                        headers.forEach((header, index) => {
                            header.style.cursor = 'pointer';
                            header.onclick = () => sortTable(table, index);
                        });
                    });
                    
                    function sortTable(table, column) {
                        const tbody = table.querySelector('tbody') || table;
                        const rows = Array.from(tbody.querySelectorAll('tr')).slice(1);
                        
                        rows.sort((a, b) => {
                            const aText = a.cells[column].textContent;
                            const bText = b.cells[column].textContent;
                            
                            const aNum = parseFloat(aText);
                            const bNum = parseFloat(bText);
                            
                            if (!isNaN(aNum) && !isNaN(bNum)) {
                                return aNum - bNum;
                            }
                            return aText.localeCompare(bText);
                        });
                        
                        rows.forEach(row => tbody.appendChild(row));
                    }
                </script>
            ");

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            System.IO.File.WriteAllText(reportPath, html.ToString());

            EditorUtility.DisplayDialog("Export Complete",
                $"Dependency report exported to:\n{reportPath}", "OK");

            Application.OpenURL("file:///" + reportPath);
        }

        #endregion

        #region Optimization Suggestions

        /// <summary>
        /// 최적화 제안 생성
        /// </summary>
        public List<string> GenerateOptimizationSuggestions()
        {
            var suggestions = new List<string>();

            // 순환 참조 체크
            if (_circularReferences.Count > 0)
            {
                suggestions.Add($"🔴 Critical: {_circularReferences.Count} circular references detected. These can cause loading delays and memory issues.");

                foreach (var circular in _circularReferences.Take(3))
                {
                    suggestions.Add($"  → {circular.Description}");
                }
            }

            // 대용량 에셋 체크
            var largeAssets = _nodes.Values.Where(n => n.FileSize > 10 * 1024 * 1024).ToList();
            if (largeAssets.Count > 0)
            {
                suggestions.Add($"⚠️ Warning: {largeAssets.Count} assets larger than 10MB found:");
                foreach (var asset in largeAssets.Take(5))
                {
                    suggestions.Add($"  → {asset.Address}: {FormatBytes(asset.FileSize)}");
                }
            }

            // 과도한 종속성 체크
            var heavyDependencies = _nodes.Values.Where(n => n.TotalDependencyCount > 50).ToList();
            if (heavyDependencies.Count > 0)
            {
                suggestions.Add($"⚠️ Warning: {heavyDependencies.Count} assets with excessive dependencies (>50):");
                foreach (var asset in heavyDependencies.Take(5))
                {
                    suggestions.Add($"  → {asset.Address}: {asset.TotalDependencyCount} dependencies");
                }
            }

            // 중복 가능성 체크
            var duplicateCandidates = _nodes.Values
                .GroupBy(n => new { n.Type, n.FileSize })
                .Where(g => g.Count() > 1)
                .OrderByDescending(g => g.Count())
                .Take(5);

            if (duplicateCandidates.Any())
            {
                suggestions.Add("💡 Tip: Potential duplicate assets detected:");
                foreach (var group in duplicateCandidates)
                {
                    suggestions.Add($"  → {group.Count()} {group.Key.Type} assets with size {FormatBytes(group.Key.FileSize)}");
                }
            }

            // 그룹 밸런싱 체크
            var unbalancedGroups = _bundleAnalyses.Values
                .Where(b => b.EstimatedSize > 50 * 1024 * 1024 || b.EstimatedSize < 1024 * 1024)
                .ToList();

            if (unbalancedGroups.Count > 0)
            {
                suggestions.Add($"💡 Tip: {unbalancedGroups.Count} groups may need rebalancing:");
                foreach (var group in unbalancedGroups)
                {
                    string issue = group.EstimatedSize > 50 * 1024 * 1024 ? "too large" : "too small";
                    suggestions.Add($"  → {group.GroupName}: {FormatBytes(group.EstimatedSize)} ({issue})");
                }
            }

            // 공유 리소스 체크
            var sharedCandidates = _nodes.Values
                .Where(n => n.ReferencedBy.Count > 10)
                .OrderByDescending(n => n.ReferencedBy.Count)
                .Take(5);

            if (sharedCandidates.Any())
            {
                suggestions.Add("💡 Tip: Consider moving frequently shared assets to a common group:");
                foreach (var asset in sharedCandidates)
                {
                    suggestions.Add($"  → {asset.Address}: referenced by {asset.ReferencedBy.Count} assets");
                }
            }

            return suggestions;
        }

        #endregion
    }
}
#endif