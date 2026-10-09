using UnityEngine;

// Taret objesine eklenir (Pantsir: taret_govdesi, Tor M1: "taret").
// Taret, kameranin baktigi yone kendini hizalar.
//
// IKI OLCME SEKLI VAR (otomatik secilir):
//  1) NAMLULU taret (Pantsir): tarete bagli bir makineli namlu noktasi bulunur, onun GERCEK dunya yonu olculur.
//  2) NAMLUSUZ taret (Tor M1): taretin kendi eksenlerine hic bakilmaz. Taret, ARACIN ON YONUNE gore
//     ne kadar dondugunu kendisi sayar. Model nasil aktarilmis olursa olsun calisir.
//     Baslangicta taretin onu aracin onune bakiyor olmalidir.
public class TaretKontrol : MonoBehaviour
{
    [Header("Baglantilar")]
    public KameraKontrol kamera;                  // BOS BIRAK: sahnedeki KameraKontrol otomatik bulunur
    public Transform rampa;                       // yukari-asagi donen parca (Tor'da BOS birak)
    public Transform yonReferansi;                // BOS BIRAK: Pantsir'de makinelinin ilk namlu noktasi otomatik bulunur
    public AracHareket arac;                      // BOS BIRAK: ustteki objelerde otomatik bulunur

    [Header("Donecek parca (TOR icin ONEMLI)")]
    public Transform donecekObje;                 // GORUNEN taret modeli. BOS birakirsan bu scriptin oldugu obje doner.
                                                  // Script bos bir yardimci objedeyse (ornek: Taret_Yon) buraya taret modelini surukle!
    public bool merkezOtomatik = true;            // ISARETLI: taret, GORUNEN modelinin (tum parcalarinin) ORTA NOKTASI etrafinda doner. Sadece 'Donecek Obje' doluyken calisir
    public Transform donmeNoktasi;                // (opsiyonel) elle merkez: tam taret ortasina koydugun bos obje. DOLUYSA otomatik merkez yerine bu kullanilir
                                                  // (bu bos objeyi taretin ALTINA degil, ARACIN ana objesinin altina koy)

    public bool girisAktif = true;                // AracYoneticisi arac degisince kapatir: taret kamerayi takip etmez

    [Header("Yon olcme")]
    public bool aracOnuneGore = false;            // ISARETLE: namlusu olmayan taret (Tor). Isaretlemesen de namlu bulunamazsa otomatik acilir

    [Header("Taret (yatay)")]
    public float taretHizi = 60f;                 // saniyede derece
    public float yatayOfset = 0f;                 // taret kameraya gore saga/sola kayiksa ince ayar (derece)

    [Header("Rampa (dikey)")]
    public float rampaHizi = 30f;
    public Vector3 rampaEkseni = new Vector3(1f, 0f, 0f);
    public bool rampaTersCevir = false;
    public float rampaMin = 0f;
    public float rampaMax = 75f;
    public float dikeyOfset = 0f;

    Transform dondur;         // gercekte donen parca
    Transform merkezBaz;      // otomatik merkezin bagli oldugu obje (arac govdesi)
    Vector3 merkezYerel;      // otomatik merkez: merkezBaz'a gore yerel konum
    bool merkezHazir;
    Quaternion rampaBaslangic;
    float rampaAci;
    Transform referans;
    bool aracModu;
    float goreliYaw;          // arac modu: taretin aracin onune gore kac derece dondugu

    void Start()
    {
        if (kamera == null) kamera = FindFirstObjectByType<KameraKontrol>();
        if (arac == null) arac = GetComponentInParent<AracHareket>();
        if (rampa != null) rampaBaslangic = rampa.localRotation;
        dondur = donecekObje != null ? donecekObje : transform;

        if (donecekObje != null && donmeNoktasi == null && merkezOtomatik) MerkezHesapla();

        // Uyari: script bos bir objede ve donecek parca atanmamis -> gorunen taret donmez
        if (donecekObje == null && GetComponent<Renderer>() == null && transform.childCount == 0 && transform.parent != null)
            Debug.LogWarning("TaretKontrol (" + name + "): bu obje bos gorunuyor, GORUNEN taret donmeyecek. 'Donecek Obje' alanina taret modelini surukle.", this);

        ReferansBul();
    }

    // Donen parcanin GORUNEN modelinin orta noktasini bulur (oyun basinda, taret henuz dönmemisken)
    void MerkezHesapla()
    {
        Renderer[] rs = dondur.GetComponentsInChildren<Renderer>(false);
        bool var1 = false;
        Bounds b = new Bounds();
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i] is ParticleSystemRenderer || !rs[i].enabled) continue;
            if (!var1) { b = rs[i].bounds; var1 = true; }
            else b.Encapsulate(rs[i].bounds);
        }
        if (!var1) return;

        merkezBaz = arac != null ? arac.transform : dondur.root;
        merkezYerel = merkezBaz.InverseTransformPoint(b.center);
        merkezHazir = true;
        Debug.Log("TaretKontrol (" + name + "): donme merkezi otomatik bulundu = " + b.center.ToString("F2") + "  (yanlissa 'Donme Noktasi' alanina bos obje ver)");
    }

    Vector3 DonmeMerkezi()
    {
        if (donmeNoktasi != null) return donmeNoktasi.position;
        return merkezBaz.TransformPoint(merkezYerel);
    }

    // Donusu tek yerden uygular (Pantsir/Tor farki: donen parca ayri olabilir)
    void Don(float aci)
    {
        if (Mathf.Abs(aci) < 0.0001f) return;
        if (donmeNoktasi != null || merkezHazir) dondur.RotateAround(DonmeMerkezi(), Vector3.up, aci);
        else dondur.Rotate(0f, aci, 0f, Space.World);
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !(donmeNoktasi != null || merkezHazir)) return;
        Gizmos.color = Color.cyan;
        Vector3 m = DonmeMerkezi();
        Gizmos.DrawWireSphere(m, 0.4f);
        Gizmos.DrawLine(m, m + Vector3.up * 3f);
    }

    float KokYaw()
    {
        Transform k = arac != null ? arac.transform : transform.root;
        Vector3 e = (arac != null && arac.ileriEksen.sqrMagnitude > 0.001f) ? arac.ileriEksen : Vector3.forward;
        Vector3 f = k.TransformDirection(e);
        return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }

    // Taretin GERCEK atis yonu: yatay = taretin baktigi yon, dikey = rampanin anlik acisi
    public Vector3 AtisYonu()
    {
        float yaw;
        if (aracModu)
        {
            yaw = KokYaw() + goreliYaw;
        }
        else
        {
            Vector3 f = referans != null ? referans.forward : transform.forward;
            yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }
        float pitch = rampa != null ? rampaAci : 0f;
        return Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
    }

    public Vector3 NamluPozisyonu()
    {
        return referans != null ? referans.position : transform.position;
    }

    void ReferansBul()
    {
        bool namluBulundu = false;
        referans = yonReferansi;
        if (referans != null) namluBulundu = true;

        if (referans == null)
        {
            // Sadece BU tarete bagli bir namlu noktasi sayilir (baska aracin namlusu karismasin)
            MakineliTufek mt = GetComponentInChildren<MakineliTufek>();
            if (mt == null) mt = FindFirstObjectByType<MakineliTufek>();
            if (mt != null && mt.namlular != null && mt.namlular.Length > 0
                && mt.namlular[0] != null && mt.namlular[0].IsChildOf(transform))
            {
                referans = mt.namlular[0];
                namluBulundu = true;
            }
        }

        if (referans != null && !referans.IsChildOf(transform))
        {
            Debug.LogWarning("TaretKontrol (" + name + "): Yon Referansi tarete bagli degil, yok sayildi.");
            referans = null;
            namluBulundu = false;
        }

        aracModu = aracOnuneGore || !namluBulundu;

        if (referans == null) referans = rampa != null ? rampa : transform;

        Debug.Log("TaretKontrol (" + name + "): " + (aracModu ? "ARAC ONUNE GORE donus modu (namlu yok)" : "NAMLU yonune gore donus modu"));
    }

    void Update()
    {
        if (!girisAktif || kamera == null) return;
        float dt = Time.deltaTime;

        if (aracModu)
        {
            // Taretin mevcut yonu = aracin yonu + kendi saydigi aci
            float mevcut = KokYaw() + goreliYaw;
            float fark = Mathf.DeltaAngle(mevcut, kamera.Yaw + yatayOfset);
            float adim = Mathf.Clamp(fark, -taretHizi * dt, taretHizi * dt);
            Don(adim);
            goreliYaw += adim;
        }
        else if (referans != null)
        {
            // Yatay: namlunun dunyadaki yonunu olc, kameranin yonune dogru don
            Vector3 f = referans.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.0001f)
            {
                float mevcut = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                float fark = Mathf.DeltaAngle(mevcut, kamera.Yaw + yatayOfset);
                float adim = taretHizi * dt;
                Don(Mathf.Clamp(fark, -adim, adim));
            }
        }

        // Dikey
        if (rampa != null)
        {
            float hedefR = Mathf.Clamp(kamera.Pitch + dikeyOfset, rampaMin, rampaMax);
            rampaAci = Mathf.MoveTowards(rampaAci, hedefR, rampaHizi * dt);
            float uygula = rampaTersCevir ? -rampaAci : rampaAci;
            rampa.localRotation = rampaBaslangic * Quaternion.AngleAxis(uygula, rampaEkseni);
        }
    }
}
