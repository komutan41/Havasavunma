using UnityEngine;

// KAMERA objesine eklenir (KameraKontrol'un oldugu obje).
// Iki arti cizer:
//   Acik gri  = taretin (mermi ve fuzenin gidecegi) yonu
//   Acik mavi = kameranin baktigi yon
// Ikisi ust uste gelince tek bir yesil arti olur.
// Fuze kumandasindayken (sinyal kaybi titremesi icin) tek bir kirmizi arti cizilir.
public class Nisangah : MonoBehaviour
{
    [Header("Baglantilar (bos birakirsan otomatik bulunur)")]
    public TaretKontrol taret;
    public FuzeKamera fuzeKamera;

    [Header("Gorunum")]
    public float mesafe = 1000f;                 // taret yonunun ekrana yansitildigi uzaklik (m)
    public float kolOrani = 0.012f;              // arti kolu uzunlugu = ekran yuksekligi x bu oran
    public float kalinlik = 2f;
    public float birlesmeToleransi = 10f;        // piksel: bu kadar yakinsa yesil olur
    public Color taretRengi = new Color(0.75f, 0.75f, 0.75f, 0.95f);
    public Color kameraRengi = new Color(0.55f, 0.8f, 1f, 0.95f);
    public Color birlesikRenk = new Color(0.3f, 1f, 0.3f, 1f);
    public Color fuzeRengi = new Color(1f, 0.2f, 0.2f, 1f);

    Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (taret == null) taret = FindFirstObjectByType<TaretKontrol>();
        if (fuzeKamera == null) fuzeKamera = FindFirstObjectByType<FuzeKamera>();
    }

    void Arti(Vector2 m, float kol, Color renk)
    {
        GUI.color = renk;
        GUI.DrawTexture(new Rect(m.x - kol, m.y - kalinlik * 0.5f, kol * 2f, kalinlik), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(m.x - kalinlik * 0.5f, m.y - kol, kalinlik, kol * 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (cam == null) return;

        float kol = Screen.height * kolOrani;
        Vector2 merkez = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // Fuze kumandasi: tek kirmizi arti, sinyal zayifladikca titrer
        if (fuzeKamera != null && fuzeKamera.FuzeAktifMi)
        {
            float r = fuzeKamera.GorselTitremeYaricapi;
            Vector2 t = fuzeKamera.ArtiTitreme;
            Arti(new Vector2(merkez.x + t.x * r, merkez.y - t.y * r), kol, fuzeRengi);
            return;
        }

        // Mavi: kameranin baktigi yon (ekran ortasi)
        Vector3 kamNokta = cam.WorldToScreenPoint(cam.transform.position + cam.transform.forward * mesafe);
        Vector2 mavi = new Vector2(kamNokta.x, Screen.height - kamNokta.y);

        if (taret == null)
        {
            Arti(mavi, kol, kameraRengi);
            return;
        }

        // Gri: taretin gercek atis yonu
        Vector3 tNokta = cam.WorldToScreenPoint(taret.NamluPozisyonu() + taret.AtisYonu() * mesafe);
        if (tNokta.z <= 0f)
        {
            Arti(mavi, kol, kameraRengi);
            return;
        }
        Vector2 gri = new Vector2(tNokta.x, Screen.height - tNokta.y);

        if (Vector2.Distance(gri, mavi) <= birlesmeToleransi)
        {
            Arti((gri + mavi) * 0.5f, kol, birlesikRenk);
        }
        else
        {
            Arti(gri, kol, taretRengi);
            Arti(mavi, kol, kameraRengi);
        }
    }
}
