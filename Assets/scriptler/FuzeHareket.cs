using System.Collections.Generic;
using UnityEngine;

// Iki calisma sekli vardir:
//  1) LAZER GUDUMLU (Pantsir): fuze, kameranin gosterdigi lazer huzmesini izler.
//  2) KENDI GUDUMLU (Tor M1):  "Kendi Gudumlu" kutusu isaretli prefab'larda. IKI FAZ:
//     1) KALKIS : fuze SABIT hizla dumduz yukari cikarken (yavaslamaz, durmaz), govdedeki 6 iticiden hedefin
//                 TERS tarafindaki ates eder ve fuze YUKSELIRKEN burnunu hedefe dogru egilmeAcisi (70) derece alcaltir.
//                 Tepe yuksekligine (kalkisYuksekligi) varinca...
//     2) BOOSTER: ...ARA VERMEDEN booster yanar, beyaz duman izi baslar. Hiz baslangicHizi'ndan hiz'a boosterSuresi
//                 sn icinde kademeli artar. Fuze kilitli ucagin onune (kestirim) gider. Chaff'e aldanabilir.
public class FuzeHareket : MonoBehaviour
{
    public static List<FuzeHareket> tumu = new List<FuzeHareket>();

    [Header("Ucus")]
    public float hiz = 250f;                 // EN YUKSEK hiz (m/sn). km/h icin: m/sn x 3.6  (2000 km/h = 555 m/sn)
    public float omur = 20f;
    public float donusHizi = 45f;
    public float ileriBakis = 150f;

    [Header("Lazer huzmesi (sadece lazer gudumlu)")]
    public float minGenislik = 60f;
    public float lazerKoniAcisi = 10f;

    [Header("KENDI GUDUMLU MOD (Tor M1)")]
    public bool kendiGudumlu = false;        // Tor fuze prefab'inda ISARETLE
    public bool bilgiYaz = true;             // Console'a her atista tepe yuksekligini yazar (ayar bitince kapat)
    [Header("1) Kalkis + burun alcaltma (yukselirken doner)")]
    public float kalkisYuksekligi = 4.5f;    // m: BOOSTER'in yandigi yukseklik (hucredeki yerinden olculur). TorAtesle.tepeYuksekligi bunu ezer
    public float kalkisHizi = 12f;           // m/sn: SABIT yukselme hizi (yavaslamaz). Dusuk = daha uzun sure doner
    public float egilmeAcisi = 70f;          // derece: tepeye varana kadar burun dikeyden hedefe dogru bu kadar yatar
    public bool egilmeAnlikKonuma = true;    // ISARETLI: burun, ucagin SU ANKI konumuna dogru yatar (ucak sagdaysa fuze saga yatar). Kapali: kestirim (onune) noktasina
    public bool iticiAlevi = true;           // govdedeki 6 iticiden ilgili olan kalkis boyunca ates puskurtur
    public bool iticiTersCevir = false;      // itici yonu hala ters gorunuyorsa BUNU isaretle (normalde gerekmez)
    public float iticiKonumu = 0.30f;        // burundan geriye dogru, fuze boyunun orani (0.3 = harp basligindan biraz geride)
    public float iticiHalkaOrani = 0.045f;   // itici halkasinin yaricapi / fuze boyu (govde kalinligina gore ayarla)
    public float iticiOlcegi = 1f;           // alev boyutu carpani
    public bool iticiNoktalariniGoster = false;   // Scene gorunumunde itici noktalarini cizer (ayar icin ac)

    [Header("2) Booster ve gudum")]
    public float baslangicHizi = 20f;        // m/sn: booster yanarken ilk hiz (20 = 72 km/h, 28 = 100 km/h)
    public float boosterSuresi = 5f;         // sn: baslangicHizi'ndan en yuksek hiz'a (yukaridaki 'Hiz') cikma suresi
    public float gudumDonusHizi = 70f;       // derece/sn: ucus sirasinda donebilme siniri
    public float hizYonuUyumu = 8f;          // booster'da fuzenin hareket yonu burnuna ne kadar hizli uyar (buyuk = keskin donus)
    [Range(0f, 1f)] public float aldanmaOlasiligi = 0.35f;   // ucak chaff atinca fuzenin chaff'e kayma sansi
    public float boosterAlevOlcegi = 0.7f;   // booster atesleme parlamasi/dumani boyutu

    [Header("Baslangic duman izi (beyaz)")]
    public bool beyazDuman = true;
    public float beyazDumanSuresi = 1.5f;   // SADECE lazer gudumlu: bu kadar sn beyaz duman. Kendi gudumluda tum ucus boyunca
    public float beyazDumanBoyutu = 1.5f;   // kalin/ince icin bunu degistir

    [Header("Ses")]
    public bool sesEfekti = true;
    public float motorSesi = 0.7f;
    public float motorMenzili = 1200f;
    public float patlamaSesi = 1f;

    [Header("Carpma")]
    public GameObject patlamaPrefab;
    public float vurmaMesafesi = 10f;
    public float chaffKilitAcisi = 4f;   // lazer chaff'e bu kadar derece yakinsa fuze ona carpar

    [HideInInspector] public bool kumandaVar;
    [HideInInspector] public Vector3 losKaynak;
    [HideInInspector] public Vector3 losYon;

    public bool BaglantiVar { get; private set; }

    float dogum;
    bool bitti;
    ParticleSystem beyazIz;

    // kendi gudumlu
    enum Faz { Kalkis, Booster }
    Faz faz = Faz.Kalkis;
    float fazBas;
    HedefUcus gudumHedef;
    float hizAnlik;
    Vector3 kalkisBas;
    Vector3 kalkisYonu;
    Vector3 hizYonu;
    float kalkisSure;
    float boosterIvme;
    Quaternion egilmeBasRot;
    Vector3 sonYatay;
    ParticleSystem[] motorSistemleri;
    ParticleSystem[] iticiler;
    Vector3[] iticiYerelYon;

    void OnEnable()
    {
        tumu.Add(this);
        if (kendiGudumlu) HedefUcus.ChaffKilidiAldi += ChaffAldat;
    }

    void OnDisable()
    {
        tumu.Remove(this);
        HedefUcus.ChaffKilidiAldi -= ChaffAldat;
    }

    void Awake()
    {
        if (kendiGudumlu)
        {
            // Prefab icindeki duman/ates efektleri booster yanana kadar kapali kalir
            motorSistemleri = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < motorSistemleri.Length; i++)
                motorSistemleri[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        if (beyazDuman) beyazIz = Efektler.Ornek.FuzeIziEkle(transform, beyazDumanBoyutu);
        if (sesEfekti) SesYoneticisi.Ornek.DonguEkle(gameObject, "fuze_motor", motorSesi, motorMenzili);
    }

    void Start()
    {
        dogum = Time.time;
        kalkisBas = transform.position;
        kalkisYonu = transform.forward;

        if (kendiGudumlu)
        {
            kalkisSure = Mathf.Max(kalkisYuksekligi, 0.5f) / Mathf.Max(kalkisHizi, 1f);   // sabit hizla bu yukseklige varma suresi
            fazBas = Time.time;
            egilmeBasRot = transform.rotation;
            if (iticiAlevi)
            {
                IticiKur();
                Vector3 yatay; float aci;
                EgilmeHesapla(out yatay, out aci);
                IticiAtesle(yatay);
            }
        }
    }

    // Hedefe dogru egilme yonu (yatay bilesen) ve egilme acisi
    void EgilmeHesapla(out Vector3 yatay, out float aci)
    {
        bool hedefVar = gudumHedef != null && !gudumHedef.olu;
        Vector3 pos = transform.position;
        Vector3 hedefNokta = hedefVar ? (egilmeAnlikKonuma ? gudumHedef.transform.position : Kestirim(pos)) : pos + kalkisYonu;
        Vector3 d = hedefNokta - pos;

        yatay = Vector3.ProjectOnPlane(d, kalkisYonu);
        if (yatay.sqrMagnitude < 0.0001f) yatay = Vector3.ProjectOnPlane(Vector3.forward, kalkisYonu);
        if (yatay.sqrMagnitude < 0.0001f) yatay = Vector3.ProjectOnPlane(Vector3.right, kalkisYonu);
        yatay.Normalize();

        // Hedef dikeye cok yakinsa 70 dereceye kadar yatirmak yanlis olur: hedefin acisini gecme
        float hedefAci = hedefVar ? Vector3.Angle(kalkisYonu, d) : egilmeAcisi;
        aci = Mathf.Min(egilmeAcisi, Mathf.Max(hedefAci, 10f));
        sonYatay = yatay;
    }

    // ---------------- Yon iticileri (6 adet, govdede halka) ----------------
    // Fuzenin GORUNEN govdesinin olcusunden hesaplanir: burundan 'iticiKonumu' kadar geride, 60 derece arayla 6 nokta.
    void IticiKur()
    {
        MeshRenderer[] rs = GetComponentsInChildren<MeshRenderer>(false);
        bool bulundu = false;
        Bounds b = new Bounds();
        for (int i = 0; i < rs.Length; i++)
        {
            if (!rs[i].enabled) continue;
            if (!bulundu) { b = rs[i].bounds; bulundu = true; }
            else b.Encapsulate(rs[i].bounds);
        }
        if (!bulundu) return;

        Vector3 burun = transform.forward;
        float sMin = float.MaxValue, sMax = float.MinValue;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3(x, y, z));
                    float s = Vector3.Dot(c - b.center, burun);
                    sMin = Mathf.Min(sMin, s);
                    sMax = Mathf.Max(sMax, s);
                }
        float boy = Mathf.Max(sMax - sMin, 0.5f);
        Vector3 nokta = b.center + burun * (sMax - iticiKonumu * boy);
        float yaricap = iticiHalkaOrani * boy;

        iticiler = new ParticleSystem[6];
        iticiYerelYon = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            Vector3 dis = (transform.right * Mathf.Cos(a) + transform.up * Mathf.Sin(a)).normalized;
            iticiYerelYon[i] = transform.InverseTransformDirection(dis);
            iticiler[i] = Efektler.Ornek.IticiYap(transform, nokta + dis * yaricap, dis, boy * iticiOlcegi);
        }
    }

    void IticiAtesle(Vector3 egilmeYonu)
    {
        if (iticiler == null) return;
        // Fuze egilmeYonu'ne donecekse, TERS taraftaki itici ates eder (iticinin itkisi burnu o yone iter)
        float isaret = iticiTersCevir ? 1f : -1f;
        Camera c = Camera.main;
        string atesEden = "";

        for (int i = 0; i < iticiler.Length; i++)
        {
            if (iticiler[i] == null) continue;
            Vector3 dis = transform.TransformDirection(iticiYerelYon[i]);
            if (Vector3.Dot(dis, egilmeYonu * isaret) > 0.55f)
            {
                iticiler[i].Play(true);
                if (c != null)
                {
                    float y = Vector3.Dot(dis, c.transform.right);
                    atesEden += y > 0.2f ? " SAG" : (y < -0.2f ? " SOL" : " ON/ARKA");
                }
            }
        }

        if (bilgiYaz && c != null)
        {
            float y = Vector3.Dot(egilmeYonu, c.transform.right);
            Debug.Log("Tor itici: fuze burnunu kameraya gore " + (y > 0f ? "SAGA" : "SOLA") + " yatiriyor. Ates eden itici:" + atesEden + "  (kameraya gore)");
        }
    }

    void IticiDurdur()
    {
        if (iticiler == null) return;
        for (int i = 0; i < iticiler.Length; i++)
            if (iticiler[i] != null) iticiler[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void OnDrawGizmos()
    {
        if (!iticiNoktalariniGoster || iticiler == null) return;
        for (int i = 0; i < iticiler.Length; i++)
        {
            if (iticiler[i] == null) continue;
            Gizmos.color = iticiler[i].isEmitting ? Color.green : Color.yellow;   // yesil = su an ates ediyor
            Gizmos.DrawWireSphere(iticiler[i].transform.position, 0.04f);
            Gizmos.DrawRay(iticiler[i].transform.position, iticiler[i].transform.forward * 0.3f);
        }
        Gizmos.color = Color.magenta;                                              // mor cizgi = burnun yatacagi yon
        Gizmos.DrawRay(transform.position, sonYatay * 3f);
    }

    // Tor atesleyicisi cagirir: fuzenin gidecegi ucak
    public void HedefAta(HedefUcus h)
    {
        gudumHedef = h;
    }

    bool LazerUstunde(HedefUcus h)
    {
        if (!kumandaVar) return false;
        float aci = Vector3.Angle(losYon, h.transform.position - losKaynak);
        return aci < chaffKilitAcisi;
    }

    // Ucak chaff atti: fuze ucagi birakip chaff'e kayabilir
    void ChaffAldat(HedefUcus ucak, HedefUcus chaff)
    {
        if (bitti || gudumHedef != ucak) return;
        if (Random.value < aldanmaOlasiligi) gudumHedef = chaff;
    }

    bool Vurulabilir(HedefUcus h)
    {
        if (kendiGudumlu) return !h.sahte || h == gudumHedef;
        return !h.sahte || LazerUstunde(h);
    }

    // a-b dogru parcasinin p noktasina en kisa mesafesi (hizli fuzenin ucagi "atlamamasi" icin)
    static float YakinMesafe(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float ab2 = ab.sqrMagnitude;
        float t = ab2 > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab2) : 0f;
        return Vector3.Distance(a + ab * t, p);
    }

    void Update()
    {
        if (bitti) return;
        float dt = Time.deltaTime;

        if (Time.time - dogum > omur)
        {
            Bitir();
            return;
        }

        Vector3 onceki = transform.position;

        if (kendiGudumlu) KendiGudum(dt);
        else LazerGudum(dt);

        Vector3 simdiki = transform.position;

        foreach (var h in HedefUcus.tumu)
        {
            if (h == null || h.olu) continue;
            if (!Vurulabilir(h)) continue;
            if (YakinMesafe(onceki, simdiki, h.transform.position) < vurmaMesafesi)
            {
                Patla(h);
                return;
            }
        }

        if (simdiki.y < 0.5f) Patla(null);
    }

    // ---------------- Lazer gudumlu (Pantsir) ----------------
    void LazerGudum(float dt)
    {
        if (beyazIz != null && Time.time - dogum > beyazDumanSuresi)
        {
            beyazIz.Stop(true, ParticleSystemStopBehavior.StopEmitting);   // beyaz iz biter, fuzenin kendi efekti devam eder
            beyazIz = null;
        }

        BaglantiVar = false;

        if (kumandaVar)
        {
            Vector3 rel = transform.position - losKaynak;
            float boyuna = Vector3.Dot(rel, losYon);
            float yanal = (rel - losYon * boyuna).magnitude;
            float izin = minGenislik + Mathf.Max(boyuna, 0f) * Mathf.Tan(lazerKoniAcisi * Mathf.Deg2Rad);
            BaglantiVar = boyuna > 0f && yanal < izin;
        }

        if (BaglantiVar)
        {
            float ilerleme = Vector3.Dot(transform.position - losKaynak, losYon);
            Vector3 nokta = losKaynak + losYon * (ilerleme + ileriBakis);
            Vector3 hedefYon = nokta - transform.position;
            if (hedefYon.sqrMagnitude > 0.0001f)
            {
                Quaternion istenen = Quaternion.LookRotation(hedefYon.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, istenen, donusHizi * dt);
            }
        }

        transform.position += transform.forward * hiz * dt;
    }

    // ---------------- Kendi gudumlu (Tor M1) ----------------
    void KendiGudum(float dt)
    {
        bool hedefVar = gudumHedef != null && !gudumHedef.olu;
        BaglantiVar = hedefVar;

        if (faz == Faz.Kalkis)
        {
            // SABIT hizla dumduz yukari (yavaslama yok, durma yok)
            float t = Mathf.Min(Time.time - fazBas, kalkisSure);
            transform.position = kalkisBas + kalkisYonu * (kalkisHizi * t);

            // Yukselirken burun hedefe dogru yatar: tepeye vardiginda egilmeAcisi kadar yatmis olur
            float u = Mathf.Clamp01(t / kalkisSure);
            float k = Mathf.SmoothStep(0f, 1f, u);
            Vector3 yatay; float aci;
            EgilmeHesapla(out yatay, out aci);
            float r = aci * Mathf.Deg2Rad;
            Vector3 burunSon = kalkisYonu * Mathf.Cos(r) + yatay * Mathf.Sin(r);
            Quaternion son = Quaternion.FromToRotation(kalkisYonu, burunSon) * egilmeBasRot;
            transform.rotation = Quaternion.Slerp(egilmeBasRot, son, k);

            if (Time.time - fazBas >= kalkisSure) BoosterAc();   // tepede ARA VERMEDEN booster
            return;
        }

        // Booster: hiz baslangicHizi -> hiz, boosterSuresi icinde kademeli
        hizAnlik = Mathf.MoveTowards(hizAnlik, hiz, boosterIvme * dt);

        if (hedefVar)
        {
            Vector3 d = Kestirim(transform.position) - transform.position;
            if (d.sqrMagnitude > 0.01f)
            {
                // Eksen etrafinda dolanmadan (roll yapmadan) en kisa donus
                Quaternion istenen = Quaternion.FromToRotation(transform.forward, d.normalized) * transform.rotation;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, istenen, gudumDonusHizi * dt);
            }
        }

        // Hareket yonu, atalet yuzunden burnu biraz gec takip eder (dikten egime yumusak gecis)
        hizYonu = Vector3.Slerp(hizYonu, transform.forward, 1f - Mathf.Exp(-hizYonuUyumu * dt)).normalized;
        transform.position += hizYonu * hizAnlik * dt;
    }

    void BoosterAc()
    {
        faz = Faz.Booster;
        IticiDurdur();

        hizAnlik = Mathf.Max(baslangicHizi, kalkisHizi);              // yavaslama yok: asla kalkis hizinin altina inmez
        boosterIvme = Mathf.Max(hiz - hizAnlik, 1f) / Mathf.Max(boosterSuresi, 0.5f);
        hizYonu = kalkisYonu;        // kalkis sirasindaki hareket yonu: dikey. Yon burna dogru yumusakca doner

        if (bilgiYaz)
            Debug.Log("Tor fuzesi: BOOSTER yandi. Hucreden yukseklik = " + Vector3.Distance(kalkisBas, transform.position).ToString("F1")
                + " m, hiz " + (hizAnlik * 3.6f).ToString("F0") + " km/h -> " + (hiz * 3.6f).ToString("F0") + " km/h (" + boosterSuresi.ToString("F1") + " sn)");

        if (motorSistemleri != null)
            for (int i = 0; i < motorSistemleri.Length; i++)
                if (motorSistemleri[i] != null) motorSistemleri[i].Play(true);

        if (beyazDuman) beyazIz = Efektler.Ornek.FuzeIziEkle(transform, beyazDumanBoyutu);   // ucus boyunca beyaz iz
        Efektler.Ornek.FuzeCikisi(transform.position - transform.forward * 0.5f, -transform.forward, boosterAlevOlcegi);
        if (sesEfekti) SesYoneticisi.Ornek.DonguEkle(gameObject, "fuze_motor", motorSesi, motorMenzili);
    }

    // Hedefin gelecekteki konumu: fuze tam hizla gitse ucagin onune varir
    Vector3 Kestirim(Vector3 poz)
    {
        Vector3 P = gudumHedef.transform.position;
        Vector3 V = gudumHedef.yon * gudumHedef.hiz;
        float v = Mathf.Max(hiz, 50f);
        float t = Vector3.Distance(poz, P) / v;
        for (int i = 0; i < 3; i++) t = Vector3.Distance(poz, P + V * t) / v;
        return P + V * t;
    }

    void Patla(HedefUcus vurulan)
    {
        if (patlamaPrefab != null)
            Efektler.Ornek.PrefabOynat(patlamaPrefab, transform.position, Quaternion.identity);
        if (sesEfekti)
            SesYoneticisi.Ornek.PatlamaCal(transform.position, patlamaSesi, Random.Range(0.92f, 1.08f), 2500f);
        if (vurulan != null) vurulan.Vurul();
        Bitir();
    }

    // Duman izini fuzeden once koparir, sonra fuzeyi yok eder (her iki bitis yolu da buradan gecer)
    void Bitir()
    {
        if (bitti) return;
        bitti = true;
        tumu.Remove(this);

        DumanBirakici db = GetComponent<DumanBirakici>();
        if (db != null) db.Birak();

        Destroy(gameObject);
    }
}
