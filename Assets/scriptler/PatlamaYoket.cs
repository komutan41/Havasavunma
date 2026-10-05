using UnityEngine;

public class PatlamaYoket : MonoBehaviour
{
    public float omur = 6f;
    void Start() { Destroy(gameObject, omur); }
}