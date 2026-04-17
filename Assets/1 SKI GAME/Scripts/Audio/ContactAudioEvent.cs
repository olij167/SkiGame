using UnityEngine;

[System.Serializable]
public struct ContactAudioEvent
{
    public ContactAudioEventType type;
    public AudioSurfaceMaterialSO materialA;
    public AudioSurfaceMaterialSO materialB;
    public Vector3 point;
    public Vector3 normal;
    public float normalSpeed;
    public float tangentialSpeed;
    public float intensity01;
    public float duration;
    public GameObject sourceObject;
    public Collider otherCollider;
    public bool isSustained;
    public int contactId;
}
