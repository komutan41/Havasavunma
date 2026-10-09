using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RadarEkrani : MonoBehaviour
{
    [Header("Baglantilar")]
    public KameraKontrol kamera;
    public Transform arac;               // taret_govdesi
    public FuzeKamera fuzeKamera;

    [Header("Radar")]
    public float radarMenzili = 2500f;
    public float boyut = 0.30f;          // ekran yuksekliginin orani (cap)
    public float kenarBosluk = 20f;
    public float taramaHizi = 90f;

    [Header("Hedef isareti")]
    public float hizCizgiCarpani = 0.7f; // piksel / (m/s)
    public float hizCizgiMin = 20f;
    public float hizCizgiMax = 90f;

    public HedefUcus Secili { get; private set; }

    static readonly Color radarYesil = new Color(0.4f, 1f, 0.4f, 1f);

    Camera cam;
    Texture2D zemin;
    Texture2D halka;
    Texture2D kare;
    Texture2D dolguDaire;
    Texture2D okUcu;
    FuzeHareket[] fuzeler = new FuzeHareket[0];

    void Awake()
    {
        cam = GetComponent<Camera>();
        zemin = DaireOlustur(256, new Color(0f, 0.15f, 0f, 0.55f), new Color(0.3f, 1f, 0.4f, 0.9f), 3);
        halka = DaireOlustur(256, Color.clear, Color.white, 3);
        dolguDaire = DaireOlustur(64, Color.white, Color.white, 64);
        kare = KareOlustur(64, 3);
        okUcu = UcgenOlustur(32);
    }

    Texture2D DaireOlustur(int n, Color dolgu, Color kenar, int kalinlik)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        float r = n * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                Color c = Color.clear;
                if (d < r) c = dolgu;
                if (d < r && d > r - kalinlik) c = kenar;
                t.SetPixel(x, y, c);
            }
        }
        t.Apply();
        return t;
    }

    Texture2D KareOlustur(int n, int kalinlik)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                bool k = x < kalinlik || y < kalinlik || x >= n - kalinlik || y >= n - kalinlik;
                t.SetPixel(x, y, k ? Color.white : Color.clear);
            }
        }
        t.Apply();
        return t;
    }

    Texture2D UcgenOlustur(int n)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
        {
            float w = (1f - y / (float)(n - 1)) * n * 0.5f;
            for (int x = 0; x < n; x++)
            {
                bool ic = Mathf.Abs(x + 0.5f - n * 0.5f) <= w;
                t.SetPixel(x, y, ic ? Color.white : Color.clear);
            }
        }
        t.Apply();
        return t;
    }

    void OnEnable()
    {
        HedefUcus.ChaffKilidiAldi += ChaffKilidi;
    }

    void OnDisable()
    {
        HedefUcus.ChaffKilidiAldi -= ChaffKilidi;
    }

    void ChaffKilidi(HedefUcus ucak, HedefUcus chaff)
    {
        if (Secili == ucak) Sec(chaff);
    }

    // Chaff'e kilitliyken kare yanip soner
    bool Gorunur
    {
        get
        {
            bool chaffte = Secili != null && Secili.sahte;
            return !chaffte || (Time.time * 4f) % 1f < 0.5f;
        }
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.tKey.wasPressedThisFrame) SonrakiHedef();
        if (k != null && k.xKey.wasPressedThisFrame) Sec(null);
        if (k != null && k.rKey.wasPressedThisFrame) KilidiYenile();

        if (Secili != null && Secili.olu) Sec(null);

        fuzeler = FindObjectsByType<FuzeHareket>(FindObjectsSortMode.None);

        var p = Pointer.current;
        if (p != null && p.press.wasPressedThisFrame) TiklaSec(p.position.ReadValue());
    }

    void Sec(HedefUcus h)
    {
        HedefUcus eskiSecili = Secili;
        Secili = h;
        SesCal(eskiSecili, h);
        if (kamera != null) kamera.KilitAyarla(h != null ? h.transform : null);
        if (h != null && fuzeKamera != null) fuzeKamera.hedef = h.transform;
    }

    // Kilit sesleri: yeni hedefe kilit / kilit birakma / chaff'e kayma
    void SesCal(HedefUcus eski, HedefUcus yeni)
    {
        if (eski == yeni) return;
        if (yeni == null) SesYoneticisi.Ornek.Cal2D("radar_birak", 0.4f);
        else if (yeni.sahte) SesYoneticisi.Ornek.Cal2D("radar_chaff", 0.45f);
        else SesYoneticisi.Ornek.Cal2D("radar_kilit", 0.4f);
    }

    void SonrakiHedef()
    {
        List<HedefUcus> canli = new List<HedefUcus>();
        foreach (var h in HedefUcus.tumu)
        {
            if (h != null && !h.olu && !h.sahte) canli.Add(h);
        }
        if (canli.Count == 0) return;

        int i = canli.IndexOf(Secili);
        Sec(canli[(i + 1) % canli.Count]);
    }

    // Kilidi birakip, baktigin yone en yakin GERCEK ucaga yeniden kilitler
    void KilidiYenile()
    {
        if (kamera == null || arac == null) return;
        Vector3 bakis = Quaternion.Euler(-kamera.Pitch, kamera.Yaw, 0f) * Vector3.forward;

        HedefUcus en = null;
        float enAci = 30f;
        foreach (var h in HedefUcus.tumu)
        {
            if (h == null || h.olu || h.sahte) continue;
            float a = Vector3.Angle(bakis, h.transform.position - arac.position);
            if (a < enAci) { enAci = a; en = h; }
        }
        Sec(en);
    }

    // Dunya farkini radar pikseline cevirir (x sag, y asagi)
    Vector2 RadarOfset(Vector3 dunyaFark, float R)
    {
        Vector3 l = Quaternion.Euler(0f, -kamera.Yaw, 0f) * dunyaFark;
        return new Vector2(l.x, -l.z) / radarMenzili * R;
    }

    // Radarda yukari = 0 derece, saat yonu pozitif
    float RadarAcisi(Vector3 dunyaYonu)
    {
        Vector3 l = Quaternion.Euler(0f, -kamera.Yaw, 0f) * dunyaYonu;
        return Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg;
    }

    void TiklaSec(Vector2 ekranNokta)
    {
        if (kamera == null || arac == null) return;
        float R = Screen.height * boyut * 0.5f;
        Vector2 merkez = new Vector2(kenarBosluk + R, kenarBosluk + R);   // ekran koordinati (alt-sol baslangic)
        if (Vector2.Distance(ekranNokta, merkez) > R) return;

        HedefUcus enYakin = null;
        float enKucuk = Mathf.Max(R * 0.15f, 25f);
        foreach (var h in HedefUcus.tumu)
        {
            if (h == null || h.olu) continue;
            Vector2 o = RadarOfset(h.transform.position - arac.position, R);
            if (o.magnitude > R) continue;
            Vector2 nokta = merkez + new Vector2(o.x, -o.y);
            float d = Vector2.Distance(ekranNokta, nokta);
            if (d < enKucuk) { enKucuk = d; enYakin = h; }
        }
        if (enYakin != null) Sec(enYakin);
    }

    // a noktasindan, yukariya gore aciDerece yonunde (saat yonu) cizgi
    void Cizgi(Vector2 a, float aciDerece, float uzunluk, float kalinlik)
    {
        Matrix4x4 eski = GUI.matrix;
        GUIUtility.RotateAroundPivot(aciDerece, a);
        GUI.DrawTexture(new Rect(a.x - kalinlik * 0.5f, a.y - uzunluk, kalinlik, uzunluk), Texture2D.whiteTexture);
        GUI.matrix = eski;
    }

    Vector2 AciVektoru(float aciDerece)
    {
        float r = aciDerece * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(r), -Mathf.Cos(r));
    }

    void OnGUI()
    {
        if (kamera == null || arac == null) return;

        float cap = Screen.height * boyut;
        float R = cap * 0.5f;
        Vector2 merkez = new Vector2(kenarBosluk + R, Screen.height - kenarBosluk - R);   // GUI koordinati (ust-sol baslangic)

        HedefIsareti();

        // radar zemini
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(merkez.x - R, merkez.y - R, cap, cap), zemin);
        GUI.color = new Color(0.3f, 1f, 0.4f, 0.35f);
        GUI.DrawTexture(new Rect(merkez.x - R * 0.5f, merkez.y - R * 0.5f, R, R), halka);
        GUI.DrawTexture(new Rect(merkez.x - R, merkez.y - 0.5f, cap, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(merkez.x - 0.5f, merkez.y - R, 1f, cap), Texture2D.whiteTexture);

        // tarama cizgisi
        GUI.color = new Color(0.3f, 1f, 0.4f, 0.6f);
        Cizgi(merkez, Time.time * taramaHizi, R, 2f);

        // kendi araciniz
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(merkez.x - 3f, merkez.y - 3f, 6f, 6f), Texture2D.whiteTexture);

        // hedefler
        foreach (var h in HedefUcus.tumu)
        {
            if (h == null || h.olu) continue;
            Vector2 o = RadarOfset(h.transform.position - arac.position, R);
            if (o.magnitude > R) continue;

            Vector2 nokta = merkez + o;
            bool sec = h == Secili;
            if (sec && !Gorunur) continue;

            GUI.color = sec ? radarYesil : new Color(0.4f, 1f, 0.4f, 0.6f);

            float b = sec ? 8f : 6f;
            GUI.DrawTexture(new Rect(nokta.x - b * 0.5f, nokta.y - b * 0.5f, b, b), Texture2D.whiteTexture);

            float aci = RadarAcisi(h.yon.normalized);
            float uz = Mathf.Clamp(h.hiz * 0.35f, 6f, R * 0.3f);
            Cizgi(nokta, aci, uz, sec ? 2f : 1f);

            if (sec)
            {
                GUI.DrawTexture(new Rect(nokta.x - 10f, nokta.y - 10f, 20f, 20f), kare);
                Vector2 uc = nokta + AciVektoru(aci) * uz;
                GUI.DrawTexture(new Rect(uc.x - 2.5f, uc.y - 2.5f, 5f, 5f), dolguDaire);
            }
        }

        // fuzeler (ok)
        foreach (var m in fuzeler)
        {
            if (m == null) continue;
            Vector2 o = RadarOfset(m.transform.position - arac.position, R);
            if (o.magnitude > R * 0.97f) o = o.normalized * R * 0.97f;
            Vector2 nokta = merkez + o;

            GUI.color = m.BaglantiVar ? new Color(1f, 0.9f, 0.2f, 1f) : new Color(1f, 0.25f, 0.2f, 1f);
            Matrix4x4 eski = GUI.matrix;
            GUIUtility.RotateAroundPivot(RadarAcisi(m.transform.forward), nokta);
            GUI.DrawTexture(new Rect(nokta.x - 5f, nokta.y - 7f, 10f, 14f), okUcu);
            GUI.matrix = eski;
        }
        GUI.color = Color.white;

        // bilgi yazisi
        GUIStyle s = new GUIStyle(GUI.skin.label);
        s.fontSize = Mathf.RoundToInt(Screen.height * 0.025f);
        s.normal.textColor = Color.white;

        string bilgi = "Hedef secilmedi  (T veya radara dokun)";
        if (Secili != null)
        {
            Vector3 d = Secili.transform.position - arac.position;
            bilgi = "[KILITLI] " + Secili.isim + "   Mesafe: " + Mathf.RoundToInt(d.magnitude) +
                    " m   Irtifa: " + Mathf.RoundToInt(Secili.transform.position.y) +
                    " m   Hiz: " + Mathf.RoundToInt(Secili.hiz) + " m/s";
        }
        GUI.Label(new Rect(kenarBosluk, Screen.height - kenarBosluk - cap - Screen.height * 0.045f, 900f, 40f), bilgi, s);

        // vurulan sayaci
        GUIStyle sayac = new GUIStyle(GUI.skin.label);
        sayac.alignment = TextAnchor.UpperRight;
        sayac.fontSize = Mathf.RoundToInt(Screen.height * 0.035f);
        sayac.normal.textColor = Color.yellow;
        GUI.Label(new Rect(Screen.width - 420f, 20f, 400f, 60f), "Vurulan hedef: " + HedefUcus.vurus, sayac);
    }

    // Secili hedefi ekranda kare + hiz cizgisiyle, ekran disindaysa ok ile gosterir
    void HedefIsareti()
    {
        if (Secili == null || cam == null) return;
        if (!Gorunur) return;

        Vector3 pos = Secili.transform.position;
        Vector3 v = cam.WorldToViewportPoint(pos);
        bool arkada = v.z < 0f;
        if (arkada) { v.x = 1f - v.x; v.y = 1f - v.y; }

        bool ekranda = !arkada && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;

        if (ekranda)
        {
            Vector2 mrk = new Vector2(v.x * Screen.width, (1f - v.y) * Screen.height);
            float mesafe = Vector3.Distance(pos, cam.transform.position);
            float kenar = Mathf.Clamp(40000f / Mathf.Max(mesafe, 1f), 32f, 80f);

            GUI.color = radarYesil;
            GUI.DrawTexture(new Rect(mrk.x - kenar * 0.5f, mrk.y - kenar * 0.5f, kenar, kenar), kare);

            // hiz cizgisi: hedefin 3 saniye sonraki konumuna dogru
            Vector3 hizVek = Secili.yon.normalized * Secili.hiz;
            Vector3 v2 = cam.WorldToViewportPoint(pos + hizVek * 3f);
            if (v2.z > 0f)
            {
                Vector2 p2 = new Vector2(v2.x * Screen.width, (1f - v2.y) * Screen.height);
                Vector2 fark = p2 - mrk;
                if (fark.magnitude > 1f)
                {
                    float aci = Mathf.Atan2(fark.x, -fark.y) * Mathf.Rad2Deg;
                    float uz = Mathf.Clamp(Secili.hiz * hizCizgiCarpani, hizCizgiMin, hizCizgiMax);
                    Vector2 bas = mrk + fark.normalized * (kenar * 0.5f);
                    Cizgi(bas, aci, uz, 2f);
                    Vector2 uc = bas + fark.normalized * uz;
                    GUI.DrawTexture(new Rect(uc.x - 4f, uc.y - 4f, 8f, 8f), dolguDaire);
                }
            }
            GUI.color = Color.white;
        }
        else
        {
            float cx = Mathf.Clamp(v.x, 0.06f, 0.94f);
            float cy = Mathf.Clamp(v.y, 0.06f, 0.94f);
            float dx = v.x - 0.5f;
            float dy = v.y - 0.5f;
            string ok;
            if (Mathf.Abs(dx) > Mathf.Abs(dy)) ok = dx > 0f ? ">>" : "<<";
            else ok = dy > 0f ? "^^" : "vv";

            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.alignment = TextAnchor.MiddleCenter;
            s.fontSize = Mathf.RoundToInt(Screen.height * 0.06f);
            s.normal.textColor = radarYesil;
            GUI.Label(new Rect(cx * Screen.width - 60f, (1f - cy) * Screen.height - 40f, 120f, 80f), ok, s);
        }
    }
}