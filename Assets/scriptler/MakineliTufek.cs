using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MakineliTufek : MonoBehaviour
{
    [Header("Namlular (sirayla atar)")]
    public Transform[] namlular;            // NamluCikis_L, NamluCikis_R

    [Header("Atis")]
    public Key atesTusu = Key.F;
    public bool girisAktif = true;          // AracYoneticisi arac degisince kapatir
    public float atesHizi = 25f;            // saniyede mermi (iki namlu toplam)
    public float namluHizi = 900f;          // m/sn
    public float sacilma = 0.15f;           // derece
    public float mermiOmru = 2.5f;          // saniye (900 m/sn ile yaklasik 1700 m menzil)
    public float yerYuksekligi = 0f;

    [Header("Fizik")]
    public float surtunme = 0.0003f;
    public float yercekimiCarpani = 1f;

    [Header("Hasar")]
    public float isabetYaricapi = 8f;
    public int vurusGerekli = 8;

    [Header("Mermi modeli (Blender prefab)")]
    public GameObject mermiPrefab;          // BOS BIRAKIRSAN basit bir cubuk kullanilir
    public Vector3 modelDonusu = Vector3.zero;   // model yanlis yone bakiyorsa (ornek: 90, 0, 0)
    public float mermiOlcegi = 1f;
    public float uzaktaBuyume = 0.01f;      // uzaklastikca buyur (0 = kapali)
    public float maksOlcek = 12f;
    public int gorunenAraligi = 1;          // 1 = her mermi gorunur, 2 = her ikinci mermi gorunur

    [Header("Efektler")]
    public bool namluEfekti = true;
    public bool sesEfekti = true;
    public int dumanHerKacAtista = 3;       // 0 = namlu dumani yok
    public bool isabetEfekti = true;
    public bool toprakEfekti = true;

    [Header("Nisan gostergesi (SADECE radar kilidinde)")]
    public RadarEkrani radar;               // bos birakirsan otomatik bulunur
    public float gostergeMenzili = 1200f;
    public Color gostergeRengi = new Color(0.55f, 1f, 0.55f, 0.95f);
    public float halkaYaricapi = 14f;

    class Gorsel
    {
        public GameObject kok;
        public TrailRenderer[] izler;
    }

    class Mermi
    {
        public Vector3 baslangic, poz, hiz;
        public float yas;
        public bool aktif;
        public Gorsel gorsel;
    }

    List<Mermi> mermiler = new List<Mermi>();
    Stack<Gorsel> bosGorseller = new Stack<Gorsel>();
    Transform kap;
    Material yedekMat;

    int sonNamlu;
    int atisSayaci;
    float sonrakiAtis;
    float sonToz;
    Dictionary<HedefUcus, int> isabetler = new Dictionary<HedefUcus, int>();
    Camera kam;
    Texture2D halka;
    Efektler ef;

    void Start()
    {
        kam = Camera.main;
        halka = HalkaYap();
        ef = Efektler.Ornek;
        if (radar == null) radar = FindFirstObjectByType<RadarEkrani>();
        kap = new GameObject("Mermiler").transform;
    }

    void Update()
    {
        if (kam == null) kam = Camera.main;
        float dt = Time.deltaTime;

        var k = Keyboard.current;
        if (girisAktif && k != null && k[atesTusu].isPressed && Time.time >= sonrakiAtis)
        {
            Ates();
            sonrakiAtis = Mathf.Max(sonrakiAtis + 1f / atesHizi, Time.time - 0.05f);
        }

        MermileriGuncelle(dt);
    }

    // Radarda kilitli GERCEK ucak (chaff ya da kilitsiz ise null)
    HedefUcus KilitliHedef()
    {
        if (radar == null) return null;
        HedefUcus h = radar.Secili;
        if (h == null || h.sahte || h.olu) return null;
        return h;
    }

    Vector3 NamluNoktasi()
    {
        return (namlular != null && namlular.Length > 0 && namlular[0] != null)
            ? namlular[0].position : transform.position;
    }


    void Ates()
    {
        int idx = (namlular != null && namlular.Length > 0) ? sonNamlu % namlular.Length : -1;
        Transform nam = idx >= 0 ? namlular[idx] : transform;
        sonNamlu++;
        if (nam == null) return;

        Mermi m = MermiAl();
        if (m == null) return;

        Vector3 yon = nam.forward;   // mermi namlunun GERCEKTE baktigi yone gider (kameraya degil)
        yon += Random.insideUnitSphere * Mathf.Tan(sacilma * Mathf.Deg2Rad);
        yon.Normalize();

        atisSayaci++;
        if (sesEfekti)
            SesYoneticisi.Ornek.Cal("makineli_" + Random.Range(1, 4), nam.position, 0.8f, Random.Range(0.94f, 1.06f), 1200f);
        m.baslangic = nam.position;
        m.poz = nam.position;
        m.hiz = yon * namluHizi;
        m.yas = 0f;
        m.aktif = true;
        m.gorsel = null;

        bool gorunur = gorunenAraligi <= 1 || atisSayaci % gorunenAraligi == 0;
        if (gorunur)
        {
            Gorsel g = GorselAl();
            g.kok.transform.SetPositionAndRotation(m.poz, Quaternion.LookRotation(yon));
            g.kok.transform.localScale = Vector3.one * mermiOlcegi;
            g.kok.SetActive(true);
            for (int i = 0; i < g.izler.Length; i++) g.izler[i].Clear();
            m.gorsel = g;
        }

        if (namluEfekti && ef != null)
        {
            ef.NamluAlevi(nam.position, yon);
            if (dumanHerKacAtista > 0 && atisSayaci % dumanHerKacAtista == 0)
                ef.NamluDumani(nam.position, yon);
        }
    }

    Mermi MermiAl()
    {
        for (int i = 0; i < mermiler.Count; i++)
            if (!mermiler[i].aktif) return mermiler[i];

        if (mermiler.Count >= 400) return null;
        Mermi m = new Mermi();
        mermiler.Add(m);
        return m;
    }

    void MermiBitir(Mermi m)
    {
        m.aktif = false;
        if (m.gorsel != null)
        {
            m.gorsel.kok.SetActive(false);
            bosGorseller.Push(m.gorsel);
            m.gorsel = null;
        }
    }

    Gorsel GorselAl()
    {
        if (bosGorseller.Count > 0) return bosGorseller.Pop();
        return GorselYarat();
    }

    Gorsel GorselYarat()
    {
        GameObject kok = new GameObject("Mermi");
        kok.transform.SetParent(kap, false);

        if (mermiPrefab != null)
        {
            GameObject model = Instantiate(mermiPrefab, kok.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(modelDonusu);

            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Rigidbody rb in model.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
        else
        {
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider c = model.GetComponent<Collider>();
            if (c != null) Destroy(c);
            model.transform.SetParent(kok.transform, false);
            model.transform.localScale = new Vector3(0.12f, 0.12f, 3f);
            Renderer r = model.GetComponent<Renderer>();
            r.sharedMaterial = YedekMateryal();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        kok.SetActive(false);

        Gorsel g = new Gorsel();
        g.kok = kok;
        g.izler = kok.GetComponentsInChildren<TrailRenderer>(true);
        return g;
    }

    Material YedekMateryal()
    {
        if (yedekMat != null) return yedekMat;
        GameObject temel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        yedekMat = new Material(temel.GetComponent<Renderer>().sharedMaterial);
        yedekMat.color = new Color(1f, 0.85f, 0.3f);
        Destroy(temel);
        return yedekMat;
    }

    void MermileriGuncelle(float dt)
    {
        Vector3 g = Physics.gravity * yercekimiCarpani;
        Vector3 kamPoz = kam != null ? kam.transform.position : Vector3.zero;

        for (int i = 0; i < mermiler.Count; i++)
        {
            Mermi m = mermiler[i];
            if (!m.aktif) continue;

            Vector3 eski = m.poz;
            float sp = m.hiz.magnitude;
            m.hiz += (g - m.hiz * sp * surtunme) * dt;
            m.poz += m.hiz * dt;
            m.yas += dt;

            if (m.yas > mermiOmru) { MermiBitir(m); continue; }

            if (m.poz.y < yerYuksekligi)
            {
                if (toprakEfekti && ef != null && Time.time >= sonToz)
                {
                    float dy = eski.y - m.poz.y;
                    float t = dy > 0.0001f ? Mathf.Clamp01((eski.y - yerYuksekligi) / dy) : 1f;
                    Vector3 nokta = Vector3.Lerp(eski, m.poz, t);
                    if ((nokta - kamPoz).sqrMagnitude < 1500f * 1500f)
                    {
                        ef.ToprakToz(nokta);
                        sonToz = Time.time + 0.1f;
                    }
                }
                MermiBitir(m);
                continue;
            }

            if (IsabetKontrol(eski, m.poz, m.hiz)) { MermiBitir(m); continue; }

            if (m.gorsel != null)
            {
                Transform tr = m.gorsel.kok.transform;
                tr.position = m.poz;
                if (m.hiz.sqrMagnitude > 0.01f) tr.rotation = Quaternion.LookRotation(m.hiz);

                float d = kam != null ? Vector3.Distance(m.poz, kamPoz) : 0f;
                float k = Mathf.Clamp(1f + d * uzaktaBuyume, 1f, maksOlcek);
                tr.localScale = Vector3.one * (mermiOlcegi * k);
            }
        }
    }

    bool IsabetKontrol(Vector3 a, Vector3 b, Vector3 hiz)
    {
        Vector3 ab = b - a;
        float ab2 = Mathf.Max(ab.sqrMagnitude, 0.0001f);

        for (int i = HedefUcus.tumu.Count - 1; i >= 0; i--)
        {
            HedefUcus h = HedefUcus.tumu[i];
            if (h == null || h.sahte || h.olu) continue;

            Vector3 p = h.transform.position;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab2);
            Vector3 yakin = a + ab * t;

            if ((yakin - p).sqrMagnitude <= isabetYaricapi * isabetYaricapi)
            {
                int n;
                isabetler.TryGetValue(h, out n);
                n++;
                bool oldu = n >= vurusGerekli;

                if (isabetEfekti && ef != null)
                    ef.Isabet(Vector3.Lerp(yakin, p, 0.5f), -hiz.normalized, oldu);
                if (sesEfekti)
                {
                    if (oldu) SesYoneticisi.Ornek.PatlamaCal(p, 0.9f, Random.Range(0.94f, 1.06f), 1500f);
                    else SesYoneticisi.Ornek.Cal("isabet_" + Random.Range(1, 3), p, 0.7f, Random.Range(0.94f, 1.06f), 1500f, 0.05f);
                }

                if (oldu)
                {
                    isabetler.Remove(h);
                    h.Vurul();
                }
                else isabetler[h] = n;
                return true;
            }
        }
        return false;
    }

    // Surtunme ve yercekimi hesaba katilarak hedefin gelecekteki konumuna nisan noktasi
    Vector3 Kestirim(Vector3 namlu, HedefUcus h)
    {
        Vector3 P = h.transform.position;
        Vector3 V = h.yon * h.hiz;
        float t = Vector3.Distance(namlu, P) / namluHizi;

        for (int i = 0; i < 5; i++)
        {
            float d = Vector3.Distance(namlu, P + V * t);
            if (surtunme < 0.000001f) t = d / namluHizi;
            else t = (Mathf.Exp(surtunme * d) - 1f) / (surtunme * namluHizi);
        }

        Vector3 nokta = P + V * t;
        nokta -= Physics.gravity * yercekimiCarpani * 0.5f * t * t;
        return nokta;
    }

    void Cizgi(Vector2 a, Vector2 b, float kalinlik)
    {
        Vector2 f = b - a;
        float boy = f.magnitude;
        if (boy < 1f) return;

        Matrix4x4 eski = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg, a);
        GUI.DrawTexture(new Rect(a.x, a.y - kalinlik * 0.5f, boy, kalinlik), Texture2D.whiteTexture);
        GUI.matrix = eski;
    }

    // Sadece radarda kilitli gercek ucak icin: hedeften cikan cubuk + ucundaki halka
    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (!girisAktif) return;
        if (kam == null) return;

        HedefUcus h = KilitliHedef();
        if (h == null) return;

        Vector3 nm = NamluNoktasi();
        if (Vector3.Distance(nm, h.transform.position) > gostergeMenzili) return;

        Vector3 s1 = kam.WorldToScreenPoint(h.transform.position);
        Vector3 s2 = kam.WorldToScreenPoint(Kestirim(nm, h));
        if (s1.z <= 0f || s2.z <= 0f) return;

        GUI.color = gostergeRengi;

        Vector2 a = new Vector2(s1.x, Screen.height - s1.y);
        Vector2 c = new Vector2(s2.x, Screen.height - s2.y);

        Vector2 fark = c - a;
        float boy = fark.magnitude;
        if (boy > halkaYaricapi)
            Cizgi(a, c - fark / boy * halkaYaricapi, 2f);

        GUI.DrawTexture(new Rect(c.x - halkaYaricapi, c.y - halkaYaricapi, halkaYaricapi * 2f, halkaYaricapi * 2f), halka);

        GUI.color = Color.white;
    }

    Texture2D HalkaYap()
    {
        int n = 64;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;

        float r = n * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - (r - 4f)) / 2.5f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        t.Apply();
        return t;
    }
}