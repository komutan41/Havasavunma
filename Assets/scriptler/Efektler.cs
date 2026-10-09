using System.Collections.Generic;
using UnityEngine;

// Sahneye eklemen GEREKMEZ, ilk kullanildiginda kendini olusturur ("Efektler" objesi).
// - Namlu alevi, namlu dumani, mermi isabet patlamasi, toprak tozu: kodla uretilir, havuzdan yeniden kullanilir.
// - PrefabOynat: fuze patlamasi gibi hazir prefab efektlerini her seferinde Instantiate/Destroy etmek yerine havuzdan oynatir.
public class Efektler : MonoBehaviour
{
    static Efektler ornek;
    public static Efektler Ornek
    {
        get
        {
            if (ornek == null)
            {
                GameObject g = new GameObject("Efektler");
                ornek = g.AddComponent<Efektler>();
            }
            return ornek;
        }
    }

    class Efekt
    {
        public GameObject obje;
        public ParticleSystem[] sistemler;
        public Light isik;
        public float isikSiddeti;
        public float isikMenzili;
        public float isikSuresi;
        public float sure;
        public float baslama;
        public float bitis;
        public Vector3 ilkOlcek = Vector3.one;
    }

    class Havuz
    {
        public List<Efekt> liste = new List<Efekt>();
        public int sinir;
        public System.Func<Efekt> uret;
    }

    class PSAyar
    {
        public string ad = "PS";
        public int adet = 5;
        public float omurMin = 0.5f, omurMax = 1f;
        public float hizMin = 0f, hizMax = 5f;
        public float boyMin = 0.5f, boyMax = 1f;
        public Color renkA = Color.white, renkB = Color.white;
        public float buyume = 1f;          // omrun sonunda boyut carpani (1 = ayni, 3 = 3 kat buyur, 0.3 = kuculur)
        public float yercekimi = 0f;       // negatif = yukari suzulur
        public ParticleSystemShapeType sekil = ParticleSystemShapeType.Sphere;
        public float aci = 20f;            // koni acisi
        public float yaricap = 0.1f;
        public bool cizgi = false;         // kivilcim gibi uzamis cizgi
        public float surtunme = 0f;        // 0 = yok, 0.1 = hizla yavaslar
    }

    Havuz alev, duman, isabet, toz, firlatma;
    List<Efekt> aktifler = new List<Efekt>();
    Dictionary<GameObject, List<Efekt>> prefabHavuzlari = new Dictionary<GameObject, List<Efekt>>();
    Material mat;

    void Awake()
    {
        ornek = this;
        mat = MateryalYap();

        alev = new Havuz { sinir = 20, uret = AlevYap };
        duman = new Havuz { sinir = 20, uret = DumanYap };
        isabet = new Havuz { sinir = 14, uret = IsabetYap };
        toz = new Havuz { sinir = 10, uret = TozYap };
        firlatma = new Havuz { sinir = 6, uret = FirlatmaYap };

        Doldur(alev, 3);
        Doldur(duman, 3);
        Doldur(isabet, 3);
        Doldur(toz, 2);
        Doldur(firlatma, 2);
    }

    // Oyun basinda tum efektleri bir kereligine kameranin onunde (cok kucuk) oynatir:
    // shader/isik/parcacik hazirligi simdi yapilir, ilk vurusta takilma olmaz.
    public bool oyunBasiIsinma = false;   // KAPALI: oyun basinda patlama gorunmez. Acarsan ilk vurustaki takilma azalir ama baslangicta parlama olur

    void Start()
    {
        if (!oyunBasiIsinma) return;
        Havuz[] hepsi = { alev, duman, isabet, toz, firlatma };
        for (int i = 0; i < hepsi.Length; i++)
            for (int j = 0; j < hepsi[i].liste.Count; j++) Isinma(hepsi[i].liste[j]);

        foreach (var kv in prefabHavuzlari)
            for (int j = 0; j < kv.Value.Count; j++) Isinma(kv.Value[j]);
    }

    void Isinma(Efekt e)
    {
        if (e == null || e.obje == null || e.obje.activeSelf) return;
        Camera c = Camera.main;
        Vector3 p = c != null ? c.transform.position + c.transform.forward * 3f : Vector3.zero;
        Baslat(e, p, Quaternion.identity, 0.02f);
        e.bitis = Time.time + 0.2f;
    }

    void Update()
    {
        float t = Time.time;
        for (int i = aktifler.Count - 1; i >= 0; i--)
        {
            Efekt e = aktifler[i];
            if (e.obje == null) { aktifler.RemoveAt(i); continue; }

            if (e.isik != null)
            {
                float k = 1f - (t - e.baslama) / Mathf.Max(e.isikSuresi, 0.01f);
                if (k > 0f) e.isik.intensity = e.isikSiddeti * k;
                else if (e.isik.enabled) e.isik.enabled = false;
            }

            if (t >= e.bitis)
            {
                e.obje.SetActive(false);
                aktifler.RemoveAt(i);
            }
        }
    }

    // ---------------- Disaridan cagrilanlar ----------------

    public void NamluAlevi(Vector3 poz, Vector3 yon)
    {
        Efekt e = Al(alev);
        if (e != null) Baslat(e, poz, YonDonusu(yon), 1f);
    }

    public void NamluDumani(Vector3 poz, Vector3 yon)
    {
        Efekt e = Al(duman);
        if (e != null) Baslat(e, poz, YonDonusu(yon), 1f);
    }

    public void Isabet(Vector3 poz, Vector3 normal, bool buyuk)
    {
        Efekt e = Al(isabet);
        if (e != null) Baslat(e, poz, YonDonusu(normal), UzakOlcek(poz) * (buyuk ? 2.5f : 1f));
    }

    // Fuze kovaninin arkasindan cikan buyuk duman bulutu + parlama (arkaYon = fuzenin gittigi yonun TERSI)
    public void FuzeCikisi(Vector3 poz, Vector3 arkaYon, float olcek)
    {
        Efekt e = Al(firlatma);
        if (e != null) Baslat(e, poz, YonDonusu(arkaYon), Mathf.Max(olcek, 0.1f));
    }

    // Fuzenin cocugu olarak beyaz duman izi ekler (fuze hareket ettikce mesafeye gore parcacik uretir)
    public ParticleSystem FuzeIziEkle(Transform fuze, float boyut)
    {
        GameObject g = new GameObject("BeyazDumanIzi");
        g.transform.SetParent(fuze, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(boyut * 0.7f, boyut);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.85f), new Color(0.88f, 0.88f, 0.88f, 0.7f));
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 600;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        em.rateOverDistance = 0.5f;

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gr);

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 4f));

        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        return ps;
    }

    public void ToprakToz(Vector3 poz)
    {
        Efekt e = Al(toz);
        if (e != null) Baslat(e, poz, Quaternion.LookRotation(Vector3.up), UzakOlcek(poz));
    }

    // Hazir prefab efektini (ornegin fuze patlamasi) havuzdan oynatir
    public void PrefabOynat(GameObject prefab, Vector3 poz, Quaternion rot)
    {
        if (prefab == null) return;
        List<Efekt> l = PrefabListesi(prefab);

        Efekt e = null;
        for (int i = 0; i < l.Count; i++)
        {
            if (l[i].obje != null && !l[i].obje.activeSelf) { e = l[i]; break; }
        }
        if (e == null)
        {
            e = PrefabYarat(prefab);
            l.Add(e);
        }
        Baslat(e, poz, rot, -1f);
    }

    // Oyun basinda onceden olusturur, ilk patlamada takilma olmaz
    public void Isit(GameObject prefab, int adet)
    {
        if (prefab == null) return;
        List<Efekt> l = PrefabListesi(prefab);
        while (l.Count < adet) l.Add(PrefabYarat(prefab));
    }

    // Fuze govdesindeki kucuk yon iticisi: fuzenin cocugu olur, Play()/Stop() ile kisa sureligine atesletilir.
    // dunyaYon = iticinin ates puskurttugu yon (govdeden DISARI). olcek ~ fuze boyu (m).
    public ParticleSystem IticiYap(Transform ebeveyn, Vector3 dunyaPoz, Vector3 dunyaYon, float olcek)
    {
        GameObject g = new GameObject("Itici");
        g.transform.SetParent(ebeveyn, true);
        g.transform.position = dunyaPoz;
        g.transform.rotation = dunyaYon.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dunyaYon) : Quaternion.identity;
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.10f, 0.20f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(olcek * 1.0f, olcek * 2.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(olcek * 0.05f, olcek * 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f, 1f), new Color(1f, 0.55f, 0.15f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 80;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 140f;

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 7f;
        sh.radius = olcek * 0.01f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.6f, 0.3f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gr);

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    // ---------------- Havuz islemleri ----------------

    List<Efekt> PrefabListesi(GameObject prefab)
    {
        List<Efekt> l;
        if (!prefabHavuzlari.TryGetValue(prefab, out l))
        {
            l = new List<Efekt>();
            prefabHavuzlari[prefab] = l;
        }
        return l;
    }

    Efekt PrefabYarat(GameObject prefab)
    {
        GameObject g = Instantiate(prefab, transform);
        PatlamaYoket py = g.GetComponent<PatlamaYoket>();
        if (py != null) py.havuzlu = true;
        g.SetActive(false);

        Efekt e = new Efekt();
        e.obje = g;
        e.ilkOlcek = g.transform.localScale;
        e.sistemler = g.GetComponentsInChildren<ParticleSystem>(true);
        e.sure = py != null ? py.omur : 6f;
        return e;
    }

    void Doldur(Havuz h, int n)
    {
        while (h.liste.Count < n) h.liste.Add(h.uret());
    }

    Efekt Al(Havuz h)
    {
        for (int i = 0; i < h.liste.Count; i++)
            if (!h.liste[i].obje.activeSelf) return h.liste[i];

        if (h.liste.Count < h.sinir)
        {
            Efekt yeni = h.uret();
            h.liste.Add(yeni);
            return yeni;
        }

        // hepsi mesgul: en eskisini yeniden kullan
        Efekt eski = h.liste[0];
        for (int i = 1; i < h.liste.Count; i++)
            if (h.liste[i].baslama < eski.baslama) eski = h.liste[i];
        return eski;
    }

    void Baslat(Efekt e, Vector3 poz, Quaternion rot, float olcek)
    {
        Transform t = e.obje.transform;
        t.SetPositionAndRotation(poz, rot);
        t.localScale = olcek > 0f ? Vector3.one * olcek : e.ilkOlcek;
        e.obje.SetActive(true);

        for (int i = 0; i < e.sistemler.Length; i++)
        {
            if (e.sistemler[i] == null) continue;
            e.sistemler[i].Clear(false);
            e.sistemler[i].Play(false);
        }

        if (e.isik != null)
        {
            e.isik.enabled = true;
            e.isik.intensity = e.isikSiddeti;
            e.isik.range = e.isikMenzili * (olcek > 0f ? olcek : 1f);
        }

        e.baslama = Time.time;
        e.bitis = Time.time + e.sure;
        if (!aktifler.Contains(e)) aktifler.Add(e);
    }

    Quaternion YonDonusu(Vector3 y)
    {
        return y.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(y) : Quaternion.identity;
    }

    // Uzaktaki efekt kucuk kalmasin: kameradan uzaklastikca buyur
    float UzakOlcek(Vector3 poz)
    {
        Camera c = Camera.main;
        if (c == null) return 1f;
        float d = Vector3.Distance(c.transform.position, poz);
        return Mathf.Clamp(1f + d * 0.004f, 1f, 4f);
    }

    // ---------------- Efekt tanimlari ----------------

    Efekt AlevYap()
    {
        Efekt e = YeniEfekt("NamluAlevi");
        PSYap(e, new PSAyar
        {
            ad = "Parlama", adet = 2, omurMin = 0.04f, omurMax = 0.07f, hizMin = 0f, hizMax = 3f,
            boyMin = 0.6f, boyMax = 1.1f, renkA = new Color(1f, 0.95f, 0.6f, 1f), renkB = new Color(1f, 0.7f, 0.25f, 1f),
            buyume = 1.6f, sekil = ParticleSystemShapeType.Cone, aci = 15f, yaricap = 0.02f
        });
        PSYap(e, new PSAyar
        {
            ad = "Kivilcim", adet = 4, omurMin = 0.05f, omurMax = 0.1f, hizMin = 25f, hizMax = 50f,
            boyMin = 0.08f, boyMax = 0.14f, renkA = new Color(1f, 0.9f, 0.5f, 1f), renkB = new Color(1f, 0.6f, 0.2f, 1f),
            buyume = 1f, sekil = ParticleSystemShapeType.Cone, aci = 10f, yaricap = 0.02f, cizgi = true
        });
        IsikEkle(e, new Color(1f, 0.75f, 0.35f), 2.5f, 10f, 0.06f);
        Tamamla(e, 0.3f);
        return e;
    }

    Efekt DumanYap()
    {
        Efekt e = YeniEfekt("NamluDumani");
        PSYap(e, new PSAyar
        {
            ad = "Duman", adet = 4, omurMin = 0.8f, omurMax = 1.4f, hizMin = 3f, hizMax = 7f,
            boyMin = 0.35f, boyMax = 0.7f, renkA = new Color(0.75f, 0.75f, 0.75f, 0.55f), renkB = new Color(0.9f, 0.9f, 0.9f, 0.4f),
            buyume = 3.2f, yercekimi = -0.02f, sekil = ParticleSystemShapeType.Cone, aci = 14f, yaricap = 0.03f, surtunme = 0.08f
        });
        Tamamla(e, 2f);
        return e;
    }

    Efekt IsabetYap()
    {
        Efekt e = YeniEfekt("IsabetPatlamasi");
        PSYap(e, new PSAyar
        {
            ad = "Parlama", adet = 1, omurMin = 0.08f, omurMax = 0.12f, hizMin = 0f, hizMax = 0f,
            boyMin = 2.5f, boyMax = 3.5f, renkA = new Color(1f, 0.95f, 0.7f, 1f), renkB = new Color(1f, 0.85f, 0.5f, 1f),
            buyume = 1.6f, yaricap = 0.05f
        });
        PSYap(e, new PSAyar
        {
            ad = "Ates", adet = 8, omurMin = 0.25f, omurMax = 0.45f, hizMin = 2f, hizMax = 9f,
            boyMin = 1f, boyMax = 2f, renkA = new Color(1f, 0.55f, 0.1f, 1f), renkB = new Color(1f, 0.3f, 0.05f, 1f),
            buyume = 1.5f, yaricap = 0.3f
        });
        PSYap(e, new PSAyar
        {
            ad = "Kivilcim", adet = 14, omurMin = 0.3f, omurMax = 0.7f, hizMin = 15f, hizMax = 40f,
            boyMin = 0.12f, boyMax = 0.22f, renkA = new Color(1f, 0.9f, 0.5f, 1f), renkB = new Color(1f, 0.5f, 0.1f, 1f),
            buyume = 0.6f, yercekimi = 0.6f, yaricap = 0.1f, cizgi = true
        });
        PSYap(e, new PSAyar
        {
            ad = "Duman", adet = 6, omurMin = 1f, omurMax = 1.8f, hizMin = 1f, hizMax = 4f,
            boyMin = 1.2f, boyMax = 2.2f, renkA = new Color(0.2f, 0.2f, 0.2f, 0.7f), renkB = new Color(0.4f, 0.4f, 0.4f, 0.6f),
            buyume = 2.2f, yercekimi = -0.03f, yaricap = 0.4f
        });
        IsikEkle(e, new Color(1f, 0.6f, 0.25f), 3f, 12f, 0.12f);
        Tamamla(e, 2.2f);
        return e;
    }

    Efekt FirlatmaYap()
    {
        Efekt e = YeniEfekt("FuzeCikisi");
        PSYap(e, new PSAyar
        {
            ad = "Parlama", adet = 3, omurMin = 0.08f, omurMax = 0.15f, hizMin = 0f, hizMax = 8f,
            boyMin = 3f, boyMax = 5f, renkA = new Color(1f, 0.95f, 0.65f, 1f), renkB = new Color(1f, 0.75f, 0.3f, 1f),
            buyume = 1.8f, sekil = ParticleSystemShapeType.Cone, aci = 25f, yaricap = 0.3f
        });
        PSYap(e, new PSAyar
        {
            ad = "Ates", adet = 10, omurMin = 0.2f, omurMax = 0.4f, hizMin = 10f, hizMax = 30f,
            boyMin = 1.5f, boyMax = 3f, renkA = new Color(1f, 0.6f, 0.15f, 1f), renkB = new Color(1f, 0.35f, 0.05f, 1f),
            buyume = 1.8f, sekil = ParticleSystemShapeType.Cone, aci = 20f, yaricap = 0.3f
        });
        PSYap(e, new PSAyar
        {
            ad = "BuyukDuman", adet = 16, omurMin = 2.5f, omurMax = 4f, hizMin = 6f, hizMax = 18f,
            boyMin = 3f, boyMax = 6f, renkA = new Color(0.8f, 0.8f, 0.8f, 0.6f), renkB = new Color(0.95f, 0.95f, 0.95f, 0.45f),
            buyume = 3.5f, yercekimi = -0.02f, sekil = ParticleSystemShapeType.Cone, aci = 35f, yaricap = 0.4f, surtunme = 0.08f
        });
        IsikEkle(e, new Color(1f, 0.7f, 0.3f), 4f, 25f, 0.15f);
        Tamamla(e, 4.5f);
        return e;
    }

    Efekt TozYap()
    {
        Efekt e = YeniEfekt("ToprakToz");
        PSYap(e, new PSAyar
        {
            ad = "Toz", adet = 5, omurMin = 0.8f, omurMax = 1.4f, hizMin = 2f, hizMax = 6f,
            boyMin = 1f, boyMax = 1.8f, renkA = new Color(0.45f, 0.38f, 0.3f, 0.7f), renkB = new Color(0.55f, 0.48f, 0.4f, 0.6f),
            buyume = 2.5f, yercekimi = -0.02f, sekil = ParticleSystemShapeType.Cone, aci = 35f, yaricap = 0.3f
        });
        PSYap(e, new PSAyar
        {
            ad = "Parca", adet = 6, omurMin = 0.3f, omurMax = 0.6f, hizMin = 8f, hizMax = 18f,
            boyMin = 0.1f, boyMax = 0.18f, renkA = new Color(0.35f, 0.28f, 0.2f, 1f), renkB = new Color(0.5f, 0.42f, 0.3f, 1f),
            buyume = 0.8f, yercekimi = 1f, sekil = ParticleSystemShapeType.Cone, aci = 40f, yaricap = 0.1f, cizgi = true
        });
        Tamamla(e, 1.8f);
        return e;
    }

    // ---------------- Yardimcilar ----------------

    Efekt YeniEfekt(string ad)
    {
        GameObject g = new GameObject(ad);
        g.transform.SetParent(transform, false);
        g.SetActive(false);   // kapali olusturuyoruz: parcacik sistemi kurulum sirasinda calismasin
        Efekt e = new Efekt();
        e.obje = g;
        return e;
    }

    void Tamamla(Efekt e, float sure)
    {
        e.sure = sure;
        e.sistemler = e.obje.GetComponentsInChildren<ParticleSystem>(true);
    }

    void IsikEkle(Efekt e, Color renk, float siddet, float menzil, float sure)
    {
        GameObject g = new GameObject("Isik");
        g.transform.SetParent(e.obje.transform, false);
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = renk;
        l.intensity = siddet;
        l.range = menzil;
        l.shadows = LightShadows.None;
        e.isik = l;
        e.isikSiddeti = siddet;
        e.isikMenzili = menzil;
        e.isikSuresi = sure;
    }

    void PSYap(Efekt e, PSAyar a)
    {
        GameObject g = new GameObject(a.ad);
        g.transform.SetParent(e.obje.transform, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(a.omurMin, a.omurMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(a.hizMin, a.hizMax);
        main.startSize = new ParticleSystem.MinMaxCurve(a.boyMin, a.boyMax);
        main.startColor = new ParticleSystem.MinMaxGradient(a.renkA, a.renkB);
        main.gravityModifier = a.yercekimi;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = a.adet + 4;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)a.adet) });

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = a.sekil;
        sh.radius = a.yaricap;
        if (a.sekil == ParticleSystemShapeType.Cone) sh.angle = a.aci;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.7f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(gr);

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, a.buyume));

        if (a.surtunme > 0f)
        {
            var lim = ps.limitVelocityOverLifetime;
            lim.enabled = true;
            lim.limit = 0.5f;
            lim.dampen = a.surtunme;
        }

        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (a.cizgi)
        {
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 2.5f;
        }
    }

    Material MateryalYap()
    {
        Shader s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) s = Shader.Find("Particles/Standard Unlit");
        if (s == null) s = Shader.Find("Standard");

        Material m = new Material(s);
        m.mainTexture = YumusakDaire(64);
        return m;
    }

    Texture2D YumusakDaire(int n)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        float r = n * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        t.Apply();
        return t;
    }
}