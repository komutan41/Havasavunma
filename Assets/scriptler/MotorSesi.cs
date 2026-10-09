using UnityEngine;

// Aracin ana (kok) objesine, AracHareket'in yanina eklenir.
// Iki motor katmani (alcak devir + yuksek devir) hiza gore birbirine karisir:
//   - Hizlanirken perde ve ses yumusakca yukselir (ivmelenirken biraz daha zorlanir)
//   - Yavaslarken yumusakca duser
//   - Sabit hizda ayni seste kalir
// Ucaklar icin de ayni script kullanilir: "arac" bos birakilip "girdi" disaridan (0-1) verilir.
// Sesler: Assets/Resources/Sesler/arac_motor_alcak.wav ve arac_motor_yuksek.wav
public class MotorSesi : MonoBehaviour
{
    [Header("Kaynak")]
    public AracHareket arac;                 // bos birakirsan ayni objedeki AracHareket otomatik bulunur
    [Range(0f, 1f)] public float girdi = 0f; // arac yoksa: baska scriptler buraya 0-1 arasi hiz/gaz yazar

    [Header("Sesler (Resources/Sesler)")]
    public string alcakSes = "arac_motor_alcak";
    public string yuksekSes = "arac_motor_yuksek";
    public float menzil = 600f;

    [Header("Perde (devir)")]
    public float alcakPerdeMin = 0.85f;      // duruyorken
    public float alcakPerdeMax = 1.5f;       // tam hizda
    public float yuksekPerdeMin = 0.8f;
    public float yuksekPerdeMax = 1.25f;

    [Header("Ses seviyesi")]
    public float rolanti = 0.30f;            // duruyorken
    public float tamGaz = 0.75f;             // tam hizda
    public float yukBonusu = 0.15f;          // hizlanirken eklenen ses

    [Header("Alcak/yuksek katman gecisi (devir orani)")]
    public float gecisBasi = 0.25f;
    public float gecisSonu = 0.70f;

    [Header("Tepki (saniye: buyudukce daha yavas)")]
    public float yukselmeSuresi = 0.9f;
    public float dusmeSuresi = 1.6f;
    public float yukTepkisi = 0.4f;          // bu kadar hizlanma (1/sn) = tam yuk

    AudioSource alcak;
    AudioSource yuksek;
    float oran;          // yumusatilmis devir orani (0-1)
    float oranHizi;
    float yuk;           // 0-1 yuklenme
    float oncekiHedef;

    void Start()
    {
        if (arac == null) arac = GetComponent<AracHareket>();

        alcak = SesYoneticisi.Ornek.DonguEkle(gameObject, alcakSes, 0f, menzil);
        yuksek = SesYoneticisi.Ornek.DonguEkle(gameObject, yuksekSes, 0f, menzil);

        // Baslangic degerlerini hemen uygula
        oran = Hedef();
        oncekiHedef = oran;
        Uygula();
    }

    float Hedef()
    {
        if (arac != null) return Mathf.Clamp01(Mathf.Abs(arac.Hiz) / Mathf.Max(arac.maxIleriHiz, 0.1f));
        return Mathf.Clamp01(girdi);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float hedef = Hedef();

        // Hizlanma: hedef orandaki artis hizi (1/sn)
        float artis = (hedef - oncekiHedef) / dt;
        oncekiHedef = hedef;
        float yukHedef = Mathf.Clamp01(artis / Mathf.Max(yukTepkisi, 0.01f));
        yuk = Mathf.Lerp(yuk, yukHedef, 1f - Mathf.Exp(-dt / 0.35f));

        // Devir yumusakca takip eder: yukselirken ve dusen devirde farkli sure
        float sure = hedef > oran ? yukselmeSuresi : dusmeSuresi;
        oran = Mathf.SmoothDamp(oran, hedef, ref oranHizi, Mathf.Max(sure, 0.05f));

        Uygula();
    }

    void Uygula()
    {
        float ana = SesYoneticisi.Ornek.anaSes;

        // Yuk altinda devir biraz yukselir
        float rpm = Mathf.Clamp01(oran + yuk * 0.08f);

        float gecis = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(gecisBasi, gecisSonu, rpm));
        float alcakK = 1f - gecis * 0.85f;
        float yuksekK = gecis;

        float genel = Mathf.Clamp01(Mathf.Lerp(rolanti, tamGaz, rpm) + yuk * yukBonusu);

        if (alcak != null)
        {
            alcak.pitch = Mathf.Lerp(alcakPerdeMin, alcakPerdeMax, rpm);
            alcak.volume = genel * alcakK * ana;
        }
        if (yuksek != null)
        {
            yuksek.pitch = Mathf.Lerp(yuksekPerdeMin, yuksekPerdeMax, rpm);
            yuksek.volume = genel * yuksekK * ana;
        }
    }
}
