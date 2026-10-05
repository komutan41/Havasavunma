using UnityEngine;

public class RadarDondur : MonoBehaviour
{
    public float hiz = 100f;                                  // saniyede derece
    public Vector3 eksen = new Vector3(0f, 1f, 0f);          // hangi eksende donecek

    void Update()
    {
        transform.Rotate(eksen * hiz * Time.deltaTime, Space.Self);
    }
}