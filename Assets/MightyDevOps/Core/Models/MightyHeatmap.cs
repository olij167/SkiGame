#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using static Mighty.MightyCoreData;


namespace Mighty
{
    public class MightyHeatmap
    {
        public struct HeatmapCell
        {
            public float intensity;
            public float opacity;
            public int count;

            public HeatmapCell(float intensity, float opacity, int count)
            {
                this.intensity = intensity;
                this.opacity = opacity;
                this.count = count;
            }
        }

        public enum AggregationMethod
        {
            Average,
            Median,
            Max,
            Min
        }

        public class Heatmap
        {
            private HeatmapCell[,] grid;
            private float cellSize;
            private int kernelSize;
            private bool relativeFPS;
            private float targetFPS;
            private Color lowFPSColor;
            private Color highFPSColor;
            private float minFPS;
            private float maxFPS;
            private Vector2 gridOrigin;
            private int gridWidth, gridHeight;
            // private float minIntensity;
            // private float maxIntensity;
            private ComputeBuffer gridBuffer;
            private int lastBufferSize;
            private AggregationMethod aggregationMethod;
            private float opacity;
            public Heatmap(
                List<(Vector3 position, float intensity)> data,
                float cellSize = 3f,
                int kernelSize = 5,
                bool relativeFPS = true,
                float targetFPS = 60f,
                Color lowFPSColor = default,
                Color highFPSColor = default,
                float minFPS = 1f,
                float maxFPS = 150f,
                AggregationMethod aggregationMethod = AggregationMethod.Average,
                float opacity = 0.5f)
            {
                this.cellSize = cellSize;
                this.kernelSize = kernelSize;
                this.relativeFPS = relativeFPS;
                this.targetFPS = targetFPS;
                this.lowFPSColor = lowFPSColor == default ? Color.red : lowFPSColor;
                this.highFPSColor = highFPSColor == default ? Color.green : highFPSColor;
                this.minFPS = minFPS;
                this.maxFPS = maxFPS;
                this.aggregationMethod = aggregationMethod;
                this.opacity = opacity;
                InitializeGrid(data.ConvertAll(d => d.position), this.kernelSize);
                PopulateGrid(data);
                NormalizeGrid(this.relativeFPS, this.targetFPS);
            }


            public void UpdateSettings(
                float cellSize = 3f,
                int kernelSize = 5,
                bool relativeFPS = true,
                float targetFPS = 60f,
                Color lowFPSColor = default,
                Color highFPSColor = default,
                float minFPS = 1f,
                float maxFPS = 150f,
                AggregationMethod aggregationMethod = AggregationMethod.Average,
                float opacity = 0.5f)
            {
                this.cellSize = cellSize;
                this.kernelSize = kernelSize;
                this.relativeFPS = relativeFPS;
                this.targetFPS = targetFPS;
                this.lowFPSColor = lowFPSColor == default ? Color.red : lowFPSColor;
                this.highFPSColor = highFPSColor == default ? Color.green : highFPSColor;
                this.minFPS = minFPS;
                this.maxFPS = maxFPS;
                this.aggregationMethod = aggregationMethod;
                this.opacity = opacity;

            }

            public void InitializeGrid(List<Vector3> dataPoints, int blurKernelSize)
            {
                if (dataPoints == null || dataPoints.Count == 0)
                {
                    DevLog("InitializeGrid: No data points provided.");
                    return;
                }

                Vector3 minPoint = dataPoints[0];
                Vector3 maxPoint = dataPoints[0];

                foreach (var point in dataPoints)
                {
                    if (point.x < minPoint.x) minPoint.x = point.x;
                    if (point.z < minPoint.z) minPoint.z = point.z;
                    if (point.x > maxPoint.x) maxPoint.x = point.x;
                    if (point.z > maxPoint.z) maxPoint.z = point.z;
                }

                int padding = blurKernelSize / 2;
                gridOrigin = new Vector2(minPoint.x - padding * cellSize, minPoint.z - padding * cellSize);
                gridWidth = Mathf.CeilToInt((maxPoint.x - minPoint.x) / cellSize) + padding * 2;
                gridHeight = Mathf.CeilToInt((maxPoint.z - minPoint.z) / cellSize) + padding * 2;

                grid = new HeatmapCell[gridWidth, gridHeight];

                for (int y = 0; y < gridHeight; y++)
                {
                    for (int x = 0; x < gridWidth; x++)
                    {
                        grid[x, y] = new HeatmapCell(0, 0, 0);
                    }
                }
            }

            public void PopulateGrid(List<(Vector3 position, float intensity)> data)
            {
                if (data == null || data.Count == 0)
                {
                    DevLog("PopulateGrid: No data provided.");
                    return;
                }

                int aboveTarget = data.Where(d => d.intensity >= targetFPS).Count();
                DevLog($"Input data points: {data.Count}, Above target: {aboveTarget}, Target FPS: {targetFPS}");

                int falloffPadding = kernelSize;
                int expandedWidth = gridWidth + (falloffPadding * 2);
                int expandedHeight = gridHeight + (falloffPadding * 2);
                var expandedGrid = new HeatmapCell[expandedWidth, expandedHeight];

                for (int y = 0; y < expandedHeight; y++)
                {
                    for (int x = 0; x < expandedWidth; x++)
                    {
                        expandedGrid[x, y] = new HeatmapCell(0, 0, 0);
                    }
                }

                var cellValues = new Dictionary<(int x, int y), List<float>>();
                foreach (var (position, intensity) in data)
                {
                    if (float.IsInfinity(intensity) || float.IsNaN(intensity))
                    {
                        continue;
                    }

                    int xIndex = Mathf.FloorToInt((position.x - gridOrigin.x) / cellSize) + falloffPadding;
                    int zIndex = Mathf.FloorToInt((position.z - gridOrigin.y) / cellSize) + falloffPadding;

                    if (xIndex >= 0 && xIndex < expandedWidth && zIndex >= 0 && zIndex < expandedHeight)
                    {
                        float influenceRadius = cellSize * (kernelSize * 0.5f);

                        int radiusCells = Mathf.CeilToInt(influenceRadius / cellSize);
                        for (int dy = -radiusCells; dy <= radiusCells; dy++)
                        {
                            for (int dx = -radiusCells; dx <= radiusCells; dx++)
                            {
                                int nx = xIndex + dx;
                                int nz = zIndex + dy;

                                if (nx >= 0 && nx < expandedWidth && nz >= 0 && nz < expandedHeight)
                                {
                                    float dist = Vector2.Distance(
                                        new Vector2(xIndex, zIndex),
                                        new Vector2(nx, nz)
                                    ) * cellSize;

                                    if (dist <= influenceRadius)
                                    {
                                        var key = (nx, nz);
                                        if (!cellValues.ContainsKey(key))
                                        {
                                            cellValues[key] = new List<float>();
                                        }
                                        cellValues[key].Add(intensity);
                                    }
                                }
                            }
                        }
                    }
                }

                foreach (var kvp in cellValues)
                {
                    var (x, y) = kvp.Key;
                    var values = kvp.Value;
                    float aggregatedValue;

                    switch (aggregationMethod)
                    {
                        case AggregationMethod.Max:
                            aggregatedValue = values.Max();
                            break;
                        case AggregationMethod.Min:
                            aggregatedValue = values.Min();
                            break;
                        case AggregationMethod.Median:
                            var sortedValues = values.OrderBy(v => v).ToList();
                            int midIndex = sortedValues.Count / 2;
                            aggregatedValue = sortedValues.Count % 2 == 1
                                ? sortedValues[midIndex]
                                : (sortedValues[midIndex - 1] + sortedValues[midIndex]) * 0.5f;
                            break;
                        case AggregationMethod.Average:
                        default:
                            aggregatedValue = values.Average();
                            break;
                    }

                    float opacity = Mathf.Min(1.0f, values.Count / (float)(kernelSize * kernelSize));

                    float cellIntensity = aggregatedValue >= targetFPS ? 1.0f : 0.0f;

                    expandedGrid[x, y] = new HeatmapCell(cellIntensity, opacity, values.Count);
                }

                for (int y = 0; y < gridHeight; y++)
                {
                    for (int x = 0; x < gridWidth; x++)
                    {
                        var cell = expandedGrid[x + falloffPadding, y + falloffPadding];

                        float edgeDistX = Mathf.Min(x, gridWidth - 1 - x) / (float)falloffPadding;
                        float edgeDistY = Mathf.Min(y, gridHeight - 1 - y) / (float)falloffPadding;
                        float edgeFade = Mathf.Min(edgeDistX, edgeDistY);
                        edgeFade = Mathf.Clamp01(edgeFade);

                        cell.opacity *= edgeFade;

                        grid[x, y] = cell;
                    }
                }

                int finalAboveThreshold = 0;
                int totalCells = 0;
                for (int y = 0; y < gridHeight; y++)
                {
                    for (int x = 0; x < gridWidth; x++)
                    {
                        if (grid[x, y].opacity > 0.1f)
                        {
                            totalCells++;
                            if (grid[x, y].intensity > 0.5f)
                            {
                                finalAboveThreshold++;
                            }
                        }
                    }
                }
                DevLog($"Final grid using {aggregationMethod} aggregation: {totalCells} visible cells, {finalAboveThreshold} above threshold");
            }

            public void NormalizeGrid(bool usePredeterminedMax = false, float predeterminedMax = 60f)
            {
                return;
            }

            public void ApplyGaussianBlur(int kernelSize, float sigma)
            {
                return;
            }

            public HeatmapCell[,] GetGrid()
            {
                return grid;
            }

            public Vector2 GetGridOrigin()
            {
                return gridOrigin;
            }

            public float GetCellSize()
            {
                return cellSize;
            }

            public RenderTexture RenderHeatmap(int textureWidth, int textureHeight, Camera sceneCamera)
            {
                if (grid == null)
                {
                    DevLog("RenderHeatmap: Heatmap grid is not initialized.");
                    return null;
                }

                RenderTextureDescriptor rtDesc = new RenderTextureDescriptor(textureWidth, textureHeight, RenderTextureFormat.ARGB32, 0);
                rtDesc.enableRandomWrite = true;
                RenderTexture heatmapTexture = new RenderTexture(rtDesc);
                heatmapTexture.Create();

                float[] gridData = new float[gridWidth * gridHeight * 2];
                for (int y = 0; y < gridHeight; y++)
                {
                    for (int x = 0; x < gridWidth; x++)
                    {
                        int index = (y * gridWidth + x) * 2;
                        gridData[index] = grid[x, y].intensity;
                        gridData[index + 1] = grid[x, y].opacity;
                    }
                }

                if (gridData.Length == 0)
                {
                    return null;
                }

                ComputeShader computeShader = Resources.Load<ComputeShader>("HeatmapComputeShader");
                if (computeShader == null)
                {
                    return null;
                }

                ComputeBuffer gridBuffer = new ComputeBuffer(gridData.Length, sizeof(float));
                gridBuffer.SetData(gridData);

                Vector3 bottomLeftWorld = sceneCamera.ScreenToWorldPoint(new Vector3(0, 0, sceneCamera.nearClipPlane));
                Vector3 topRightWorld = sceneCamera.ScreenToWorldPoint(new Vector3(textureWidth, textureHeight, sceneCamera.nearClipPlane));

                float worldWidth = topRightWorld.x - bottomLeftWorld.x;
                float worldHeight = topRightWorld.z - bottomLeftWorld.z;

                Vector2 screenToWorldScale = new Vector2(worldWidth / textureWidth, worldHeight / textureHeight);
                Vector2 screenToWorldOffset = new Vector2(bottomLeftWorld.x, bottomLeftWorld.z);

                computeShader.SetBuffer(0, "GridData", gridBuffer);
                computeShader.SetTexture(0, "Result", heatmapTexture);
                computeShader.SetFloat("cellSize", cellSize);
                computeShader.SetInt("gridWidth", gridWidth);
                computeShader.SetInt("gridHeight", gridHeight);
                computeShader.SetVector("gridOrigin", new Vector4(gridOrigin.x, gridOrigin.y, 0, 0));
                computeShader.SetVector("screenToWorldScale", screenToWorldScale);
                computeShader.SetVector("screenToWorldOffset", screenToWorldOffset);
                computeShader.SetVector("lowFPSColor", new Vector3(lowFPSColor.r, lowFPSColor.g, lowFPSColor.b));
                computeShader.SetVector("highFPSColor", new Vector3(highFPSColor.r, highFPSColor.g, highFPSColor.b));
                computeShader.SetFloat("minFPS", minFPS);
                computeShader.SetFloat("maxFPS", maxFPS);
                computeShader.SetFloat("targetFPS", targetFPS);
                computeShader.SetFloat("globalOpacity", opacity);

                int threadGroupsX = Mathf.CeilToInt((float)textureWidth / 8.0f);
                int threadGroupsY = Mathf.CeilToInt((float)textureHeight / 8.0f);
                computeShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

                gridBuffer.Release();

                return heatmapTexture;
            }
        }
    }
}
#endif