

namespace RaycastPro
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using RaySensors2D;
    using UnityEngine;
    using Object = UnityEngine.Object;
    using RaySensors;

#if UNITY_EDITOR
    using Editor;
#endif


    internal static class IListExtensions {
        public static void Swap<T>(
            this IList<T> list,
            int firstIndex,
            int secondIndex
        ) {
            if (firstIndex == secondIndex) return;
            if (firstIndex < 0 || secondIndex < 0) return;
            if (firstIndex > list.Count-1 || secondIndex > list.Count-1) return;
            
            (list[firstIndex], list[secondIndex]) = (list[secondIndex], list[firstIndex]);
        }
    }
  
    
    public static class RaycastProExtension
    {
        internal static void CloneDestroy(this RaySensor2D sensor)
        {
            if (!(sensor && sensor.gameObject)) return;

            if (sensor.cloneRaySensor) CloneDestroy(sensor.cloneRaySensor);
            
            Object.Destroy(sensor.gameObject);
        }

        internal static Quaternion ToRotation2D(this Vector2 direction)
        {
            var angle = Mathf.Atan2(direction.y,direction.x) * Mathf.Rad2Deg;
            
            return Quaternion.AngleAxis(angle, Vector3.forward);
        }
        internal static Quaternion ToRotation2D(this Vector3 direction)
        {
            var angle = Mathf.Atan2(direction.y,direction.x) * Mathf.Rad2Deg;
            
            return Quaternion.AngleAxis(angle, Vector3.forward);
        }
        internal static Quaternion ToRotation3D(this Vector3 direction, Vector3 up) => Quaternion.LookRotation(direction, up);
        internal static Vector2 To2D(this Vector3 vector3) => new Vector2(vector3.x, vector3.y);
        internal static Vector3 ToXFlip(this Vector3 vector3) => new Vector3(-vector3.x, vector3.y, vector3.z);
        internal static Vector3 ToYFlip(this Vector3 vector3) => new Vector3(vector3.x, -vector3.y, vector3.z);
        internal static Vector3 ToZFlip(this Vector3 vector3) => new Vector3(vector3.x, vector3.y, -vector3.z);
        internal static Vector3 ToDepth(this Vector3 vector3, float depth = 0) => new Vector3(vector3.x, vector3.y, depth);
        internal static Vector3 ToDepth(this Vector2 vector2, float depth = 0) => new Vector3(vector2.x, vector2.y, depth);
        internal static void SetSlicedPosition(this LineRenderer liner, IEnumerable<Vector3> path, Vector3 hitPoint, int detectIndex = -1)
        {
            var list = path.ToList();
            if (detectIndex > -1)
            {
                liner.positionCount = detectIndex+2;
                for (var i = 0; i <= detectIndex; i++)
                {
                    liner.SetPosition(i, list[i]);
                }
                liner.SetPosition(detectIndex+1, hitPoint);
                return;
            }
            liner.positionCount = list.Count;
            liner.SetPositions(list.ToArray());
        }
        internal static bool InColorTolerance(this Color color, Color targetColor, Color tolerance)
        {
            return Mathf.Abs(targetColor.r - color.r) <= tolerance.r
                   && Mathf.Abs(targetColor.b - color.b) <= tolerance.b
                   && Mathf.Abs(targetColor.g - color.g) <= tolerance.g
                   && Mathf.Abs(targetColor.a - color.a) <= tolerance.a;
        }
        internal static Vector3 GetDirection(this List<Vector3> points, int i) => points[i] - points[i - 1];
        internal static Vector2 GetDirection(this List<Vector2> points, int i) => points[i] - points[i - 1];
        internal static Vector3[] ToDepth(this Vector2[] points, float depth)
        {
            var newPoints = new Vector3[points.Length];
            
            for (var i = 0; i < points.Length; i++)  newPoints[i] = new Vector3(points[i].x, points[i].y, depth);
            
            return newPoints;
        }
        internal static Vector3[] ToDepth(this Vector3[] points, float depth = 0)
        {
            for (var i = 0; i < points.Length; i++)  points[i] = new Vector3(points[i].x, points[i].y, depth);
            
            return points;
        }
        internal static List<Vector3> ToDepth(this List<Vector3> points, float depth = 0)
        {
            for (var i = 0; i < points.Count; i++)  points[i] = new Vector3(points[i].x, points[i].y, depth);
            
            return points;
        }
        internal static List<Vector3> ToDepth(this List<Vector2> points, float depth = 0)
        {
            var newPoints = new List<Vector3>();
            
            for (var i = 0; i < points.Count; i++)  newPoints.Add(new Vector3(points[i].x, points[i].y, depth));
            
            return newPoints;
        }
        internal static Vector2[] To2D(this Vector3[] points)
        {
            var newPoints = new Vector2[points.Length];

            for (var i = 0; i < points.Length; i++)  newPoints[i] = new Vector2(points[i].x, points[i].y);
            
            return newPoints;
        }
        internal static Vector3 ToUp(this Vector3 points, float up) => new Vector3(points.x, points.y + up, points.z);
        internal static Vector3[] ToLocal(this Vector3[] points, Transform _t)
        {
            var newPoints = new Vector3[points.Length];
            for (var i = 0; i < points.Length; i++)  newPoints[i] = _t.TransformPoint(points[i]);
            return newPoints;
        }
        internal static Vector3 ToWorld(this Vector3 point, Transform _t) => _t.InverseTransformPoint(point);
        internal static Vector3 ToLocal(this Vector3 point, Transform _t) => _t.TransformPoint(point);
        internal static Vector2[] ToLocal(this Vector2[] points, Transform _t)
        {
            var newPoints = new Vector2[points.Length];
            
            for (var i = 0; i < points.Length; i++)  newPoints[i] = _t.TransformPoint(points[i]);
            
            return newPoints;
        }
        internal static Vector3[] ToRelative(this Vector3[] points)
        {
            var newPoints = new Vector3[points.Length];
            
            for (var i = 0; i < points.Length; i++)
            {
                var sum = Vector3.zero;

                for (var j = 0; j <= i; j++) sum += points[j];

                newPoints[i] = sum;
            }

            return newPoints;
        }
        internal static Vector2[] ToRelative(this Vector2[] points)
        {
            var newPoints = new Vector2[points.Length];
            
            for (var i = 0; i < points.Length; i++)
            {
                var sum = Vector2.zero;

                for (var j = 0; j <= i; j++) sum += points[j];

                newPoints[i] = sum;
            }

            return newPoints;
        }

        public static bool IsInTag(this RaycastHit hit, string _tag)
        {
            return hit.transform && hit.transform.CompareTag(_tag);
        }
        public static bool OutComponent<T>(this RaycastHit hit, out T component)
        {
            return hit.transform.TryGetComponent(out component);
        }
        public static bool IsInTag(this RaycastHit2D hit, string _tag)
        {
            return hit.transform && hit.transform.CompareTag(_tag);
        }

        #region RaycastHit

        public static int GetTerrainIndex(this RaycastHit hit)
        {
            if (!hit.transform) return -1;

            if (hit.transform.TryGetComponent(out Terrain terrain))
            {
                var terrainPos = hit.point - terrain.GetPosition();
                var splatPos = new Vector3(terrainPos.x / terrain.terrainData.size.x, 0,
                    terrainPos.z / terrain.terrainData.size.z);

                var x = Mathf.FloorToInt(splatPos.x * terrain.terrainData.alphamapWidth);
                var z = Mathf.FloorToInt(splatPos.z * terrain.terrainData.alphamapHeight);

                var alphaMaps = terrain.terrainData.GetAlphamaps(x, z, 1, 1);

                var _cIndex = 0;
                for (var i = 0; i < alphaMaps.Length; i++)
                {
                    if (alphaMaps[0,0,i] > alphaMaps[0, 0, _cIndex])
                    {
                        _cIndex = i;
                    }
                }

                return _cIndex;
            }
            return -1;
        }

        public static void GetTerrainAlpha(this RaycastHit hit, ref float[] alphasValues)
        {
            if (hit.transform.TryGetComponent(out Terrain terrain))
            {
                var terrainPos = hit.point - terrain.GetPosition();
                var splatPos = new Vector3(terrainPos.x / terrain.terrainData.size.x, 0,
                    terrainPos.z / terrain.terrainData.size.z);

                var x = Mathf.FloorToInt(splatPos.x * terrain.terrainData.alphamapWidth);
                var z = Mathf.FloorToInt(splatPos.z * terrain.terrainData.alphamapHeight);

                var alphaMaps = terrain.terrainData.GetAlphamaps(x, z, 1, 1);
                
                for (var i = 0; i < alphaMaps.Length; i++) alphasValues[i] = alphaMaps[0, 0, i];
            }
        }
        #endregion

        #region ColorCatching
        /// <summary>
        /// Returns the material of the exact sub-mesh hit by the ray.
        /// This method directly queries MeshFilter and MeshRenderer components
        /// and resolves the sub-mesh using the triangle index.
        /// </summary>
        /// <remarks>
        /// Simple and straightforward, but relatively expensive due to repeated
        /// GetComponent calls. Best suited for infrequent raycasts,
        /// editor tools, or debugging purposes.
        /// </remarks>
        /// <example>
        /// Used to determine whether a ray hit a metal or wood surface
        /// in a low-frequency interaction.
        /// </example>
        public static Material GetMaterial(this RaycastHit hit)
        {
            if (!hit.transform) return null;
            var materials = hit.transform.GetComponent<MeshRenderer>().materials;
            var index = hit.triangleIndex;
            var mesh = hit.transform.GetComponent<MeshFilter>().mesh;
            var subMeshIndex = RaySensor.GetSubMeshIndex(mesh, index);
            return materials[subMeshIndex];
        }
        
        /// <summary>
        /// Returns the material of the exact sub-mesh hit by the ray using cached mesh data.
        /// Functionally equivalent to GetMaterial, but optimized for performance.
        /// </summary>
        /// <remarks>
        /// Uses a mesh/material cache to avoid repeated component lookups,
        /// making it suitable for frequent raycasts in real-time systems
        /// such as weapon hits, surface interaction, or physics queries.
        /// </remarks>
        /// <example>
        /// Used in a shooting system where multiple raycasts are performed
        /// every frame and surface type must be resolved efficiently.
        /// </example>
        public static Material GetMaterialCached(this RaycastHit hit)
        {
            var col = hit.collider;
            if (!col)
                return null;

            if (!RaycastMeshCache.TryGet(col, out var entry))
                entry = RaycastMeshCache.Resolve(col);

            if (!entry.mesh || entry.materials == null)
                return null;

            int subMeshIndex = RaySensor.GetSubMeshIndex(entry.mesh, hit.triangleIndex);
            if ((uint)subMeshIndex >= entry.materials.Length)
                return null;

            return entry.materials[subMeshIndex];
        }
        
        /// <summary>
        /// Samples the exact pixel color from a specified material texture
        /// at the raycast hit point.
        /// </summary>
        /// <remarks>
        /// This method performs pixel-level sampling using GetPixel,
        /// taking texture scale and offset into account.
        /// It is relatively expensive and requires the texture to be readable.
        /// Use only when precise per-pixel information is required.
        /// </remarks>
        /// <example>
        /// Used for alpha-based hit detection on foliage,
        /// fences, or masked surfaces where transparency matters.
        /// </example>
        public static Color GetColor(this RaycastHit hit, string textureID)
        {
            var material = hit.GetMaterial();
            if (!material) return Color.black;
            var tex = material.GetTexture(textureID) as Texture2D;
            if (tex)
            {
                if (tex.isReadable)
                {
                    var scale = material.mainTextureScale;
                    var offset = material.mainTextureOffset;
                    var pixelUV = hit.textureCoord;
                    pixelUV.x *= tex.width*scale.x;
                    pixelUV.y *= tex.height*scale.y;
                    offset.x *= tex.width;
                    offset.y *= tex.height;
                        
                    return tex.GetPixel((int) (offset.x + pixelUV.x), (int) (offset.y + pixelUV.y)) * material.color;
                }
#if UNITY_EDITOR
                RCProEditor.Log($"{hit.transform.name} material texture isn't readable!");
#endif
                return material.color;
            }
            return material.color;
        }
        
        /// <summary>
        /// Returns the effective surface color at the raycast hit point.
        /// For opaque materials, this simply returns the material color.
        /// For transparent materials, it attempts to sample the texture pixel.
        /// </summary>
        /// <remarks>
        /// Provides a balanced approach between accuracy and performance.
        /// Suitable for gameplay logic where approximate surface color
        /// or alpha information is needed without full texture control.
        /// </remarks>
        /// <example>
        /// Used to decide whether a surface blocks vision or sound
        /// based on its transparency.
        /// </example>
        public static Color GetColor(this RaycastHit hit)
        {
            var material = hit.GetMaterial();
            if (!material)
                return Color.black;

            var baseColor = material.color;

            // اگر شیدر Opaque باشد، آلفا همیشه ۱ است
            if (material.renderQueue <= (int)UnityEngine.Rendering.RenderQueue.Geometry)
            {
                baseColor.a = 1f;
                return baseColor;
            }

            var tex = material.mainTexture as Texture2D;
            if (tex && tex.isReadable)
            {
                var scale = material.mainTextureScale;
                var offset = material.mainTextureOffset;
                var pixelUV = hit.textureCoord;

                pixelUV.x = pixelUV.x * tex.width * scale.x + offset.x * tex.width;
                pixelUV.y = pixelUV.y * tex.height * scale.y + offset.y * tex.height;

                if (pixelUV.x >= 0 && pixelUV.y >= 0 &&
                    pixelUV.x < tex.width && pixelUV.y < tex.height)
                {
                    Color pixel = tex.GetPixel((int)pixelUV.x, (int)pixelUV.y);
                    return new Color(
                        pixel.r * baseColor.r,
                        pixel.g * baseColor.g,
                        pixel.b * baseColor.b,
                        pixel.a * baseColor.a
                    );
                }
            }

#if UNITY_EDITOR
            if (tex && !tex.isReadable)
                RCProEditor.Log($"{hit.transform.name} material texture isn't readable!");
#endif

            // fallback: بدون تکسچر یا غیر readable → فقط color
            return baseColor;
        }
        
        /// <summary>
        /// Returns the effective surface color at the raycast hit point
        /// using cached material lookup.
        /// </summary>
        /// <remarks>
        /// Optimized version of GetColor, designed for performance-sensitive
        /// scenarios with frequent raycasts.
        /// Uses cached mesh/material data to reduce overhead.
        /// </remarks>
        /// <example>
        /// Used in continuous laser or scanning systems that must evaluate
        /// surface transparency every frame.
        /// </example>

        public static Color GetColorCached(this RaycastHit hit)
        {
            var material = hit.GetMaterialCached();
            if (!material)
                return Color.black;

            var baseColor = material.color;

            // Opaque → آلفا قطعاً ۱
            if (material.renderQueue <= (int)UnityEngine.Rendering.RenderQueue.Geometry)
            {
                baseColor.a = 1f;
                return baseColor;
            }

            var tex = material.mainTexture as Texture2D;
            if (tex && tex.isReadable)
            {
                var scale = material.mainTextureScale;
                var offset = material.mainTextureOffset;
                var pixelUV = hit.textureCoord;

                pixelUV.x = pixelUV.x * tex.width * scale.x + offset.x * tex.width;
                pixelUV.y = pixelUV.y * tex.height * scale.y + offset.y * tex.height;

                if (pixelUV.x >= 0 && pixelUV.y >= 0 &&
                    pixelUV.x < tex.width && pixelUV.y < tex.height)
                {
                    var pixel = tex.GetPixel((int)pixelUV.x, (int)pixelUV.y);
                    return new Color(
                        pixel.r * baseColor.r,
                        pixel.g * baseColor.g,
                        pixel.b * baseColor.b,
                        pixel.a * baseColor.a
                    );
                }
            }

            return baseColor;
        }
        /// <summary>
        /// Samples the pixel color of a Sprite at the raycast hit point
        /// using cached sprite and renderer data.
        /// </summary>
        /// <remarks>
        /// Optimized for frequent sprite-based raycasts.
        /// Avoids repeated GetComponent calls and is suitable for real-time
        /// 2D gameplay systems requiring pixel-accurate hit detection.
        /// </remarks>
        /// <example>
        /// Used in a 2D shooter where bullets should only collide
        /// with non-transparent parts of character sprites.
        /// </example>
        public static Color GetSpriteColorCached(this RaycastHit hit)
        {
            var col = hit.collider;
            if (!col)
                return Color.black;

            if (!RaycastSpriteCache.TryGet(col, out var entry))
                entry = RaycastSpriteCache.Resolve(col);

            var sr = entry.renderer;
            var sprite = entry.sprite;

            if (!sr || !sprite)
                return Color.black;

            var tex = sprite.texture;
            if (!tex || !tex.isReadable)
                return sr.color;

            // world → local
            Vector2 local = sr.transform.InverseTransformPoint(hit.point);

            // dispatch by draw mode
            switch (sr.drawMode)
            {
                case SpriteDrawMode.Simple:
                    return SampleSimple(sr, sprite, tex, local);

                case SpriteDrawMode.Sliced:
                    return SampleSliced(sr, sprite, tex, local);

                case SpriteDrawMode.Tiled:
                    return SampleTiled(sr, sprite, tex, local);

                default:
                    return Color.clear;
            }
        }
        /// <summary>
        /// Samples the pixel color of a Sprite at the raycast hit point.
        /// Accounts for sprite bounds, atlas rect, flipping, and renderer tint.
        /// </summary>
        /// <remarks>
        /// Directly accesses SpriteRenderer and texture data.
        /// Suitable for simple 2D interactions or low-frequency checks,
        /// but not recommended for heavy real-time usage.
        /// </remarks>
        /// <example>
        /// Used to detect whether a mouse click hit a visible (non-transparent)
        /// part of a 2D sprite.
        /// </example>
        public static Color GetSpriteColor(this RaycastHit hit)
        {
            var col = hit.collider;
            if (!col)
                return Color.black;

            var sr = col.GetComponent<SpriteRenderer>();
            if (!sr || !sr.sprite)
                return Color.black;

            var sprite = sr.sprite;
            var tex = sprite.texture;

            if (!tex || !tex.isReadable)
                return sr.color;

            // world → local
            Vector2 local = sr.transform.InverseTransformPoint(hit.point);

            switch (sr.drawMode)
            {
                case SpriteDrawMode.Simple:
                    return SampleSimple(sr, sprite, tex, local);

                case SpriteDrawMode.Sliced:
                    return SampleSliced(sr, sprite, tex, local);

                case SpriteDrawMode.Tiled:
                    return SampleTiled(sr, sprite, tex, local);

                default:
                    return Color.clear;
            }
        }
        static Color SampleSimple(
            SpriteRenderer sr,
            Sprite sprite,
            Texture2D tex,
            Vector2 local)
        {
            Rect rect = sprite.rect;
            Rect texRect = sprite.textureRect;

            Vector2 pivot = sprite.pivot / sprite.pixelsPerUnit;
            Vector2 size = rect.size / sprite.pixelsPerUnit;

            float u = (local.x + pivot.x) / size.x;
            float v = (local.y + pivot.y) / size.y;

            if (sr.flipX) u = 1f - u;
            if (sr.flipY) v = 1f - v;

            if (u < 0f || u > 1f || v < 0f || v > 1f)
                return Color.clear;

            int x = Mathf.FloorToInt(texRect.x + u * texRect.width);
            int y = Mathf.FloorToInt(texRect.y + v * texRect.height);

            if ((uint)x >= tex.width || (uint)y >= tex.height)
                return Color.clear;

            return tex.GetPixel(x, y) * sr.color;
        }
  
        static Color SampleSliced(
            SpriteRenderer sr,
            Sprite sprite,
            Texture2D tex,
            Vector2 local)
        {
            Rect rect = sprite.rect;
            Rect texRect = sprite.textureRect;

            Vector2 size = sr.size;
            Vector4 border = sprite.border / sprite.pixelsPerUnit;

            float x = local.x + size.x * 0.5f;
            float y = local.y + size.y * 0.5f;

            float u = ResolveSliceU(
                x, size.x,
                border.x, border.z,
                sprite.border.x, sprite.border.z,
                rect.width
            );

            float v = ResolveSliceU(
                y, size.y,
                border.y, border.w,
                sprite.border.y, sprite.border.w,
                rect.height
            );

            if (sr.flipX) u = 1f - u;
            if (sr.flipY) v = 1f - v;

            int px = Mathf.FloorToInt(texRect.x + u * texRect.width);
            int py = Mathf.FloorToInt(texRect.y + v * texRect.height);

            if ((uint)px >= tex.width || (uint)py >= tex.height)
                return Color.clear;

            return tex.GetPixel(px, py) * sr.color;
        }
        static float ResolveSliceU(
            float pos,
            float total,
            float borderMin,
            float borderMax,
            float pxBorderMin,
            float pxBorderMax,
            float pxTotal)
        {
            if (pos < borderMin)
                return (pos / borderMin) * (pxBorderMin / pxTotal);

            if (pos > total - borderMax)
                return 1f - ((total - pos) / borderMax) * (pxBorderMax / pxTotal);

            float centerSize = total - borderMin - borderMax;
            float centerPx = pxTotal - pxBorderMin - pxBorderMax;

            return (pxBorderMin / pxTotal) +
                   ((pos - borderMin) / centerSize) * (centerPx / pxTotal);
        }

        static Color SampleTiled(
            SpriteRenderer sr,
            Sprite sprite,
            Texture2D tex,
            Vector2 local)
        {
            Rect rect = sprite.rect;
            Rect texRect = sprite.textureRect;

            Vector2 tileSize = rect.size / sprite.pixelsPerUnit;

            float u = Mathf.Repeat(local.x / tileSize.x + 0.5f, 1f);
            float v = Mathf.Repeat(local.y / tileSize.y + 0.5f, 1f);

            if (sr.flipX) u = 1f - u;
            if (sr.flipY) v = 1f - v;

            int x = Mathf.FloorToInt(texRect.x + u * texRect.width);
            int y = Mathf.FloorToInt(texRect.y + v * texRect.height);

            if ((uint)x >= tex.width || (uint)y >= tex.height)
                return Color.clear;

            return tex.GetPixel(x, y) * sr.color;
        }
        #endregion
    
        public static bool CompareWith(this RaycastHit currenHit, RaycastHit hit) =>
            currenHit.transform == hit.transform;

        public static bool IsInLayerMask(this RaycastHit hit, LayerMask mask) {
            return mask == ( mask | ( 1 << hit.transform.gameObject.layer ) );
        }
        public static bool IsInLayerMask(this RaycastHit2D hit, LayerMask mask) {
            return mask == ( mask | ( 1 << hit.transform.gameObject.layer ) );
        }
        internal static Vector3 LastDirection(this List<Vector3> points, Vector3 defaultDir) => points.Count > 1 ?
            points[points.Count - 1] - points[points.Count - 2] : defaultDir;
        internal static Vector2 LastDirection(this List<Vector2> points, Vector2 defaultDir) => points.Count > 1 ?
            points[points.Count - 1] - points[points.Count - 2] : defaultDir; 
        internal static T LastOrBase<T>(this List<T> objects, T baseObject) => objects.Count > 0 ? objects[objects.Count - 1] : baseObject;
        
        internal static T LastOrBase<T>(this List<T> objects, T baseObject, int lastIndex = 0) => objects.Count > lastIndex ? objects[objects.Count - (1 + lastIndex)] : baseObject;
        
        internal static string ToRegex(this string text) => Regex.Replace(text, "(\\B[A-Z])", " $1").Replace("2 D", "2D");
        internal static void RemoveChildren(this Transform t) { foreach (Transform child in t) Object.Destroy(child.gameObject);}
        internal static bool InLayer(this LayerMask mask, GameObject obj) => mask == (mask | (1 << obj.layer));
        internal static Vector3 PortalPoint(this Transform t, Transform from, Vector3 point)
        {
            var p = from.InverseTransformPoint(point);

            return t.TransformPoint(p);
        }
        internal static float GetPathLength(this IEnumerable<Vector3> points)
        {
            var distance = 0f;
            var enumerable = points.ToList();
            for (var i = 0; i < enumerable.Count-1; i++)
            {
                distance += (enumerable[i+1] - enumerable[i]).magnitude;
            }
            return distance;
        }
        
        internal static float GetPathLength(this IEnumerable<Vector3> points, int index)
        {
            var enumerable = points.ToList();
            var distance = 0f;
    
            // اگر اندیس کمتر از 1 باشد، مسیر ندارد
            if (index < 1 || enumerable.Count < 2)
                return 0f;

            // از صفر تا اندیسِ مورد نظر یا آخر لیست، هر کدام که کمتر باشد
            var maxIndex = Math.Min(index, enumerable.Count - 1);

            for (var i = 0; i < maxIndex; i++)
            {
                distance += (enumerable[i + 1] - enumerable[i]).magnitude;
            }

            return distance;
        }

        internal static float GetPathLength(this IEnumerable<Vector2> points)
        {
            var distance = 0f;

            var enumerable = points.ToList();
            
            for (var i = 0; i < enumerable.Count-1; i++)
            {
                distance += (enumerable[i+1] - enumerable[i]).magnitude;
            }
            
            return distance;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="points"></param>
        /// <param name="index">Index 1 mean (point[1] - point[0]).magnitude</param>
        /// <returns></returns>
        internal static float GetEdgeLength(this IEnumerable<Vector3> points, int index)
        {
            var list = points.ToList();
            return (list[index] - list[index - 1]).magnitude;
        }
        internal static float GetEdgeLength(this IEnumerable<Vector2> points, int index)
        {
            var list = points.ToList();
            return (list[index] - list[index - 1]).magnitude;
        }
        internal static float GetPathLength(this IEnumerable<Vector3> points, int startIndex, int lastIndex)
        {
            var distance = 0f;
            var list = points.ToList();
            for (var i = startIndex; i < lastIndex; i++)
            {
                distance += (list[i+1] - list[i]).magnitude;
            }
            
            return distance;
        }
        internal static Vector3 GetPathInfo(this IEnumerable<Vector3> points, float pos)
        {
            var list = points.ToList();
            var posM = pos * list.GetPathLength();
            var p = Vector3.zero;
            for (var i = 1; i < list.Count; i++)
            {
                var lineDistance = list.GetEdgeLength(i);
                if (posM <= lineDistance) return Vector3.Lerp(list[i - 1], list[i], posM / lineDistance);
                posM -= lineDistance;
            }
            return p;
        }
        internal static (Vector3 point, int index) GetPathInfo(this List<Vector3> path, float value)
        {
            var posM = value * path.GetPathLength();

            for (var i = 1; i < path.Count; i++)
            {
                var lineDistance = path.GetEdgeLength(i);
                
                if (posM <= lineDistance) return (Vector3.Lerp(path[i - 1], path[i], posM / lineDistance), i);

                posM -= lineDistance;
            }
            return (path.Last(), path.Count-1);
        }

        internal static Vector3 GetPositionOnPath(this List<Vector3> path, float pos)
        {
            pos = Mathf.Clamp01(pos);
            var posM = pos * path.GetPathLength();

            var p = Vector3.zero;
            for (var i = 1; i < path.Count; i++)
            {
                var lineDistance = path.GetEdgeLength(i);
                if (posM <= lineDistance)
                {
                    p = Vector3.Lerp(path[i - 1], path[i], posM / lineDistance);
                    break;
                }
                posM -= lineDistance;
            }
            return p;
        }
        internal static (Vector2 point, int index) GetPathInfo(this List<Vector2> path, float value)
        {
            var posM = value * path.GetPathLength();

            for (var i = 1; i < path.Count; i++)
            {
                var lineDistance = path.GetEdgeLength(i);
                if (posM <= lineDistance) return (Vector3.Lerp(path[i - 1], path[i], posM / lineDistance), i);

                posM -= lineDistance;
            }
            return (path.Last(), path.Count-1);
        }

        internal static Color Alpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
        internal static void ToAlpha(ref this Color color, float alpha)
        {
            color.a = alpha;
        }
        internal static GUIContent ToContent(this string label, string tooltip) => new GUIContent(label, tooltip);
        internal static GUIContent ToContent(this string label) => new GUIContent(label, label);
        internal static GUIContent[] ToContents(this string[] label, string[] tooltip)
        {
            var contents = new GUIContent[label.Length];

            for (var i = 0; i < contents.Length; i++)
            {
                contents[i] = label[i].ToContent(tooltip[i]);
            }

            return contents;
        }
        
        internal static GUIContent[] ToContent(this string[] label, string[] tooltip)
        {
            var contents = new GUIContent[label.Length];
            for (var i = 0; i < label.Length; i++)
            {
                contents[i].text = label[i];
                contents[i].tooltip = tooltip[i];
            }
            return contents;
        }
        internal static IEnumerable<Type> GetInheritedTypes(this Type BaseClass)
        {
            var subclassTypes = Assembly
                .GetAssembly(BaseClass)
                .GetTypes()
                .Where(type => type.IsSubclassOf(BaseClass));
            return subclassTypes;
        }
    }
}