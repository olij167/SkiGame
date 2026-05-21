using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif 

public class FibonacciBuilder : MonoBehaviour
{
    [Tooltip("Object or objects to be spawned in Fibonacci array.")]
    public GameObject[] spawners;
    public enum SpawnerIteration { Cycle, Random };
    public SpawnerIteration spawnerIteration = SpawnerIteration.Random;

    [Tooltip("Naming convension of spawned objects. Will be <Name>_1, <Name>_2, etc.")]
    public string baseName = "Object";

    public enum ColorChoice { Black, White, Blue, Cyan, Gray, Green, Red, Magenta, Yellow }

    [Tooltip("Color of preview points.")]
    public ColorChoice widgetColor = ColorChoice.Yellow;

    [Tooltip("Visual size of preview points.")]
    public float previewPointSize = 0.1f;

    [Tooltip("How many points in array.")]
    public int points = 144;
            
    public enum LatticeType { Disk, Sphere }
    [Tooltip("Type of arrangement (Disk or sphere).")]
    public LatticeType latticeType = LatticeType.Disk;

    private Vector3[] spawnPoints;
    private int currentSpawnObjectIndex = 0;   
    
    [System.Serializable]
    public class Magnitudes
    {     
        public AnimationCurve X = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1));
        public AnimationCurve Y = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1));
        public AnimationCurve Z = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1));
    }

    //for procedural scaling
    [System.Serializable]
    public class Scaling
    {
        public float baseScale = 1;
        public Magnitudes pointScaleMagnitudes;
    }
    [Tooltip("Scaling properties of created objects. See documentation.")]
    public Scaling scaling;


    //for procedural rotation
    [System.Serializable]
    public class Rotations
    {        
        public enum LookRotation { Z_Out, Z_In, Z_Up, Z_Down, Z_Left, Z_Right, Z_Face_Random }
        public LookRotation lookRotation = LookRotation.Z_Out;
        public Magnitudes pointRotationMagnitudes;       
    }
    [Tooltip("Rotation properties of created objects. See documentation.")]
    public Rotations rotations;

    //for surface projection
    [System.Serializable]
    public class Projection
    {
        public LayerMask sampleLayers;
        public bool project = false;
        public bool copySurfaceNormals = false;
        public float projectionOffset = 0;
        
    }
    [Tooltip("Projection of created objects against objects on chosen layers. (Only applies to disk array). See documentation.")]
    public Projection projection;
    
    /// <summary>
    /// Deletes the generated array
    /// </summary>
    public void DeleteObject() {
        while (transform.childCount != 0)
        {            
            DestroyImmediate(transform.GetChild(0).gameObject);
        }        
    }

    /// <summary>
    /// used in projection option, finding surface points of objects on a given layer, and mapping the supplied coordinate to the surface point.
    /// </summary>
    /// <param name="coord"></param>
    /// <returns></returns>
    public  Vector3 GetProjectedPoint(Vector3 coord) {
        RaycastHit hit;
        
        if (Physics.Raycast(coord, -transform.up, out hit, 1000, projection.sampleLayers))
        {            
            return hit.point;
        }
        else {
            return coord;
        }       
    }

    /// <summary>
    /// Gets the normal of a surface area for a given coordinate
    /// </summary>
    /// <param name="coord"></param>
    /// <returns></returns>
    public Vector3 GetProjectedPointNormal(Vector3 coord)
    {
        RaycastHit hit;
        
        if (Physics.Raycast(coord+transform.up, -transform.up, out hit, 1000, projection.sampleLayers))
        {
            return hit.normal;
        }
        else
        {
            return coord;
        }
    }

    /// <summary>
    /// Main function for generating a sphere lattice of points
    /// </summary>
    /// <param name="create"></param>
    public void GenerateSphereLattice(bool create = false) {
        spawnPoints = new Vector3[points];

        spawnPoints = new Vector3[points];
        float inc = Mathf.PI * (3 - Mathf.Sqrt(5));
        float off = 2.0f / spawnPoints.Length;
        float x = 0;
        float y = 0;
        float z = 0;
        float r = 0;
        float phi = 0;

        for (var k = 0; k < spawnPoints.Length; k++)
        {
            y = k * off - 1 + (off / 2);
            r = Mathf.Sqrt(1 - y * y);
            phi = k * inc;
            x = Mathf.Cos(phi) * r;
            z = Mathf.Sin(phi) * r;

            spawnPoints[k] = transform.TransformDirection(new Vector3(x, y, z)* scaling.baseScale / 2)+transform.position;
        }
    }

    /// <summary>
    /// Main function for generating a disk lattice of points
    /// </summary>
    /// <param name="create">Whether or not to create the object.</param>
    public void GenerateDiskLattice(bool create = false)
    {        
        spawnPoints = new Vector3[points];

        float theta = Mathf.PI * (3 - Mathf.Sqrt(5));

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            float r = (scaling.baseScale / 2) * Mathf.Sqrt(i) / Mathf.Sqrt(points);
            float a = theta * i;
            GetPointScale(i);
            Vector3 coords = transform.TransformDirection(new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)) + transform.position;

            if (projection.project)
            {
                coords = GetProjectedPoint(coords);
            }

            spawnPoints[i] = coords;
        }
    }

    /// <summary>
    /// Called to generate a lattice. Decides whether or not lattice will be a sphere or disk lattice. 
    /// </summary>
    /// <param name="create"></param>
    public void GenerateLattice(bool create = false) {

        if (!spawners[0])
        {
            Debug.Log("<color=green>Alert: Please set a gameobject to the spawner attribute. </color>");
            return;
        }


        if (previewPointSize < 0)
        {
            previewPointSize = 0;
        }

        if (points < 1)
        {
            points = 1;
        }

        if (scaling.baseScale <= 0)
        {
            scaling.baseScale = 0;
            return;
        }
        switch (latticeType)
        {
            case LatticeType.Disk:                
                GenerateDiskLattice(create: false);
                break;
            case LatticeType.Sphere:                
                GenerateSphereLattice(create: false);
                break;
        }

        if (create)
        {            
            Create();
        }
    }

    /// <summary>
    /// creates a default FibonacciArray gameobject to anchor array to.
    /// </summary>
    /// <returns></returns>
    public GameObject FibonacciArrayObject() {

        GameObject ar = new GameObject();
        ar.name = "FibonacciArray";
        ar.transform.parent = transform;
        ar.transform.position = transform.position;
        ar.transform.rotation = transform.rotation;
        FibonacciArray f = ar.AddComponent<FibonacciArray>();
        f.fibonacciArray = new GameObject[spawnPoints.Length];
        return ar;
    }

    /// <summary>
    /// Creates the array based on points supplied in spawnPoints.
    /// </summary>
    public void Create() {
        DeleteObject();
        GameObject ar = FibonacciArrayObject();
        FibonacciArray f = ar.GetComponent<FibonacciArray>();
        int i = 0;
        currentSpawnObjectIndex = 0;
        foreach (Vector3 sp in spawnPoints)
        {

            GameObject instance = Instantiate(spawners[currentSpawnObjectIndex], sp, transform.rotation);

            switch (spawnerIteration) {
                case SpawnerIteration.Random:
                    currentSpawnObjectIndex = Random.Range(0, spawners.Length);
                break;

                case SpawnerIteration.Cycle:
                    if (currentSpawnObjectIndex == (spawners.Length - 1))
                    {
                        currentSpawnObjectIndex = 0;
                    }
                    else
                    {
                        currentSpawnObjectIndex++;
                    }
                break;
            }
            
            instance.name = baseName + " (" + i.ToString() + ")";
            Vector3 scale = GetPointScale(i);
            instance.transform.localScale = new Vector3(
                scale.x * instance.transform.localScale.x,
                scale.y * instance.transform.localScale.y,
                scale.z * instance.transform.localScale.z
            );
            instance.transform.parent = ar.transform;
                        
            switch (latticeType) {

                case LatticeType.Sphere:                    
                    SetPointRotation(instance);
                    break;

                case LatticeType.Disk:
                    SetPointRotation(instance);
                    if (projection.project)
                    {
                        instance.transform.position += new Vector3(0, projection.projectionOffset, 0);
                    }

                    if (projection.project && projection.copySurfaceNormals) {
                        Vector3 n = GetProjectedPointNormal(instance.transform.position);
                        instance.transform.rotation = Quaternion.FromToRotation(instance.transform.up, n) * instance.transform.rotation;
                    }

                    break;
            }            
            instance.transform.Rotate(GetPointRotation(i)*360);            
            f.fibonacciArray[i] = instance;
            i++;
        }
    }

    /// <summary>
    /// "Places" the object by unparenting it.
    /// </summary>
    public void Place()
    {
        while (transform.childCount != 0)
        {
            transform.GetChild(0).parent = null;
        }
    }

    /// <summary>
    /// For each point, rotation is decided procedurally. This decides which direction the Z local axis will face. It is suggested that you decide your own rotations in your own script.
    /// </summary>
    /// <param name="instance"></param>
    public void SetPointRotation(GameObject instance) {
        Vector3 lookat = new Vector3(transform.position.x, instance.transform.position.y, transform.position.z);

        instance.transform.LookAt(lookat);

        switch (rotations.lookRotation) {
            case Rotations.LookRotation.Z_Out:
                instance.transform.Rotate(0, 180, 0);
                break;
            case Rotations.LookRotation.Z_In:
                //instance.transform.rotation = Quaternion.LookRotation(instance.transform.position - transform.position);
                break;
            case Rotations.LookRotation.Z_Up:
                instance.transform.Rotate(-90, 180, 0);
                break;
            case Rotations.LookRotation.Z_Down:
                instance.transform.Rotate(90, 180, 0);
                break;
            case Rotations.LookRotation.Z_Left:
                instance.transform.Rotate(0, -90, 0);
                break;
            case Rotations.LookRotation.Z_Right:
                instance.transform.Rotate(0, 90, 0);
                break;
            case Rotations.LookRotation.Z_Face_Random:
                instance.transform.Rotate(0, Random.Range(0,360), 0);
                break;
        }

        
    }

    /// <summary>
    /// Gets point scale based on animation curve. 
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public Vector3 GetPointScale(int point) {
        float evaluationPoint = (1.0f / points) * point;
        float x = scaling.pointScaleMagnitudes.X.Evaluate(evaluationPoint);
        float y = scaling.pointScaleMagnitudes.Y.Evaluate(evaluationPoint);
        float z = scaling.pointScaleMagnitudes.Z.Evaluate(evaluationPoint);
        return new Vector3(x, y, z);        
    }

    /// <summary>
    /// Gets rotation based on animation curve.
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public Vector3 GetPointRotation(int point)
    {
        float evaluationPoint = (1.0f / points) * point;
        float x = rotations.pointRotationMagnitudes.X.Evaluate(evaluationPoint);
        float y = rotations.pointRotationMagnitudes.Y.Evaluate(evaluationPoint);
        float z = rotations.pointRotationMagnitudes.Z.Evaluate(evaluationPoint);

        return new Vector3(x, y, z);
    }
    
    /// <summary>
    /// OnDrawGizmos is being used to create the point preview array.
    /// </summary>
    public void OnDrawGizmos()
    {

        #if UNITY_EDITOR

        if (!spawners[0])
        {            
            return;
        }

        GenerateLattice(create : false);
            
        //Black, White, Blue, Cyan, Gray, Green, Red, Magenta, Yellow 
        switch (widgetColor)
        {
            case ColorChoice.Black:
                Gizmos.color = Color.black;
                break;

            case ColorChoice.White:
                Gizmos.color = Color.white;
                break;

            case ColorChoice.Blue:
                Gizmos.color = Color.blue;
                break;

            case ColorChoice.Cyan:
                Gizmos.color = Color.cyan;
                break;

            case ColorChoice.Gray:
                Gizmos.color = Color.gray;
                break;

            case ColorChoice.Green:
                Gizmos.color = Color.green;
                break;

            case ColorChoice.Red:
                Gizmos.color = Color.red;
                break;

            case ColorChoice.Magenta:
                Gizmos.color = Color.magenta;
                break;

            case ColorChoice.Yellow:
                Gizmos.color = Color.yellow;
                break;

            default:
                Gizmos.color = Color.red;
                break;
        }
        
        foreach (Vector3 sp in spawnPoints) {
            Gizmos.DrawSphere(sp, previewPointSize);
            if (latticeType == LatticeType.Sphere) {

                var direction = (sp - transform.position).normalized;
                var end = transform.position + (direction * (scaling.baseScale / 2)*1.1f);

                Gizmos.DrawLine(sp, end);
            }
        }

        Gizmos.color = Color.white;                
        if (latticeType == LatticeType.Sphere)
        {
            Gizmos.DrawWireSphere(transform.position, scaling.baseScale / 2);
        }
        else {
            Handles.DrawWireDisc(transform.position, transform.up, scaling.baseScale / 2);
        }

        Gizmos.DrawLine(transform.position - transform.up * (scaling.baseScale * 2), transform.position + transform.up * (scaling.baseScale / 10));

        Gizmos.DrawLine(transform.position - transform.forward * (scaling.baseScale / 2), transform.position + transform.forward * (scaling.baseScale / 2));
        Gizmos.DrawLine(transform.position - transform.right * (scaling.baseScale / 2), transform.position + transform.right * (scaling.baseScale / 2));
        #endif   
    }

    /// <summary>
    /// Helper function: set amount of points for array. Will generate lattice accordingly.
    /// </summary>
    /// <param name="p"></param>
    public void SetPoints(int p) {
        points = p;
        GenerateLattice(create : false);
    }

    /// <summary>
    /// Helper function: get points only of array.
    /// </summary>
    /// <returns></returns>
    public Vector3[] GetPointPositions() {

        GenerateLattice(create : false);

          return spawnPoints;
    }

    /// <summary>
    /// Helper function: Get transforms of array.
    /// </summary>
    /// <returns></returns>
    public Transform[] GetPointTransforms()
    {

        Transform faObject = transform.Find("FibonacciArray");

        if (!faObject) {
            GenerateLattice(create: true);
            faObject = transform.Find("FibonacciArray");
        }

        GameObject[] fa = new GameObject[spawnPoints.Length];
        fa = faObject.GetComponent<FibonacciArray>().fibonacciArray;
        Transform[] tPoints = new Transform[fa.Length];
        int i = 0;
        foreach (GameObject p in fa) {                
            tPoints[i] = p.transform;
            
            i++;
        }
        return tPoints;        
    }

    /// <summary>
    /// Helper function: Get game objects of array.
    /// </summary>
    /// <returns></returns>
    public GameObject[] GetPointGameObjects()
    {
        Transform faObject = transform.Find("FibonacciArray");

        if (!faObject)
        {
            GenerateLattice(create: true);
            faObject = transform.Find("FibonacciArray");
        }
        GameObject[] fa = new GameObject[spawnPoints.Length];
        fa = faObject.GetComponent<FibonacciArray>().fibonacciArray;

        return fa;
    }

}