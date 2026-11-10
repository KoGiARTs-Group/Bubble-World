using System;

namespace BubbleWorld
{
    public sealed class EditorState
    {
        public string WorldName = "My World";
        public BubblePlacementDefinition SelectedItem;
        public BrushSize BrushSize = BrushSize.One;
        public BuildTab SelectedTab = BuildTab.Terrain;
        public bool RemoveMode;
        public bool LivabilityExpanded;
        public bool ShowAnglePanel;
        public int CameraAngle = 180;
        public Camera3DView Current3DView = Camera3DView.Angle45;
        public EditorTool ActiveTool = EditorTool.Select;
        public int ActiveLayerIndex;
        public int SelectedRotation;
        public bool ShowLayerPanel = false;

        public Action OnSave;
        public Action OnLoad;
        public Action OnUndo;
        public Action OnRedo;

        public void SetBrushSize(BrushSize size, Action refresh)
        {
            BrushSize = size;
            refresh?.Invoke();
        }

        public string BrushSizeLabel()
        {
            return "Brush: " + (int)BrushSize + "x" + (int)BrushSize;
        }
    }
}
