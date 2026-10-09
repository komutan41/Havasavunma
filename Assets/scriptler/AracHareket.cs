using UnityEngine;
using UnityEngine.InputSystem;

// Aracin ana (kok) objesine eklenir.
// W/S veya yukari/asagi ok: gaz-fren-geri   A/D veya sag/sol ok: direksiyon
//
// IKI MOD VAR:
//  1) Tekerlekli (Pantsir): direksiyon + dingiller. "Paletli Mod" KAPALI.
//  2) Paletli (Tor M1):     "Paletli Mod" ACIK. A/D ile arac oldugu yerde doner:
//                           saga donerken sol paletin tekerleri one, sag paletin tekerleri geri doner.
//                           Palet bandi dokusu da kayar.
//
// Suspansiyon: virajda yatma, frende one egilme, gazda hafif geriye cokme (her iki modda).
public class AracHareket : MonoBehaviour
{
    [System.Serializable]
    public class Dingil
    {
        public string isim = "Dingil";
        public Transform solTeker;
        public Transform sagTeker;
        [Range(0f, 1f)] public float direksiyonOrani = 0f;   // 1 = tam aci, 0 = sabit
    }

    [System.Serializable]
    public class PaletTeker
    {
        public Transform teker;
        public float yaricap = 0.31f;                         // metre: tekerin yaricapi (donus hizini belirler)
        [HideInInspector] public float aci;
        [HideInInspector] public Quaternion baz = Quaternion.identity;
    }

    [Header("Kontrol")]
    public bool girisAktif = true;        // AracYoneticisi arac degisince kapatir: arac fren yapip durur

    [Header("Hiz")]
    public float maxIleriHiz = 14f;       // m/sn (yaklasik 50 km/s)
    public float maxGeriHiz = 6f;
    public float ivme = 5f;               // gaza basinca
    public float suruklenmeIvmesi = 3f;   // tusu birakinca kendi kendine yavaslama
    public float frenIvmesi = 10f;        // ters tusa basinca (fren)

    [Header("Direksiyon (tekerlekli mod)")]
    public float dingilMesafesi = 6f;         // on ile arka dingil arasi (m): kucuk = keskin donus
    public float maxDireksiyonAcisi = 30f;    // duruyorken en onun teker acisi (derece)
    [Range(0.1f, 1f)] public float yuksekHizdaOran = 0.35f;   // tam hizda direksiyon acisi bu orana kadar kisilir
    public float direksiyonHizi = 40f;        // derece/sn: tusa basinca direksiyonun donme hizi
    public float direksiyonGeriDonus = 60f;   // derece/sn: tusu birakinca duzelme hizi
    public float donusYumusakligi = 0.4f;     // saniye: buyudukce govde daha agir doner

    [Header("Model yonu")]
    public Vector3 ileriEksen = new Vector3(0f, 0f, 1f);   // modelin on tarafi hangi yerel eksende (0,0,1 = mavi ok)

    [Header("Tekerlekler (tekerlekli mod: on -> arka)")]
    public Dingil[] dingiller = new Dingil[]
    {
        new Dingil { isim = "1. dingil (en on)", direksiyonOrani = 1.0f },
        new Dingil { isim = "2. dingil",          direksiyonOrani = 0.6f },
        new Dingil { isim = "3. dingil",          direksiyonOrani = 0f },
        new Dingil { isim = "4. dingil (arka)",   direksiyonOrani = 0f },
    };
    public float tekerYaricapi = 0.6f;                       // metre (donus hizini belirler)
    public Vector3 tekerEkseni = new Vector3(1f, 0f, 0f);    // tekerin KENDI yerel ekseni: aks hangi eksen boyunca
    public bool solTersCevir = false;                        // sol tekerler yanlis yone donuyorsa isaretle
    public bool sagTersCevir = false;                        // sag tekerler yanlis yone donuyorsa isaretle

    [Header("PALETLI MOD (Tor M1)")]
    public bool paletliMod = false;
    public float paletAraligi = 2.75f;                       // iki palet ortasi arasi mesafe (m)
    public float maxDonusHizi = 35f;                         // derece/sn: oldugu yerde donus hizi
    [Range(0.1f, 1f)] public float yuksekHizdaDonusOrani = 0.6f;   // tam hizda donus bu orana kadar yavaslar
    public PaletTeker[] solTekerler;                         // sol paletin TUM tekerleri (on, alt tekerler, arka)
    public PaletTeker[] sagTekerler;
    public Renderer solPalet;                                // palet bandi mesh'i (dokusu kayacak)
    public Renderer sagPalet;
    public Vector2 paletKaymaYonu = new Vector2(0f, 1f);     // doku hangi yonde kaysin (UV): (0,1) dikey, (1,0) yatay
    public float paletDokuOrani = 0.25f;                     // 1 metre yolda doku ne kadar kaysin
    public bool solPaletTers = false;                        // sol palet dokusu ters kayiyorsa isaretle
    public bool sagPaletTers = false;
    [Range(0.05f, 1f)] public float tekerDonusCarpani = 0.3f;   // palet tekerlerinin donus hizi: 1 = gercek hiz (cok hizli gorunur), kucuk = yavas
    public float maxTekerDonusHizi = 360f;                       // bir tekerin en fazla donus hizi (derece/sn)

    [Header("Suspansiyon (cok hafif tut)")]
    public Transform govde;                                  // tekerler haric govde (tarete sahip obje); bos = suspansiyon kapali
    public Vector3 pivotOfseti = Vector3.zero;               // egilme merkezi (aracin kok objesine gore)
    public float yatmaDerece = 3f;                           // 1 g yanal ivmede yatma (derece)
    public float yatmaMax = 2.5f;
    public float egilmeDerece = 1.5f;                        // 1 g boyuna ivmede one/arkaya egilme (derece)
    public float egilmeMax = 2f;
    public float yay = 35f;                                  // buyudukce suspansiyon sertlesir
    public float sonum = 7f;                                 // buyudukce sallanma azalir

    public float Hiz { get; private set; }
    public float BoyunaIvme { get { return boyunaIvme; } }   // antenler vb. icin
    public float YanalIvme { get { return yanalIvme; } }

    float direksiyonAcisi;     // en on dingilin anlik acisi (+ = saga)
    float yawHizi;             // derece/sn
    float boyunaIvme;
    float yanalIvme;
    float tekerAci;
    float etkinOran = 1f;
    float solHiz, sagHiz;      // paletli mod: her paletin yer hizi (m/sn)

    float pitchAci, pitchHizi, rollAci, rollHizi;
    Quaternion eskiTilt = Quaternion.identity;

    Quaternion[] solBaz;
    Quaternion[] sagBaz;
    Material solMat, sagMat;

    void Start()
    {
        // Fizik motoru ile elle hareket cakismasin
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.isKinematic = true;
            Debug.Log("AracHareket: Rigidbody 'Is Kinematic' yapildi.");
        }

        // Aracin donusunu belirleyen ortalama direksiyon orani (on iki dingil)
        float top = 0f;
        int adet = 0;
        for (int i = 0; i < dingiller.Length; i++)
        {
            if (dingiller[i] != null && dingiller[i].direksiyonOrani > 0.01f)
            {
                top += dingiller[i].direksiyonOrani;
                adet++;
            }
        }
        etkinOran = adet > 0 ? top / adet : 0f;

        // Tekerlerin baslangic duruslari (arac kokune gore)
        solBaz = new Quaternion[dingiller.Length];
        sagBaz = new Quaternion[dingiller.Length];
        Quaternion kokTers = Quaternion.Inverse(transform.rotation);
        for (int i = 0; i < dingiller.Length; i++)
        {
            if (dingiller[i] == null) continue;
            if (dingiller[i].solTeker != null) solBaz[i] = kokTers * dingiller[i].solTeker.rotation;
            if (dingiller[i].sagTeker != null) sagBaz[i] = kokTers * dingiller[i].sagTeker.rotation;
        }

        // Paletli mod tekerleri
        BazAl(solTekerler, kokTers);
        BazAl(sagTekerler, kokTers);
        if (solPalet != null) solMat = solPalet.material;   // kendi kopyasi: diger araclari etkilemesin
        if (sagPalet != null) sagMat = sagPalet.material;
    }

    void BazAl(PaletTeker[] liste, Quaternion kokTers)
    {
        if (liste == null) return;
        for (int i = 0; i < liste.Length; i++)
        {
            if (liste[i] == null || liste[i].teker == null) continue;
            liste[i].baz = kokTers * liste[i].teker.rotation;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        float gaz = 0f;
        float dir = 0f;
        var k = Keyboard.current;
        if (k != null && girisAktif)
        {
            if (k.wKey.isPressed || k.upArrowKey.isPressed) gaz += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) gaz -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) dir += 1f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) dir -= 1f;
        }

        // --- Hiz ---
        float hedef = gaz > 0f ? gaz * maxIleriHiz : gaz * maxGeriHiz;
        float oran;
        if (Mathf.Abs(gaz) < 0.01f) oran = girisAktif ? suruklenmeIvmesi : frenIvmesi;   // tus yok: yavasca suruklenir (kontrol baska araca gectiyse fren)
        else if (gaz * Hiz < -0.1f) oran = frenIvmesi;                 // ters yone basildi: fren
        else oran = ivme;                                              // gaz

        float eskiHiz = Hiz;
        Hiz = Mathf.MoveTowards(Hiz, hedef, oran * dt);
        float anlikIvme = dt > 0.0001f ? (Hiz - eskiHiz) / dt : 0f;
        boyunaIvme = Mathf.Lerp(boyunaIvme, anlikIvme, 1f - Mathf.Exp(-dt / 0.08f));

        if (paletliMod)
        {
            // --- Paletli donus: oldugu yerde donebilir ---
            float hizOrani = Mathf.Clamp01(Mathf.Abs(Hiz) / Mathf.Max(maxIleriHiz, 0.1f));
            float hedefYaw = dir * maxDonusHizi * Mathf.Lerp(1f, yuksekHizdaDonusOrani, hizOrani);
            yawHizi = Mathf.Lerp(yawHizi, hedefYaw, 1f - Mathf.Exp(-dt / Mathf.Max(donusYumusakligi, 0.01f)));
            transform.Rotate(0f, yawHizi * dt, 0f, Space.World);

            // Saga donus (yaw +): sol palet hizlanir, sag palet yavaslar / geri doner
            float yawRad = yawHizi * Mathf.Deg2Rad;
            solHiz = Hiz + yawRad * paletAraligi * 0.5f;
            sagHiz = Hiz - yawRad * paletAraligi * 0.5f;
        }
        else
        {
            // --- Direksiyon: yavas doner, hiz arttikca kisilir ---
            float hizOrani = Mathf.Clamp01(Mathf.Abs(Hiz) / maxIleriHiz);
            float sinir = maxDireksiyonAcisi * Mathf.Lerp(1f, yuksekHizdaOran, hizOrani);
            float hedefAci = dir * sinir;
            float adim = (Mathf.Abs(dir) > 0.01f ? direksiyonHizi : direksiyonGeriDonus) * dt;
            direksiyonAcisi = Mathf.MoveTowards(direksiyonAcisi, hedefAci, adim);
            direksiyonAcisi = Mathf.Clamp(direksiyonAcisi, -sinir, sinir);

            // --- Donus: hiz ve direksiyon acisindan (geri giderken ters yone doner) ---
            float etkin = direksiyonAcisi * etkinOran;
            float hedefYaw = Mathf.Rad2Deg * Hiz / Mathf.Max(dingilMesafesi, 0.1f) * Mathf.Tan(etkin * Mathf.Deg2Rad);
            yawHizi = Mathf.Lerp(yawHizi, hedefYaw, 1f - Mathf.Exp(-dt / Mathf.Max(donusYumusakligi, 0.01f)));
            transform.Rotate(0f, yawHizi * dt, 0f, Space.World);
        }

        yanalIvme = Hiz * yawHizi * Mathf.Deg2Rad;   // + = saga viraj

        // --- Ilerleme ---
        Vector3 ileri = transform.TransformDirection(EksenNorm());
        ileri.y = 0f;
        if (ileri.sqrMagnitude > 0.0001f) ileri.Normalize();
        transform.position += ileri * Hiz * dt;
    }

    void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        if (dt <= 0f) return;

        SuspansiyonGuncelle(dt);   // once govde (tekerler govdenin cocugu olsa bile)
        if (paletliMod) PaletleriGuncelle(dt);
        else TekerleriGuncelle(dt);     // sonra tekerler yerde kalsin diye dik durus verilir
    }

    Vector3 EksenNorm()
    {
        return ileriEksen.sqrMagnitude > 0.001f ? ileriEksen.normalized : Vector3.forward;
    }

    void SuspansiyonGuncelle(float dt)
    {
        if (govde == null) return;

        const float g = 9.81f;

        // Virajda disa yatar (sagdan donerken sola), frende one egilir, gazda hafif arkaya cokar
        float rollHedef = Mathf.Clamp(yanalIvme / g * yatmaDerece, -yatmaMax, yatmaMax);
        float pitchHedef = Mathf.Clamp(-boyunaIvme / g * egilmeDerece, -egilmeMax, egilmeMax);

        // yay + sonum: hafif sallanip oturur
        float aR = (rollHedef - rollAci) * yay - rollHizi * sonum;
        rollHizi += aR * dt;
        rollAci += rollHizi * dt;

        float aP = (pitchHedef - pitchAci) * yay - pitchHizi * sonum;
        pitchHizi += aP * dt;
        pitchAci += pitchHizi * dt;

        rollAci = Mathf.Clamp(rollAci, -yatmaMax * 1.5f, yatmaMax * 1.5f);
        pitchAci = Mathf.Clamp(pitchAci, -egilmeMax * 1.5f, egilmeMax * 1.5f);

        Vector3 ileri = EksenNorm();
        Vector3 sag = Vector3.Cross(Vector3.up, ileri);
        Quaternion tilt = Quaternion.AngleAxis(pitchAci, sag) * Quaternion.AngleAxis(rollAci, ileri);

        // Yalnizca fark uygulanir: govde uzerindeki diger scriptlerin (taret vb.) hareketine karismaz
        Quaternion kok = transform.rotation;
        Quaternion w = kok * (tilt * Quaternion.Inverse(eskiTilt)) * Quaternion.Inverse(kok);
        Vector3 pivot = transform.TransformPoint(pivotOfseti);
        govde.position = pivot + w * (govde.position - pivot);
        govde.rotation = w * govde.rotation;
        eskiTilt = tilt;
    }

    void TekerleriGuncelle(float dt)
    {
        // Ayni yaricap: tum tekerler ayni hizla doner. Yol = hiz * sure, aci = yol / yaricap
        tekerAci += Hiz * dt / Mathf.Max(tekerYaricapi, 0.05f) * Mathf.Rad2Deg;
        tekerAci = Mathf.Repeat(tekerAci, 360f);

        Vector3 aks = tekerEkseni.sqrMagnitude > 0.001f ? tekerEkseni.normalized : Vector3.right;

        for (int i = 0; i < dingiller.Length; i++)
        {
            Dingil d = dingiller[i];
            if (d == null) continue;

            Quaternion dire = Quaternion.AngleAxis(direksiyonAcisi * d.direksiyonOrani, Vector3.up);

            if (d.solTeker != null)
            {
                float a = solTersCevir ? -tekerAci : tekerAci;
                d.solTeker.rotation = transform.rotation * dire * solBaz[i] * Quaternion.AngleAxis(a, aks);
            }
            if (d.sagTeker != null)
            {
                float a = sagTersCevir ? -tekerAci : tekerAci;
                d.sagTeker.rotation = transform.rotation * dire * sagBaz[i] * Quaternion.AngleAxis(a, aks);
            }
        }
    }

    // Paletli mod: her paletin tekerleri kendi palet hiziyla doner (sag palet geri, sol palet ileri gibi)
    void PaletleriGuncelle(float dt)
    {
        Vector3 aks = tekerEkseni.sqrMagnitude > 0.001f ? tekerEkseni.normalized : Vector3.right;

        TekerleriDondur(solTekerler, solHiz, solTersCevir, aks, dt);
        TekerleriDondur(sagTekerler, sagHiz, sagTersCevir, aks, dt);

        PaletDokusuKaydir(solMat, solHiz * (solPaletTers ? -1f : 1f), dt);
        PaletDokusuKaydir(sagMat, sagHiz * (sagPaletTers ? -1f : 1f), dt);
    }

    void TekerleriDondur(PaletTeker[] liste, float yerHizi, bool ters, Vector3 aks, float dt)
    {
        if (liste == null) return;
        for (int i = 0; i < liste.Length; i++)
        {
            PaletTeker t = liste[i];
            if (t == null || t.teker == null) continue;

            float adim = yerHizi * dt / Mathf.Max(t.yaricap, 0.05f) * Mathf.Rad2Deg * tekerDonusCarpani;
            float enFazla = maxTekerDonusHizi * dt;
            adim = Mathf.Clamp(adim, -enFazla, enFazla);
            t.aci = Mathf.Repeat(t.aci + adim, 360f);
            float a = ters ? -t.aci : t.aci;
            t.teker.rotation = transform.rotation * t.baz * Quaternion.AngleAxis(a, aks);
        }
    }

    void PaletDokusuKaydir(Material m, float yerHizi, float dt)
    {
        if (m == null) return;
        Vector2 o = m.mainTextureOffset + paletKaymaYonu * (yerHizi * dt * paletDokuOrani);
        o.x = Mathf.Repeat(o.x, 1f);
        o.y = Mathf.Repeat(o.y, 1f);
        m.mainTextureOffset = o;
    }
}
