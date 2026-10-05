using UnityEngine;
using UnityEngine.InputSystem;
 
public class FuzeAtesle : MonoBehaviour
{
    public GameObject fuzePrefab;
    public Transform[] cikisNoktalari;
    public Vector3 yonDuzeltme = Vector3.zero;
    public FuzeKamera fuzeKamera;
    public float atesAraligi = 0.8f;     // iki atis arasi en az sure (saniye)
 
    [Header("Yon")]
    public bool kameraYonuneAtesle = true;   // acik: fuze kameranin baktigi yone cikar (model yamuk olsa da duz gider)
    public float minYukselme = 0.15f;        // fuze en az bu kadar yukari bakarak cikar (yere gommesin)
 
    int sira;
    float sonAtes = -100f;
 
    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.spaceKey.wasPressedThisFrame) Ates();
    }
 
    public void Ates()
    {
        if (fuzePrefab == null || cikisNoktalari.Length == 0) return;
        if (Time.time - sonAtes < atesAraligi) return;
 
        Transform c = cikisNoktalari[sira];
        Quaternion rot = c.rotation * Quaternion.Euler(yonDuzeltme);
 
        if (kameraYonuneAtesle)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 f = cam.transform.forward;
                if (f.y < minYukselme) f.y = minYukselme;
                rot = Quaternion.LookRotation(f.normalized, Vector3.up);
            }
        }
 
        GameObject f2 = Instantiate(fuzePrefab, c.position, rot);
        sira = (sira + 1) % cikisNoktalari.Length;
        sonAtes = Time.time;
 
        if (fuzeKamera != null) fuzeKamera.FuzeyeGec(f2);
    }
}