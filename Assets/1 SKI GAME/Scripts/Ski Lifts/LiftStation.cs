using UnityEngine;

public enum LiftStationType { Bottom, Top, Mid }

public class LiftStation : MonoBehaviour
{
    public LiftStationType stationType;
    public LiftLine line;          // Which lift line this belongs to
    public float stationLength = 10f; // How “long” the slow zone is along the path

    // Optional boarding / dismount triggers can be separate child colliders
    public Collider boardingZone;
    public Collider dismountZone;

    private void OnDrawGizmos()
    {
        Gizmos.color = stationType == LiftStationType.Bottom ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(4f, 2f, stationLength));
        Gizmos.DrawRay(transform.position, transform.forward * stationLength);
    }
}
