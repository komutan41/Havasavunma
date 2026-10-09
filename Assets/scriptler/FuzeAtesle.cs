using UnityEngine;
using UnityEngine.InputSystem;

public class FuzeAtesle : MonoBehaviour
{
    public GameObject fuzePrefab;
    public Transform[] cikisNoktalari;
    public Vector3 yonDuzeltme = Vector3.zero;
    public FuzeKamera fuzeKamera;
    public TaretKontrol taret;           // TaretKontrol'u buraya surukle: fuze taretin baktigi yone gider
    public bool girisAktif = true;       // AracYoneticisi arac degisince kapatir
    public float atesAraligi = 0.8f;     // iki atis arasi en az sure (saniye)

    [Header("Cikis efekti (kovan arkasi)")]
    public bool cikisEfekti = true;
    public float kovanArkaOfset = 2.5f;      // cikis noktasindan geriye dogru kac metre (kovanin arka ucu)
    public float cikisEfektOlcegi = 1f;
    public bool sesEfekti = true;

    [Header("Yon")]
    public bool kameraYonuneAtesle = true;   // acik: fuze kameranin baktigi yone cikar (model yamuk olsa da duz gider)
    public float minYukselme = 0.15f;        // fuze en az bu kadar yukari bakarak cikar (yere gommesin)

    int sira;
    float sonAtes = -100f;

    void Start()
    {
        // Patlama efektini oyun basinda hazirla: ilk vurusta takilma olmasin
        if (fuzePrefab != null)
        {
            FuzeHareket fh = fuzePrefab.GetComponent<FuzeHareket>();
            if (fh != null && fh.patlamaPrefab != null)
                Efektler.Ornek.Isit(fh.patlamaPrefab, 3);
        }
    }

    void Update()
    {
        var k = Keyboard.current;
        if (girisAktif && k != null && k.spaceKey.wasPressedThisFrame) Ates();
    }

    public void Ates()
    {
        if (fuzePrefab == null || cikisNoktalari.Length == 0) return;
        if (Time.time - sonAtes < atesAraligi) return;

        Transform c = cikisNoktalari[sira];
        Quaternion rot = c.rotation * Quaternion.Euler(yonDuzeltme);

        if (taret != null)
        {
            Vector3 f = taret.AtisYonu();
            if (f.y < minYukselme) f.y = minYukselme;
            rot = Quaternion.LookRotation(f.normalized, Vector3.up);
        }
        else if (kameraYonuneAtesle)
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
        if (sesEfekti)
            SesYoneticisi.Ornek.Cal("fuze_cikis", c.position, 1f, Random.Range(0.97f, 1.03f), 2000f);
        if (cikisEfekti)
        {
            Vector3 ileri = rot * Vector3.forward;
            Efektler.Ornek.FuzeCikisi(c.position - ileri * kovanArkaOfset, -ileri, cikisEfektOlcegi);
        }

        sira = (sira + 1) % cikisNoktalari.Length;
        sonAtes = Time.time;

        if (fuzeKamera != null) fuzeKamera.FuzeyeGec(f2);
    }
}