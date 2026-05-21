using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Placement
{
    public static class PungentPlacementGridUtility
    {
        public static Vector2Int WorldToCell(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            float safeCell = Mathf.Max(0.01f, cellSize);
            return new Vector2Int(
                Mathf.RoundToInt((worldPosition.x - origin.x) / safeCell),
                Mathf.RoundToInt((worldPosition.z - origin.z) / safeCell));
        }

        public static Vector3 CellToWorld(Vector2Int cell, Vector3 origin, float cellSize, float y)
        {
            float safeCell = Mathf.Max(0.01f, cellSize);
            return new Vector3(origin.x + cell.x * safeCell, y, origin.z + cell.y * safeCell);
        }

        public static Vector3 SnapWorld(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            Vector2Int cell = WorldToCell(worldPosition, origin, cellSize);
            return CellToWorld(cell, origin, cellSize, worldPosition.y);
        }

        public static List<Vector2Int> GetFootprintCells(Vector2Int anchorCell, PungentPlacementFootprint footprint, float yawDegrees)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            if (footprint == null || !footprint.useFootprint)
            {
                cells.Add(anchorCell);
                return cells;
            }

            Vector2Int size = footprint.GetRotatedSize(yawDegrees);
            Vector2Int pivot = footprint.pivot;
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                    cells.Add(new Vector2Int(anchorCell.x + x - pivot.x, anchorCell.y + y - pivot.y));
            }

            return cells;
        }

        public static bool FootprintOverlaps(HashSet<Vector2Int> occupied, IEnumerable<Vector2Int> footprintCells)
        {
            if (occupied == null || footprintCells == null)
                return false;

            foreach (Vector2Int cell in footprintCells)
            {
                if (occupied.Contains(cell))
                    return true;
            }

            return false;
        }

        public static void MarkOccupied(HashSet<Vector2Int> occupied, IEnumerable<Vector2Int> footprintCells)
        {
            if (occupied == null || footprintCells == null)
                return;

            foreach (Vector2Int cell in footprintCells)
                occupied.Add(cell);
        }
    }

}