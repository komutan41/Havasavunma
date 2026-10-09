using System.Collections.Generic;
using UnityEngine;

public class HedefUcus : MonoBehaviour
{
    public static List<HedefUcus> tumu = new List<HedefUcus>();
    public static int vurus;
    public static System.Action<HedefUcus, HedefUcus> ChaffKilidiAldi;

    [Header("Genel")]
    public string isim = "HEDEF";
    public float hiz = 60f;
    public Vector3 yon = new Vector3(1f, 0f, 0f);
    public float basaDonMesafesi = 1500f;
    public float yenidenDogmaSuresi = 3f;

    [Header("Yapay zeka")]
    public bool kacisYapar = true;
    [Range(0f, 1f)] public float zeka = 0.5f;
    public float algilamaMesafesi = 1000f;
    public float chaffMesafesi = 700f;
    public int chaffSayisi = 6;
    public float chaffAraligi = 1.2f;
    [Range(0f, 1f)] public float chaffKilitOlasiligi = 0.7f;
    public float jinkSuresi = 1.6f;
    public float minIrtifa = 80f;
    public float maxIrtifa = 600f;
    public float yatisCarpani = 0.8f;
    [Range(1f, 12f)] public float maxG = 6f;        // en fazla kac G ile doner. Hizli ucak yavas doner (donus hizi = G x 9.81 / hiz)

    [Header("Chaff gorseli")]
    public int chaffParcaSayisi = 70;
    public float chaffParcaBoyutu = 1.2f;
    public float chaffYayilma = 12f;

    [HideInInspector] public bool sahte;
    [HideInInspector] public float sahteOmur = 8f;

    public bool olu { get; private set; }

    Vector3 baslangic;
    Vector3 ucusYonu;
    Renderer[] renderlar;

    float tehditBaslangic;
    bool tehditVar;
    float tehditYokSure;
    float sonrakiChaff;
    int atilanChaff;
    float yanTaraf = 1f;
    float dikey;
    float sonJink;
    float yatis;
    float sahteSayac;

    Transform[] parcalar;
    Vector3[] parcaHiz;
    Vector3[] parcaDonus;
    Vector3[] parcaBoyut;
    static Material[] chaffMat;

    void OnEnable() { tumu.Add(this); }
    void OnDisable() { tumu.Remove(this); }

    void Start()
    {
        baslangic = transform.position;
        if (yon.sqrMagnitude < 0.0001f) yon = Vector3.right;
        yon = yon.normalized;
        ucusYonu = yon;
        renderlar = GetComponentsInChildren<Renderer>();
        if (!sahte && GetComponent<UcakSesi>() == null) gameObject.AddComponent<UcakSesi>();
        if (!sahte) transform.rotation = Quaternion.LookRotation(yon, Vector3.up);
    }

    void Update()
    {
        if (olu) return;
        float dt = Time.deltaTime;

        if (sahte) { SahteGuncelle(dt); return; }
        Vector3 onceki = yon;
        YapayZeka(dt);
        IrtifaSinirla();

        Vector3 yeni = transform.position + yon * hiz * dt;
        yeni.y = Mathf.Max(yeni.y, minIrtifa);          // yerin altina asla inme
        transform.position = yeni;

        float donusHizi = Vector3.SignedAngle(onceki, yon, Vector3.up) / Mathf.Max(dt, 0.0001f);
        yatis = Mathf.Lerp(yatis, Mathf.Clamp(donusHizi * yatisCarpani, -60f, 60f), dt * 4f);
        transform.rotation = Quaternion.LookRotation(yon, Vector3.up) * Quaternion.Euler(0f, 0f, -yatis);

        if (Vector3.Distance(transform.position, baslangic) > basaDonMesafesi) Sifirla();
    }

    // Alcakta asagi inmeyi, yuksekte yukari cikmayi sonumler; dik dalis/tirmanisi sinirlar
    void IrtifaSinirla()
    {
        float y = transform.position.y;
        float altPay = Mathf.Clamp01((y - minIrtifa) / 100f);     // 0 = tabanda, 1 = rahat
        float ustPay = Mathf.Clamp01((maxIrtifa - y) / 100f);

        if (yon.y < 0f) yon.y *= altPay;
        if (yon.y > 0f) yon.y *= ustPay;
        yon.y = Mathf.Clamp(yon.y, -0.6f, 0.6f);

        Vector3 yatayV = new Vector3(yon.x, 0f, yon.z);
        if (yatayV.sqrMagnitude < 0.01f) yon = ucusYonu;
        yon = yon.normalized;
    }

    void SahteGuncelle(float dt)
    {
        sahteSayac += dt;
        hiz = Mathf.Max(0f, hiz * (1f - 0.6f * dt));

        Vector3 yeni = transform.position + (yon * hiz + Vector3.down * 4f) * dt;
        yeni.y = Mathf.Max(yeni.y, 1f);
        transform.position = yeni;

        if (parcalar != null)
        {
            float kuc = Mathf.Clamp01((sahteOmur - sahteSayac) / 1.5f);
            for (int i = 0; i < parcalar.Length; i++)
            {
                if (parcalar[i] == null) continue;
                parcalar[i].localPosition += parcaHiz[i] * dt;
                parcaHiz[i] *= (1f - 1.2f * dt);
                parcalar[i].Rotate(parcaDonus[i] * dt, Space.Self);
                parcalar[i].localScale = parcaBoyut[i] * kuc;
            }
        }

        if (sahteSayac >= sahteOmur)
        {
            olu = true;
            Destroy(gameObject);
        }
    }

    static Material[] ChaffMateryalleri()
    {
        if (chaffMat != null && chaffMat.Length > 0 && chaffMat[0] != null) return chaffMat;

        GameObject gecici = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material temel = gecici.GetComponent<Renderer>().sharedMaterial;
        Destroy(gecici);

        float[] tonlar = { 0.45f, 0.62f, 0.8f };
        chaffMat = new Material[tonlar.Length];
        for (int i = 0; i < tonlar.Length; i++)
        {
            chaffMat[i] = new Material(temel);
            chaffMat[i].color = new Color(tonlar[i], tonlar[i], tonlar[i]);
        }
        return chaffMat;
    }

    void ParcalariKur(int sayi, float maxBoyut, float yayilma)
    {
        Material[] mat = ChaffMateryalleri();
        parcalar = new Transform[sayi];
        parcaHiz = new Vector3[sayi];
        parcaDonus = new Vector3[sayi];
        parcaBoyut = new Vector3[sayi];

        for (int i = 0; i < sayi; i++)
        {
            bool yuvarlak = Random.value < 0.5f;
            GameObject p = GameObject.CreatePrimitive(yuvarlak ? PrimitiveType.Sphere : PrimitiveType.Cube);

            Collider col = p.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = p.GetComponent<Renderer>();
            r.sharedMaterial = mat[Random.Range(0, mat.Length)];
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            p.transform.SetParent(transform, false);
            p.transform.localPosition = Random.insideUnitSphere * 2f;
            p.transform.localRotation = Random.rotation;

            float s = Random.Range(maxBoyut * 0.4f, maxBoyut);
            parcaBoyut[i] = new Vector3(s, s * 0.15f, s);
            p.transform.localScale = parcaBoyut[i];

            parcaHiz[i] = Random.onUnitSphere * Random.Range(2f, yayilma);
            parcaDonus[i] = Random.onUnitSphere * Random.Range(90f, 360f);
            parcalar[i] = p.transform;
        }
    }

    FuzeHareket EnYakinTehdit(out float mesafe)
    {
        FuzeHareket en = null;
        mesafe = float.MaxValue;
        foreach (var m in FuzeHareket.tumu)
        {
            if (m == null) continue;
            Vector3 fark = transform.position - m.transform.position;
            float d = fark.magnitude;
            if (d > algilamaMesafesi || d < 0.1f) continue;
            if (Vector3.Dot(m.transform.forward, fark / d) < 0.3f) continue;
            if (d < mesafe) { mesafe = d; en = m; }
        }
        return en;
    }

    void YapayZeka(float dt)
    {
        FuzeHareket tehdit = null;
        float mesafe = float.MaxValue;
        if (kacisYapar) tehdit = EnYakinTehdit(out mesafe);

        if (tehdit != null)
        {
            if (!tehditVar)
            {
                tehditVar = true;
                tehditBaslangic = Time.time;
                sonJink = -100f;
            }
            tehditYokSure = 0f;
        }
        else if (tehditVar)
        {
            tehditYokSure += dt;
            if (tehditYokSure > 3f)
            {
                tehditVar = false;
                atilanChaff = 0;
            }
        }

        float tepki = Mathf.Lerp(2f, 0.3f, zeka);
        bool manevra = tehdit != null && Time.time - tehditBaslangic >= tepki;

        if (manevra)
        {
            if (Time.time - sonJink > jinkSuresi)
            {
                sonJink = Time.time;
                yanTaraf = Random.value < 0.5f ? -1f : 1f;
                dikey = Random.Range(-0.5f, 0.5f);
            }

            Vector3 fuzedenBana = (transform.position - tehdit.transform.position).normalized;
            Vector3 yanal = Vector3.Cross(Vector3.up, fuzedenBana);
            if (yanal.sqrMagnitude < 0.01f) yanal = Vector3.right;
            yanal = yanal.normalized;

            float dk = dikey;
            if (transform.position.y < minIrtifa + 100f) dk = Mathf.Abs(dk);      // alcaktaysa asla dalma
            if (transform.position.y > maxIrtifa - 100f) dk = -Mathf.Abs(dk);

            Vector3 istenen = (yanal * yanTaraf + Vector3.up * dk).normalized;
            float donus = Mathf.Min(Mathf.Lerp(25f, 70f, zeka), GSiniri());   // derece/sn, G ile sinirli
            yon = Vector3.RotateTowards(yon, istenen, donus * Mathf.Deg2Rad * dt, 0f).normalized;

            if (mesafe < chaffMesafesi && atilanChaff < chaffSayisi && Time.time >= sonrakiChaff)
            {
                ChaffAt();
                atilanChaff++;
                sonrakiChaff = Time.time + chaffAraligi;
            }
        }
        else
        {
            float hataY = baslangic.y - transform.position.y;
            Vector3 donusYonu = (ucusYonu + Vector3.up * Mathf.Clamp(hataY * 0.002f, -0.3f, 0.3f)).normalized;
            yon = Vector3.RotateTowards(yon, donusYonu, Mathf.Min(20f, GSiniri()) * Mathf.Deg2Rad * dt, 0f).normalized;
        }
    }

    // G sinirina gore bu hizda yapilabilecek en hizli donus (derece/sn)
    float GSiniri()
    {
        return Mathf.Rad2Deg * maxG * 9.81f / Mathf.Max(hiz, 1f);
    }

    void ChaffAt()
    {
        GameObject g = new GameObject("Chaff");
        g.transform.position = transform.position - yon * 15f;

        HedefUcus h = g.AddComponent<HedefUcus>();
        h.sahte = true;
        h.isim = isim;
        h.yon = yon;
        h.hiz = hiz * 0.6f;
        h.kacisYapar = false;
        h.sahteOmur = 8f;
        h.ParcalariKur(chaffParcaSayisi, chaffParcaBoyutu, chaffYayilma);

        if (Random.value < chaffKilitOlasiligi && ChaffKilidiAldi != null)
            ChaffKilidiAldi(this, h);
    }

    void Sifirla()
    {
        transform.position = baslangic;
        yon = ucusYonu;
        tehditVar = false;
        tehditYokSure = 0f;
        atilanChaff = 0;
        yatis = 0f;
    }

    public void Vurul()
    {
        if (olu) return;
        olu = true;

        if (sahte)
        {
            Destroy(gameObject);
            return;
        }

        vurus++;
        foreach (var r in renderlar) r.enabled = false;
        Invoke("Yeniden", yenidenDogmaSuresi);
    }

    void Yeniden()
    {
        Sifirla();
        foreach (var r in renderlar) r.enabled = true;
        olu = false;
    }
}