using UnityEngine;
using UnityEngine.InputSystem;

// Tor M1 kok objesine eklenir (AracHareket ile ayni obje).
//  - Oyun basinda fuze kapaklari yavasca acilir.
//  - SPACE: siradaki fuzeyi (1'den 8'e) atar. Radarda KILITLI ucak sart (T tusu ile sec).
//  - Atilan fuzenin hucredeki modeli (fuze_1 ...) gizlenir, yerine ucan fuze prefab'i cikar.
//  - Fuze dumduz yukari cikar, tepede itici ateslemesiyle burnunu hedefe dogru alcaltir, hemen sonra booster yanar (FuzeHareket).
public class TorAtesle : MonoBehaviour
{
    [System.Serializable]
    public class Kapak
    {
        public string isim = "Kapak";
        public Transform kapak;                                   // kapak objesi (pivotu mentese kenarinda olmali)
        public Vector3 eksen = new Vector3(1f, 0f, 0f);           // kapagin KENDI yerel ekseni: mentese hangi eksen etrafinda donsun
        public float acilmaAcisi = 95f;                           // derece
        public bool tersCevir = false;                            // kapak ice dogru aciliyorsa isaretle
        [HideInInspector] public Quaternion baz = Quaternion.identity;
        [HideInInspector] public float ac;                        // 0 = kapali, 1 = acik
    }

    [System.Serializable]
    public class Hucre
    {
        public string isim = "Fuze";
        public GameObject fuzeModeli;      // hucrenin icindeki gorunen fuze (fuze_1 ... fuze_8)
        public Transform cikis;            // (opsiyonel) fuze_cikis_1 ... : bos birakirsan fuze modelinin yeri kullanilir
        public int kapakNo = 0;            // bu hucrenin kapagi: Kapaklar listesindeki sira numarasi (0'dan baslar)
        [HideInInspector] public bool atildi;
    }

    [Header("Kontrol")]
    public bool girisAktif = true;         // AracYoneticisi arac degisince kapatir

    [Header("Fuze")]
    public GameObject fuzePrefab;          // Tor fuze prefab'i ("Kendi Gudumlu" isaretli)
    public RadarEkrani radar;              // bos birakirsan otomatik bulunur
    public float atesAraligi = 1.2f;       // iki atis arasi en az sure (sn)

    [Header("Ucan fuzenin gorunumu / kalkis")]
    public bool hucreModeliniKullan = false;   // ISARETLI: ucan fuzenin gorunumu hucredeki fuze modelinden kopyalanir (yon, olcek ve doku birebir dogru olur)
    public float tepeYuksekligi = 4.5f;       // BOOSTER bu yukseklikte (METRE, hucredeki fuzenin yerinden olculur) yanar. Taretin ~2-3 m ustu icin 4-5 dene; daha yuksek istersen arttir

    [Header("Yeniden dolum")]
    public Key dolumTusu = Key.E;
    public float dolumSuresi = 12f;           // atilan tum fuzelerin dolum suresi (sn). Dolum sirasinda arac durmali
    public bool otomatikDolum = false;        // ISARETLI: fuze bitince 3 sn sonra dolum kendiliginden baslar
    public Transform ikmalNoktasi;            // (opsiyonel) bos objeyi buraya surukle: dolum ancak bu noktanin yakininda yapilir
    public float ikmalYaricapi = 40f;

    [Header("Hucreler (1'den 8'e SIRAYLA)")]
    public Hucre[] hucreler;

    [Header("Kapaklar")]
    public Kapak[] kapaklar;
    public bool baslangictaAc = true;      // oyun baslayinca kapaklar yavasca acilir
    public float acilmaGecikmesi = 1f;     // oyun basladiktan kac sn sonra acilmaya baslasin
    public float acilmaSuresi = 3f;        // acilma suresi (yavas = buyuk)

    [Header("Cikis")]
    public Vector3 baslangicOfseti = Vector3.zero;   // ucan fuze hucredeki modelle tam oturmazsa ince ayar (metre, aracin kendi yonlerine gore)
    public bool merkezeHizala = true;                // acik: ucan fuze, hucredeki fuzenin tam yerine otomatik oturur
    public bool cikisEfekti = true;
    public float cikisEfektOlcegi = 0.6f;
    public bool sesEfekti = true;

    float sonAtes = -100f;
    AracHareket arac;
    bool dolumda;
    bool dolumDur;
    float dolumGecen;
    int dolumSayisi;
    int dolumDoldurulan;
    float acilmaZamani;
    string mesaj;
    float mesajBitis;

    void Start()
    {
        if (radar == null) radar = FindFirstObjectByType<RadarEkrani>();
        arac = GetComponent<AracHareket>();
        acilmaZamani = Time.time + acilmaGecikmesi;

        if (kapaklar != null)
            for (int i = 0; i < kapaklar.Length; i++)
                if (kapaklar[i] != null && kapaklar[i].kapak != null)
                    kapaklar[i].baz = kapaklar[i].kapak.localRotation;
    }

    void Update()
    {
        KapaklariGuncelle(Time.deltaTime);

        var k = Keyboard.current;
        if (girisAktif && k != null && k.spaceKey.wasPressedThisFrame) Ates();
        if (girisAktif && k != null && k[dolumTusu].wasPressedThisFrame) DolumBaslat();

        if (otomatikDolum && !dolumda && KalanFuze() == 0 && Time.time - sonAtes > 3f) DolumBaslat();
        DolumGuncelle(Time.deltaTime);
    }

    void KapaklariGuncelle(float dt)
    {
        if (kapaklar == null) return;
        bool acik = baslangictaAc && Time.time >= acilmaZamani;
        float adim = dt / Mathf.Max(acilmaSuresi, 0.05f);

        for (int i = 0; i < kapaklar.Length; i++)
        {
            Kapak kp = kapaklar[i];
            if (kp == null || kp.kapak == null) continue;

            kp.ac = Mathf.MoveTowards(kp.ac, acik ? 1f : 0f, adim);
            float yumusak = Mathf.SmoothStep(0f, 1f, kp.ac);
            float aci = kp.acilmaAcisi * yumusak * (kp.tersCevir ? -1f : 1f);
            Vector3 e = kp.eksen.sqrMagnitude > 0.001f ? kp.eksen.normalized : Vector3.right;
            kp.kapak.localRotation = kp.baz * Quaternion.AngleAxis(aci, e);
        }
    }

    int SonrakiHucre()
    {
        if (hucreler == null) return -1;
        for (int i = 0; i < hucreler.Length; i++)
            if (hucreler[i] != null && !hucreler[i].atildi) return i;
        return -1;
    }

    int KalanFuze()
    {
        int n = 0;
        if (hucreler == null) return 0;
        for (int i = 0; i < hucreler.Length; i++)
            if (hucreler[i] != null && !hucreler[i].atildi) n++;
        return n;
    }

    void Mesaj(string m)
    {
        mesaj = m;
        mesajBitis = Time.time + 1.5f;
    }

    // Bir objenin MeshRenderer'larinin dunyadaki merkez noktasi
    static bool MerkezBul(GameObject g, out Vector3 merkez)
    {
        merkez = Vector3.zero;
        if (g == null) return false;
        MeshRenderer[] rs = g.GetComponentsInChildren<MeshRenderer>(false);
        bool bulundu = false;
        Bounds b = new Bounds();
        for (int i = 0; i < rs.Length; i++)
        {
            if (!rs[i].enabled) continue;                       // gizlenen eski gorunum sayilmaz
            if (!bulundu) { b = rs[i].bounds; bulundu = true; }
            else b.Encapsulate(rs[i].bounds);
        }
        if (!bulundu) return false;
        merkez = b.center;
        return true;
    }

    // Ucan fuzenin eski mesh gorunumunu kapatir, yerine hucredeki fuze modelinin kopyasini koyar.
    // Kopya, hucredeki fuzenin TAM konum/yonunde olusur: burun yukari bakar, olcek ve doku ayni kalir.
    void GorunumuKopyala(GameObject f, GameObject kaynak)
    {
        MeshRenderer[] eski = f.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < eski.Length; i++) eski[i].enabled = false;   // parcacik efektleri aynen kalir

        GameObject kopya = Instantiate(kaynak, kaynak.transform.position, kaynak.transform.rotation, f.transform);
        kopya.name = "FuzeGorunum";
        kopya.SetActive(true);

        // Dunyadaki olcegi korumak icin yerel olcegi ayarla
        Vector3 ks = kaynak.transform.lossyScale;
        Vector3 ps = f.transform.lossyScale;
        kopya.transform.localScale = new Vector3(
            ks.x / Mathf.Max(Mathf.Abs(ps.x), 0.0001f),
            ks.y / Mathf.Max(Mathf.Abs(ps.y), 0.0001f),
            ks.z / Mathf.Max(Mathf.Abs(ps.z), 0.0001f));
    }

    // ---------------- Yeniden dolum ----------------
    void DolumBaslat()
    {
        if (dolumda) return;
        if (hucreler == null) return;

        int bos = 0;
        for (int i = 0; i < hucreler.Length; i++)
            if (hucreler[i] != null && hucreler[i].atildi) bos++;
        if (bos == 0) { Mesaj("FUZELER DOLU"); return; }

        if (ikmalNoktasi != null && Vector3.Distance(transform.position, ikmalNoktasi.position) > ikmalYaricapi)
        {
            Mesaj("IKMAL NOKTASINA GIT");
            return;
        }

        dolumda = true;
        dolumDur = false;
        dolumGecen = 0f;
        dolumSayisi = bos;
        dolumDoldurulan = 0;
        Mesaj("YENIDEN DOLUM BASLADI  (araci durdur)");
    }

    void DolumGuncelle(float dt)
    {
        if (!dolumda) return;

        // Arac hareket ediyorsa dolum bekler
        if (arac != null && Mathf.Abs(arac.Hiz) > 0.5f) { dolumDur = true; return; }
        dolumDur = false;

        dolumGecen += dt;
        float adimSuresi = Mathf.Max(dolumSuresi, 0.5f) / Mathf.Max(dolumSayisi, 1);

        while (dolumDoldurulan < dolumSayisi && dolumGecen >= adimSuresi * (dolumDoldurulan + 1))
        {
            BirFuzeDoldur();
            dolumDoldurulan++;
        }

        if (dolumDoldurulan >= dolumSayisi)
        {
            dolumda = false;
            Mesaj("DOLUM TAMAM");
        }
    }

    void BirFuzeDoldur()
    {
        for (int i = 0; i < hucreler.Length; i++)
        {
            Hucre h = hucreler[i];
            if (h == null || !h.atildi) continue;
            h.atildi = false;
            if (h.fuzeModeli != null) h.fuzeModeli.SetActive(true);
            return;
        }
    }

    string DolumYazisi()
    {
        if (!dolumda) return "";
        if (dolumDur) return "   DOLUM BEKLEMEDE (araci durdur)";
        int yuzde = Mathf.RoundToInt(Mathf.Clamp01(dolumGecen / Mathf.Max(dolumSuresi, 0.5f)) * 100f);
        return "   DOLUM %" + yuzde;
    }

    public void Ates()
    {
        if (fuzePrefab == null) return;
        if (Time.time - sonAtes < atesAraligi) return;
        if (dolumda) { Mesaj("DOLUM SURUYOR"); return; }

        int i = SonrakiHucre();
        if (i < 0) { Mesaj("FUZE KALMADI   [" + dolumTusu + "] YENIDEN DOLUM"); return; }

        HedefUcus hedef = radar != null ? radar.Secili : null;
        if (hedef == null || hedef.olu) { Mesaj("KILIT YOK  (T: hedef sec)"); return; }

        Hucre h = hucreler[i];

        Kapak kp = (kapaklar != null && h.kapakNo >= 0 && h.kapakNo < kapaklar.Length) ? kapaklar[h.kapakNo] : null;
        if (kp != null && kp.kapak != null && kp.ac < 0.95f) { Mesaj("KAPAK ACILIYOR"); return; }

        // Baslangic konumu: hucredeki fuzenin oldugu yer
        Vector3 hucreMerkezi;
        bool merkezVar = MerkezBul(h.fuzeModeli, out hucreMerkezi);
        Vector3 poz = h.cikis != null ? h.cikis.position
                    : (h.fuzeModeli != null ? h.fuzeModeli.transform.position : transform.position);
        if (merkezeHizala && merkezVar) poz = hucreMerkezi;

        Vector3 yukari = transform.up;                                  // dumduz yukari
        Quaternion rot = Quaternion.LookRotation(yukari, transform.forward);

        GameObject f = Instantiate(fuzePrefab, poz, rot);

        if (merkezeHizala && merkezVar)
        {
            Vector3 fm;
            if (MerkezBul(f, out fm)) f.transform.position += hucreMerkezi - fm;   // modeller birebir ust uste otursun
        }
        f.transform.position += transform.TransformVector(baslangicOfseti);

        FuzeHareket fh = f.GetComponent<FuzeHareket>();
        if (fh == null) { Debug.LogWarning("TorAtesle: fuze prefab'inda FuzeHareket yok."); Destroy(f); return; }
        if (!fh.kendiGudumlu) Debug.LogWarning("TorAtesle: fuze prefab'inda 'Kendi Gudumlu' kutusu isaretli degil!");
        fh.kalkisYuksekligi = tepeYuksekligi;        // dik yukari firlama yuksekligi (TorAtesle ayari prefab ayarini ezer)
        fh.HedefAta(hedef);

        if (h.fuzeModeli != null) h.fuzeModeli.SetActive(false);        // fuze gercekten firlatilmis gibi hucreden kaybolur
        h.atildi = true;
        sonAtes = Time.time;

        if (sesEfekti)
            SesYoneticisi.Ornek.Cal("fuze_cikis", poz, 1f, Random.Range(0.97f, 1.03f), 2000f);
        if (cikisEfekti)
            Efektler.Ornek.FuzeCikisi(poz, yukari, cikisEfektOlcegi);
    }

    void OnGUI()
    {
        if (!girisAktif) return;

        GUIStyle s = new GUIStyle(GUI.skin.label);
        s.alignment = TextAnchor.UpperCenter;
        s.fontSize = Mathf.RoundToInt(Screen.height * 0.028f);
        s.normal.textColor = Color.white;
        GUI.Label(new Rect(0f, 15f, Screen.width, 50f), "TOR M1   Fuze: " + KalanFuze() + "/" + (hucreler != null ? hucreler.Length : 0) + DolumYazisi(), s);

        if (Time.time < mesajBitis)
        {
            GUIStyle u = new GUIStyle(GUI.skin.label);
            u.alignment = TextAnchor.UpperCenter;
            u.fontSize = Mathf.RoundToInt(Screen.height * 0.04f);
            u.normal.textColor = new Color(1f, 0.35f, 0.3f);
            GUI.Label(new Rect(0f, Screen.height * 0.12f, Screen.width, 70f), mesaj, u);
        }
    }
}
