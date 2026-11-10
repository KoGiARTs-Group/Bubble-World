using UnityEngine;

namespace BubbleWorld
{
    public sealed class BubbleWorldEditorView
    {
        private readonly BubbleWorldGrid grid;

        public BubbleWorldEditorView(BubbleWorldGrid grid)
        {
            this.grid = grid;
        }

        public void Show(BubbleModel model)
        {
            grid.Show(model);
        }

        public void RefreshCells(BubbleModel model)
        {
            grid.RefreshAll();
        }

        public bool TryGetCellUnderPointer(out int x, out int y, out int z)
        {
            return grid.TryGetCellUnderPointer(out x, out y, out z);
        }

        public void TickCamera()
        {
            grid.TickCamera();
        }

        public void Zoom(float delta)
        {
            grid.Zoom(delta);
        }

        public void CenterCamera()
        {
            grid.CenterCamera();
        }
    }
}
