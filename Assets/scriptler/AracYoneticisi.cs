using UnityEngine;
using UnityEngine.InputSystem;

// Sahnede bos bir objeye (ornek: "AracYoneticisi") eklenir.
// TAB ile araclar arasinda gecis: aktif aracin kontrolu, kamerasi, radari ve silahlari acilir,
// digerlerinin girisi kapanir (hareket eden arac frenleyip durur).
// Ileride diger araclari yapay zeka kontrol edecek: sadece "girisAktif = false" olan araclara AI baglanir.
public class AracYoneticisi : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public string isim = "Arac";

        [Header("Zorunlu")]
        public AracHareket arac;                 // aracin kok objesi
        public Transform taretGovdesi;           // radarin merkezi ve kameranin takip noktasi
        public TaretKontrol taret;               // taret objesindeki TaretKontrol

        [Header("Silahlar (aracta olmayani BOS birak)")]
        public MakineliTufek makineli;
        public FuzeAtesle pantsirFuze;
        public TorAtesle torFuze;

        [Header("Kamera (bu aracta)")]
        public Transform kameraHedefi;           // bos = taretGovdesi
        public Vector3 hedefOfseti = Vector3.zero;
        public float kameraMesafesi = 14f;
        public float kameraYuksekligi = 1.5f;
        public Vector3 nisanOfseti = new Vector3(0f, 4.5f, 0f);
    }

    public Slot[] araclar;
    public int baslangic = 0;
    public Key degistirTusu = Key.Tab;

    [Header("Baglantilar (bos birakirsan otomatik bulunur)")]
    public KameraKontrol kamera;
    public RadarEkrani radar;
    public FuzeKamera fuzeKamera;
    public Nisangah nisangah;

    public int Aktif { get; private set; }

    void Start()
    {
        if (kamera == null) kamera = FindFirstObjectByType<KameraKontrol>();
        if (radar == null) radar = FindFirstObjectByType<RadarEkrani>();
        if (fuzeKamera == null) fuzeKamera = FindFirstObjectByType<FuzeKamera>();
        if (nisangah == null) nisangah = FindFirstObjectByType<Nisangah>();

        if (araclar == null || araclar.Length == 0) return;
        Sec(Mathf.Clamp(baslangic, 0, araclar.Length - 1));
    }

    void Update()
    {
        if (araclar == null || araclar.Length < 2) return;
        var k = Keyboard.current;
        if (k == null || !k[degistirTusu].wasPressedThisFrame) return;

        // Havadaki fuze arac degistirmeyi engellemez. Fuze ucusuna devam eder;
        // oyuncu istedigi zaman diger hava savunma aracina gecebilir.
        Sec((Aktif + 1) % araclar.Length);
    }

    public void Sec(int n)
    {
        Aktif = n;

        for (int i = 0; i < araclar.Length; i++)
        {
            Slot s = araclar[i];
            if (s == null) continue;
            bool a = i == n;

            if (s.arac != null) s.arac.girisAktif = a;
            if (s.taret != null) s.taret.girisAktif = a;
            if (s.makineli != null) s.makineli.girisAktif = a;
            if (s.pantsirFuze != null) s.pantsirFuze.girisAktif = a;
            if (s.torFuze != null) s.torFuze.girisAktif = a;
        }

        Slot yeni = araclar[n];
        if (yeni == null) return;

        Transform t = yeni.kameraHedefi != null ? yeni.kameraHedefi : yeni.taretGovdesi;
        if (kamera != null)
        {
            kamera.hedef = t;
            kamera.hedefOfseti = yeni.hedefOfseti;
            kamera.mesafe = yeni.kameraMesafesi;
            kamera.yukseklik = yeni.kameraYuksekligi;
            kamera.nisanOfseti = yeni.nisanOfseti;
        }
        if (radar != null) radar.arac = yeni.taretGovdesi;
        if (fuzeKamera != null) fuzeKamera.losKaynagi = yeni.taretGovdesi;
        if (nisangah != null) nisangah.taret = yeni.taret;
    }

    void OnGUI()
    {
        if (araclar == null || araclar.Length == 0 || Aktif >= araclar.Length || araclar[Aktif] == null) return;

        GUIStyle s = new GUIStyle(GUI.skin.label);
        s.alignment = TextAnchor.LowerCenter;
        s.fontSize = Mathf.RoundToInt(Screen.height * 0.022f);
        s.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
        GUI.Label(new Rect(0f, Screen.height - 50f, Screen.width, 40f),
            "ARAC: " + araclar[Aktif].isim + "     [" + degistirTusu + "] degistir", s);
    }
}
