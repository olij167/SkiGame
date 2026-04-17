#if UNITY_EDITOR
using System;
using System.Linq;
using Mighty;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Mighty.MightyWindowManagerStateful;
using static Mighty.MightyCoreData;

namespace Mighty
{
#if UNITY_6000_0_OR_NEWER
    [UxmlElement]
#endif
    public partial class MightyWindowStateful : VisualElement
    {
        public MightyWindowStateful()
        {
        }

#if UNITY_2023_3_OR_NEWER && !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<MightyWindowStateful, UxmlTraits> { }
#endif

        private readonly Vector2 _maxSize;
        private readonly Vector2 _minSize;
        private Vector2 preMaximizeSize;
        private Vector2 preMaximizePosition;
        private Vector2 originalSize;
        private bool isMaximized;
        public long ID;
        public VisualElement content;
        public Action GeometryChanged;

        static SerializableWindowState? savedState;
        static string thisWindowTitle;



        public MightyWindowStateful(VisualElement root, Type popOutWindowType, string windowTitle, Vector2 initialPosition, Type initialCommandType)
        {
            thisWindowTitle = windowTitle;

            root.style.backgroundColor = new Color(0, 0, 0, 0);
            var initialSize = new Vector2(root.style.minWidth.value.value, root.style.minHeight.value.value);
            var maxSize = new Vector2(root.style.maxWidth.value.value, root.style.maxHeight.value.value);
            var minSize = new Vector2(root.style.minWidth.value.value + 16, root.style.minHeight.value.value + 16);

            DevLog($"initialSize {initialSize} initialPosition {initialPosition} ");
            ID = DateTime.Now.Ticks;

            {
                DevLog($"initialSize {initialSize} initialPosition {initialPosition} ");
                ID = DateTime.Now.Ticks;


                this.style.position = Position.Absolute;
                if (maxSize == null) _maxSize = new Vector2(root.style.maxWidth.value.value, root.style.maxHeight.value.value);
                if (minSize == null) _minSize = new Vector2(root.style.minWidth.value.value, root.style.minHeight.value.value);
                if (_minSize == null) _minSize = new Vector2(356, 356); else _minSize = (Vector2)minSize; // Minimum allowed window size
                if (_maxSize == null) _maxSize = new Vector2(800, 600); else _maxSize = (Vector2)maxSize; // Maximum allowed window size
                if (initialSize.x < _minSize.x) initialSize.x = _minSize.x;
                if (initialSize.y < _minSize.y) initialSize.y = _minSize.y;
                this.style.width = initialSize.x;
                this.style.height = initialSize.y;
                this.style.flexGrow = 0;
                this.style.flexShrink = 0;
                this.style.left = initialPosition.x;
                this.style.top = initialPosition.y;

                this.name = windowTitle;
                this.style.overflow = Overflow.Hidden;
                VisualElement tileParent = new VisualElement();
                tileParent.name = "tileParent";
                tileParent.style.position = Position.Absolute;



                this.Add(tileParent);

                Texture2D bg = Resources.Load<Texture2D>("UI/bg_poly2");

                int gridSize = 5;
                int tileSize = 200;

                tileParent.style.width = gridSize * tileSize;
                tileParent.style.height = gridSize * tileSize;
                for (int x = 0; x < gridSize; x++)
                {
                    for (int y = 0; y < gridSize; y++)
                    {
                        VisualElement tile = new VisualElement();
                        tile.style.width = tileSize;
                        tile.style.height = tileSize;
                        tile.style.backgroundImage = bg;
                        tile.style.position = Position.Absolute;
                        tile.style.left = x * tileSize;
                        tile.style.top = y * tileSize;

                        tileParent.Add(tile);
                    }
                }

                originalSize = initialSize;

                var titleBar = new VisualElement
                {
                    name = "titleBar",
                    style =
                {
                    flexGrow = 0,
                    flexShrink = 0,
                    flexDirection = FlexDirection.Row,
                    backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
                    height = 24,
                }
                };
                titleBar.AddToClassList("draggable");

                var contentScrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
                {
                    name = "ContentScrollview",
                    style =
                    {
                        flexGrow = 0,
                        flexShrink = 0,
                    }
                };


                content = new VisualElement()
                {
                    style = {
                         flexGrow = 0,
                         flexShrink = 1,
                         overflow= Overflow.Hidden,
                     },
                    name = "content"
                };


                content.Add(root);

                if (MightyCoreData.icons.window_popout == null) MightyCoreData.icons = new MightyCoreData.Icons();
                var popOutButton = new Button(() =>
                {
                    var minSize = new Vector2(this.style.width.value.value, this.style.height.value.value);
                    var maxSize = new Vector2(this.style.width.value.value, this.style.height.value.value);
                    var popOutWindow = GetOrCreateWindow(popOutWindowType, windowTitle, minSize, maxSize, contentScrollView);

                    if (popOutWindow != null)
                    {
                        this.RemoveFromHierarchy();
                        popOutWindow.Show();
                    }
                })
                {
                    text = "",
                    style = { width = 20, height = 20, flexGrow = 0, flexShrink = 0,
                         backgroundImage = MightyCoreData.icons.window_popout, }
                };
                //titleBar.Add(popOutButton);




                var titleLabel = new Label(windowTitle)
                {
                    style = { unityTextAlign = TextAnchor.MiddleCenter, flexGrow = 1, flexShrink = 0 }
                };
                titleBar.Add(titleLabel);

                if (MightyCoreData.icons.window_minimize == null) MightyCoreData.icons = new MightyCoreData.Icons();
                var minButton = new Button(() =>
                {
                    DevLog("Minimize Clicked!");
                    this.style.display = DisplayStyle.None;
                    MightyCoreData.BuildWindowBar();
                })
                {
                    text = "",
                    name = "minButton",
                    style = { width = 20, height = 20, flexGrow = 0, flexShrink = 0,
                             backgroundImage = MightyCoreData.icons.window_minimize,
                             backgroundColor = new Color(0,0,0,0), }
                };
                //titleBar.Add(minButton);

                if (MightyCoreData.icons.window_maximize == null) MightyCoreData.icons = new MightyCoreData.Icons();
                var maxButton = new Button(() =>
                {
                    isMaximized = !isMaximized;
                    if (isMaximized)
                    {
                        preMaximizeSize = new Vector2(this.style.width.value.value, this.style.height.value.value);
                        preMaximizePosition = new Vector2(this.style.left.value.value, this.style.top.value.value);

                        this.style.width = Math.Min(_maxSize.x, MightyCoreData.screenWidth);
                        this.style.height = Math.Min(_maxSize.y, MightyCoreData.screenHeight - 24);
                        this.style.left = 0;
                        this.style.top = 24;
                    }
                    else
                    {
                        this.style.width = preMaximizeSize.x;
                        this.style.height = preMaximizeSize.y;
                        this.style.left = preMaximizePosition.x;
                        this.style.top = preMaximizePosition.y;
                    }
                    this.BringToFront();
                })
                {
                    text = "",
                    style = { width = 16, height = 16, flexGrow = 0, flexShrink = 0,
                         backgroundImage = MightyCoreData.icons.window_maximize,
                             backgroundColor = new Color(0,0,0,0), }
                };
                titleBar.Add(maxButton);

                if (MightyCoreData.icons.window_close == null) MightyCoreData.icons = new MightyCoreData.Icons();
                var closeButton = new Button(() =>
                {
                    this.RemoveFromHierarchy();
                    MightyCoreData.windowManagerStateful.DeregisterWindow(windowTitle);
                })
                {
                    text = "",
                    name = "closeButton",
                    style = { width = 16, height = 16, flexGrow = 0, flexShrink = 0,
                         backgroundImage = MightyCoreData.icons.window_close,
                             backgroundColor = new Color(0,0,0,0), }
                };
                titleBar.Add(closeButton);

                this.Add(titleBar);



                //ontent.style.backgroundColor = Color.black;



                contentScrollView.Add(content);
                this.Add(contentScrollView);


                if (MightyCoreData.icons.window_resize == null) MightyCoreData.icons = new MightyCoreData.Icons();
                var resizer = new VisualElement()
                {
                    style = { width = 16, height = 16, position = Position.Absolute, right = 0, bottom = 0,
                         backgroundImage = MightyCoreData.icons.window_resize,
                             backgroundColor = new Color(0,0,0,0), }
                };
                resizer.AddToClassList("resizable");
                this.Add(resizer);

                titleBar.AddManipulator(new DragManipulator());
                resizer.AddManipulator(new ResizeManipulator());

                this.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

                this.style.display = DisplayStyle.Flex;

                this.GeometryChanged += () =>
                {
                    DevLog($"Window {windowTitle} GeometryChanged! {this.layout.width} {this.layout.height}");
                    root.style.minHeight = this.layout.height - 16;
                    root.style.maxHeight = this.layout.height - 16;
                    root.style.minWidth = this.layout.width - 16;
                    root.style.maxWidth = this.layout.width - 16;

                };

                savedState = MightyCoreData.windowManagerStateful.GetSavedState(windowTitle);


                if (savedState.HasValue && savedState.Value.left + savedState.Value.top + savedState.Value.width + savedState.Value.height > 0)
                {
                    this.style.left = savedState.Value.left;
                    this.style.top = savedState.Value.top;
                    this.style.width = savedState.Value.width;
                    this.style.height = savedState.Value.height;
                }
                else
                {
                    this.style.left = initialPosition.x;
                    this.style.top = initialPosition.y;
                    this.style.width = initialSize.x;
                    this.style.height = initialSize.y;

                    SerializableWindowState initialState = new SerializableWindowState
                    {
                        id = windowTitle,
                        left = initialPosition.x,
                        top = initialPosition.y,
                        width = initialSize.x,
                        height = initialSize.y,
                    };
                    MightyCoreData.windowManagerStateful.ReplaceSavedState(windowTitle, initialState);
                }

                EditorApplication.quitting += SaveWindowState;
                EditorApplication.playModeStateChanged += SaveWindowState;

                AssemblyReloadEvents.beforeAssemblyReload += SaveWindowState;

                if (!MightyCoreData.windowManagerStateful.RegisterWindow(windowTitle, this, initialCommandType))
                {
                    DevLog($"Window {windowTitle} already exists");
                    content = null;
                    return;
                }
            }
        }

        private void SaveWindowState(PlayModeStateChange change)
        {
            SaveWindowState();
        }


        void SaveWindowState()
        {
            if (savedState == null) return;
            var tempState = savedState.Value;
            tempState.firstRun = false;
            tempState.left = this.resolvedStyle.left;
            tempState.top = this.resolvedStyle.top;
            tempState.width = this.resolvedStyle.width;
            tempState.height = this.resolvedStyle.height;

            MightyCoreData.windowManagerStateful.ReplaceSavedState(thisWindowTitle, tempState);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            GeometryChanged?.Invoke();
        }


        public static EditorWindow GetOrCreateWindow(Type windowType, string windowTitle, Vector2 minSize, Vector2 maxSize, ScrollView scrollView)
        {
            if (!typeof(EditorWindow).IsAssignableFrom(windowType))
            {
                DevLogError($"Provided type {windowType.Name} does not inherit from EditorWindow.");
                return null;
            }

            var windows = Resources.FindObjectsOfTypeAll(windowType);
            EditorWindow window = windows.FirstOrDefault() as EditorWindow;

            if (window == null)
            {
                window = ScriptableObject.CreateInstance(windowType) as EditorWindow;

                if (window == null)
                {
                    DevLogError($"Could not create instance of {windowType.Name}.");
                    return null;
                }
            }
            else
            {
                window.rootVisualElement.Clear();
            }

            window.titleContent.text = windowTitle;
            window.minSize = minSize;
            window.maxSize = maxSize;

            (window as IPopOutWindow)?.AddContent(scrollView);

            return window;
        }


        public class DragManipulator : MouseManipulator
        {
            private Vector2 startMousePosition;
            private Vector2 startElementPosition;
            private bool isDragging;

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.NoTrickleDown);
                target.RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.NoTrickleDown);
                target.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.NoTrickleDown);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.NoTrickleDown);
                target.UnregisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.NoTrickleDown);
                target.UnregisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.NoTrickleDown);
            }

            void OnMouseDown(MouseDownEvent evt)
            {
                if (evt.button != 0 || evt.clickCount > 1)
                    return;

                startMousePosition = evt.mousePosition;
                startElementPosition = target.parent.layout.position;
                isDragging = true;
                target.CaptureMouse();
                ((VisualElement)target).parent.BringToFront();
                evt.StopPropagation();

                if (((MightyWindowStateful)target.parent).isMaximized)
                {
                    ((MightyWindowStateful)target.parent).isMaximized = false;
                }
            }

            void OnMouseMove(MouseMoveEvent evt)
            {
                if (!isDragging || !target.HasMouseCapture() || evt.button != 0)
                    return;

                Vector2 diff = evt.mousePosition - startMousePosition;

                float newX = startElementPosition.x + diff.x;
                float newY = startElementPosition.y + diff.y;

                float maxX = Screen.width - 40;
                float maxY = Screen.height - 40;

                ((VisualElement)target).parent.style.left = Mathf.Clamp(newX, 0, maxX);
                ((VisualElement)target).parent.style.top = Mathf.Clamp(newY, 24, maxY);
                evt.StopPropagation();
            }




            void OnMouseUp(MouseUpEvent evt)
            {
                if (isDragging && target.HasMouseCapture() && evt.button == 0)
                {
                    isDragging = false;
                    target.ReleaseMouse();
                    evt.StopPropagation();
                }
            }
        }

        public class ResizeManipulator : MouseManipulator
        {
            private Vector2 startMousePosition;
            private Vector2 startElementSize;
            private bool isResizing;

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.NoTrickleDown);
                target.RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.NoTrickleDown);
                target.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.NoTrickleDown);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.NoTrickleDown);
                target.UnregisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.NoTrickleDown);
                target.UnregisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.NoTrickleDown);
            }

            void OnMouseDown(MouseDownEvent evt)
            {
                if (evt.button != 0 || evt.clickCount > 1)
                    return;

                startMousePosition = evt.mousePosition;
                startElementSize = target.parent.layout.size;
                DevLog($"startElementSize: {startElementSize}");
                isResizing = true;
                target.CaptureMouse();
                evt.StopPropagation();
                ((VisualElement)target).parent.BringToFront();

                if (((MightyWindowStateful)target.parent).isMaximized)
                {
                    ((MightyWindowStateful)target.parent).isMaximized = false;
                }
            }

            void OnMouseMove(MouseMoveEvent evt)
            {
                if (!isResizing || !target.HasMouseCapture() || evt.button != 0)
                    return;

                Vector2 diff = evt.mousePosition - startMousePosition;
                var targetParent = (VisualElement)target.parent;
                if (targetParent is MightyWindowStateful customWindow)
                {
                    DevLog($"customWindow._minSize: {customWindow._minSize}, customWindow._maxSize: {customWindow._maxSize}");
                    float width = Mathf.Clamp(startElementSize.x + diff.x, customWindow._minSize.x, customWindow._maxSize.x);
                    float height = Mathf.Clamp(startElementSize.y + diff.y, customWindow._minSize.y, customWindow._maxSize.y);
                    targetParent.style.width = width;
                    targetParent.style.height = height;
                    DevLog($"width: {width}, height: {height} startElementSize: {startElementSize} diff: {diff}");


                }
                evt.StopPropagation();

            }


            void OnMouseUp(MouseUpEvent evt)
            {
                if (isResizing && target.HasMouseCapture() && evt.button == 0)
                {
                    isResizing = false;
                    target.ReleaseMouse();
                    evt.StopPropagation();
                }
            }
        }


    }
}
#endif