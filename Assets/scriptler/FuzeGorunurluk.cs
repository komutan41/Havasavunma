using UnityEngine;

public class FuzeGorunurluk : MonoBehaviour
{
    [Header("Uzaklasinca buyume")]
    public float baslangicMesafe = 150f;   // bu mesafeye kadar normal boyut
    public float tamMesafe = 1500f;        // bu mesafede en buyuk olcek
    public float maksOlcek = 5f;

    [Header("Ekran isareti")]
    public bool isaretGoster = true;
    public float isaretMinMesafe = 200f;

    Camera kam;
    FuzeHareket hareket;
    ParticleSystem[] sistemler;
    float[] taban;
    Vector3 ilkOlcek;

    void Start()
    {
        kam = Camera.main;
        hareket = GetComponent<FuzeHareket>();
        ilkOlcek = transform.localScale;

        sistemler = GetComponentsInChildren<ParticleSystem>();
        taban = new float[sistemler.Length];
        for (int i = 0; i < sistemler.Length; i++)
            taban[i] = sistemler[i].main.startSizeMultiplier;
    }

    void LateUpdate()
    {
        if (kam == null) return;

        float d = Vector3.Distance(transform.position, kam.transform.position);
        float t = Mathf.InverseLerp(baslangicMesafe, tamMesafe, d);
        float s = Mathf.Lerp(1f, maksOlcek, t * t * (3f - 2f * t));   // yumusak gecis

        transform.localScale = ilkOlcek * s;
        for (int i = 0; i < sistemler.Length; i++)
        {
            var m = sistemler[i].main;
            m.startSizeMultiplier = taban[i] * s;
        }
    }

    void Elmas(Vector2 m, float yari, float kalinlik)
    {
        Matrix4x4 eski = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, m);
        Texture2D t = Texture2D.whiteTexture;
        GUI.DrawTexture(new Rect(m.x - yari, m.y - yari, yari * 2f, kalinlik), t);
        GUI.DrawTexture(new Rect(m.x - yari, m.y + yari - kalinlik, yari * 2f, kalinlik), t);
        GUI.DrawTexture(new Rect(m.x - yari, m.y - yari, kalinlik, yari * 2f), t);
        GUI.DrawTexture(new Rect(m.x + yari - kalinlik, m.y - yari, kalinlik, yari * 2f), t);
        GUI.matrix = eski;
    }

    void OnGUI()
    {
        if (!isaretGoster || kam == null) return;

        Vector3 v = kam.WorldToViewportPoint(transform.position);
        if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) return;

        float d = Vector3.Distance(transform.position, kam.transform.position);
        if (d < isaretMinMesafe) return;

        Vector2 m = new Vector2(v.x * Screen.width, (1f - v.y) * Screen.height);
        bool bagli = hareket == null || hareket.BaglantiVar;
        GUI.color = bagli ? new Color(1f, 0.9f, 0.2f, 0.9f) : new Color(1f, 0.25f, 0.2f, 0.9f);
        Elmas(m, 12f, 2f);
        GUI.color = Color.white;
    }
}