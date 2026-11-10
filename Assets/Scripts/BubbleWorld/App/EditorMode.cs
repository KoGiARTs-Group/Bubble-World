using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BubbleWorld
{
    public sealed class EditorMode : IAppMode
    {
        public BubbleUndoRedo UndoRedo { get; private set; } = new BubbleUndoRedo();

        public void OnEnter(BubbleWorldApp app)
        {
            app.ShowEditorUi();
        }

        public void OnUpdate(BubbleWorldApp app)
        {
            if (app.Bubble == null || app.Grid == null) return;

            app.Grid.TickCamera();

            BubbleWorldGrid grid = app.Grid;
            BubbleModel bubble = app.Bubble;
            EditorState state = app.State;

            bool showHighlight = state.ActiveTool == EditorTool.AddPlace
                || state.ActiveTool == EditorTool.Delete;
            if (showHighlight && grid.CellUnderCursor(out int hx, out int hy, out int hz))
            {
                if (state.ActiveTool == EditorTool.AddPlace)
                    grid.CursorYOverride = bubble.FindStackY(hx, hz, BubbleModel.MaxStackHeight);
                else
                    grid.CursorYOverride = bubble.FindTopY(hx, hz, BubbleModel.MaxStackHeight);
            }
            else
            {
                grid.CursorYOverride = -1;
            }
            grid.UpdateCursorHighlight(showHighlight ? state.SelectedItem : null);

            if (Input.GetMouseButtonDown(0)
                && !EventSystem.current.IsPointerOverGameObject()
                && grid.TryGetCellUnderPointer(out int x, out int y, out int z))
            {
                if (state.ActiveTool == EditorTool.AddPlace)
                {
                    ExecutePaint(app, bubble, grid, state, x, 0, z, false);
                    if (app.Audio != null) app.Audio.PlayBrushPlace();
                }
                else if (state.ActiveTool == EditorTool.Delete)
                {
                    ExecutePaint(app, bubble, grid, state, x, 0, z, true);
                    if (app.Audio != null) app.Audio.PlayBrushRemove();
                }
                else if (state.ActiveTool == EditorTool.Select)
                {
                    BubbleCell cell = bubble.Get(x, y, z);
                    string cellInfo = cell != null
                        ? BubblePlacementCatalog.Find(cell.ItemId)?.DisplayName ?? cell.ItemId
                        : "Empty";
                    if (app.EditorScreen != null) app.EditorScreen.UpdateLabel("SelName", cellInfo);
                    if (cell != null)
                    {
                        BubblePlacementDefinition def = BubblePlacementCatalog.Find(cell.ItemId);
                        if (def != null) state.SelectedItem = def;
                    }
                    if (app.Audio != null) app.Audio.PlaySelect();
                }
                else if (state.ActiveTool == EditorTool.Rotate)
                {
                    var before = BubbleUndoRedo.SnapshotRegion(bubble, x, y, z, BrushSize.One, true);
                    app.RotateEntity(x, y, z, state.SelectedRotation);
                    UndoRedo.Record(BubbleUndoRedo.BuildAction(before, bubble, x, y, z, BrushSize.One));
                    grid.RefreshAll();
                    app.RefreshLivability();
                    if (app.Audio != null) app.Audio.PlaySelect();
                }
            }

            if (Input.GetMouseButtonDown(1)
                && !EventSystem.current.IsPointerOverGameObject()
                && grid.TryGetCellUnderPointer(out int rx, out int ry, out int rz)
                && (state.ActiveTool == EditorTool.AddPlace || state.ActiveTool == EditorTool.Delete))
            {
                ExecutePaint(app, bubble, grid, state, rx, 0, rz, true);
            }

            if (state.ActiveTool == EditorTool.MoveView && Input.GetMouseButton(1))
            {
                float dx = Input.GetAxis("Mouse X");
                float dy = Input.GetAxis("Mouse Y");
                if (Mathf.Abs(dx) > 0.01f || Mathf.Abs(dy) > 0.01f)
                    grid.Orbit(dx, dy);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (app.Audio != null) app.Audio.PlayUiBack();
                app.ShowTitle();
            }

            if (Input.GetKeyDown(KeyCode.Z) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    app.RedoAction();
                else
                    app.UndoAction();
            }

            if (Input.GetKeyDown(KeyCode.Y) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                app.RedoAction();
            }
        }

        public void OnExit(BubbleWorldApp app)
        {
        }

        private static bool IsAutoTileLayer(BubblePlacementDefinition item)
        {
            return item != null && item.Layer == LayerBand.GroundWaterBase;
        }

        private void ExecutePaint(BubbleWorldApp app, BubbleModel bubble,
            BubbleWorldGrid grid, EditorState state, int x, int y, int z, bool remove)
        {
            var before = BubbleUndoRedo.SnapshotRegion(bubble, x, z, state.BrushSize, BubbleModel.MaxStackHeight, true);

            if (remove)
                bubble.ApplyBrush(x, y, z, state.BrushSize, null, true);
            else
                bubble.ApplyBrush(x, y, z, state.BrushSize, state.SelectedItem, state.RemoveMode);

            bool needsAutoTile = !remove
                ? IsAutoTileLayer(state.SelectedItem)
                : true;

            HashSet<(int x, int y, int z)> dirty = null;
            if (needsAutoTile)
            {
                dirty = bubble.CollectDirtyRegion(x, y, z, state.BrushSize);
                ApplyAutoTile(bubble, dirty);
            }

            UndoRedo.Record(BubbleUndoRedo.BuildAction(before, bubble, x, z, state.BrushSize, BubbleModel.MaxStackHeight));

            if (dirty != null)
                grid.RefreshCells(dirty);
            else
                grid.RefreshAll();

            app.RefreshLivability();
        }

        private static void ApplyAutoTile(BubbleModel bubble, HashSet<(int x, int y, int z)> dirty)
        {
            foreach (var (cx, cy, cz) in dirty)
            {
                BubbleCell cell = bubble.Get(cx, cy, cz);
                if (cell != null && cell.Layer == LayerBand.GroundWaterBase)
                    AutoTileSystem.ApplyToCell(bubble, cx, cy, cz);
            }
        }
    }
}
