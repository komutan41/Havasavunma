using System.Collections.Generic;
using UnityEngine;

public class MakineliTufek : MonoBehaviour
{
    [Header("Namlular (sirayla atar)")]
    public Transform[] namlular;            // NamluCikis_L, NamluCikis_R (mavi ok = mermi yonu)

    [Header("Atis")]
    public KeyCode atesTusu = KeyCode.F;
    public float atesHizi = 25f;            // saniyede mermi (iki namlu toplam)
    public float namluHizi = 900f;          // m/sn
    public float sacilma = 0.15f;           // derece
    public float mermiOmru = 3.5f;
    public float yerYuksekligi = 0f;

    [Header("Fizik")]
    public float surtunme = 0.0003f;        // buyudukce mermi daha cok yavaslar
    public float yercekimiCarpani = 1f;

    [Header("Hasar")]
    public float isabetYaricapi = 8f;       // ucagin etrafindaki isabet bolgesi (m)
    public int vurusGerekli = 8;            // kac mermi isabeti ucagi dusurur

    [Header("Mermi gorseli")]
    public Color mermiRengi = new Color(1f, 0.85f, 0.3f);
    public float mermiKalinligi = 0.12f;
    public float mermiBoyu = 4f;
    public float uzaktaBuyume = 0.01f;      // uzaklastikca gorunur kalsin

    [Header("Nisan gostergesi (radar renginde)")]
    public float gostergeMenzili = 1200f;
    public Color gostergeRengi = new Color(0.55f, 1f, 0.55f, 0.95f);
    public float halkaYaricapi = 14f;

    class Mermi
    {
        public Transform t;
        public Vector3 poz, hiz;
        public float yas;
        public bool aktif;
    }

    Mermi[] havuz;
    int sonNamlu;
    float sonrakiAtis;
    Dictionary<HedefUcus, int> isabetler = new Dictionary<HedefUcus, int>();
    Camera kam;
    Texture2D halka;

    void Start()
    {
        kam = Camera.main;
        halka = HalkaYap();

        GameObject temel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material mat = new Material(temel.GetComponent<Renderer>().sharedMaterial);
        mat.color = mermiRengi;
        Destroy(temel);

        havuz = new Mermi[250];
        for (int i = 0; i < havuz.Length; i++)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "Mermi";
            Collider c = g.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            g.SetActive(false);
            havuz[i] = new Mermi { t = g.transform };
        }
    }

    void Update()
    {
        if (kam == null) kam = Camera.main;
        float dt = Time.deltaTime;

        if (Input.GetKey(atesTusu) && Time.time >= sonrakiAtis)
        {
            Ates();
            sonrakiAtis = Mathf.Max(sonrakiAtis + 1f / atesHizi, Time.time - 0.05f);
        }

        MermileriGuncelle(dt);
    }

    Vector3 NamluNoktasi()
    {
        return (namlular != null && namlular.Length > 0 && namlular[0] != null)
            ? namlular[0].position : transform.position;
    }

    void Ates()
    {
        Transform nam = (namlular != null && namlular.Length > 0)
            ? namlular[sonNamlu % namlular.Length] : transform;
        sonNamlu++;
        if (nam == null) return;

        Mermi m = null;
        for (int i = 0; i < havuz.Length; i++)
            if (!havuz[i].aktif) { m = havuz[i]; break; }
        if (m == null) return;

        Vector3 yon = nam.forward + Random.insideUnitSphere * Mathf.Tan(sacilma * Mathf.Deg2Rad);
        yon.Normalize();

        m.poz = nam.position;
        m.hiz = yon * namluHizi;
        m.yas = 0f;
        m.aktif = true;
        m.t.position = m.poz;
        m.t.rotation = Quaternion.LookRotation(yon);
        m.t.gameObject.SetActive(true);
    }

    void MermileriGuncelle(float dt)
    {
        Vector3 g = Physics.gravity * yercekimiCarpani;

        for (int i = 0; i < havuz.Length; i++)
        {
            Mermi m = havuz[i];
            if (!m.aktif) continue;

            Vector3 eski = m.poz;
            float sp = m.hiz.magnitude;
            m.hiz += (g - m.hiz * sp * surtunme) * dt;   // yercekimi + hizin karesiyle orantili surtunme
            m.poz += m.hiz * dt;
            m.yas += dt;

            if (m.yas > mermiOmru || m.poz.y < yerYuksekligi || IsabetKontrol(eski, m.poz))
            {
                m.aktif = false;
                m.t.gameObject.SetActive(false);
                continue;
            }

            m.t.position = m.poz;
            if (m.hiz.sqrMagnitude > 0.01f) m.t.rotation = Quaternion.LookRotation(m.hiz);

            float d = kam != null ? Vector3.Distance(m.poz, kam.transform.position) : 0f;
            float k = 1f + d * uzaktaBuyume;
            m.t.localScale = new Vector3(mermiKalinligi * k, mermiKalinligi * k, mermiBoyu * (1f + d * uzaktaBuyume * 0.5f));
        }
    }

    bool IsabetKontrol(Vector3 a, Vector3 b)
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
                if (n >= vurusGerekli)
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

    // Merminin surtunme ve yercekimi payi hesaba katilarak, hedefin gelecekteki konumuna nisan noktasi
    Vector3 Kestirim(Vector3 namlu, HedefUcus h)
    {
        Vector3 P = h.transform.position;
        Vector3 V = h.yon * h.hiz;
        float t = Vector3.Distance(namlu, P) / namluHizi;

        for (int i = 0; i < 5; i++)
        {
            float d = Vector3.Distance(namlu, P + V * t);
            if (surtunme < 0.000001f) t = d / namluHizi;
            else t = (Mathf.Exp(surtunme * d) - 1f) / (surtunme * namluHizi);   // surtunmeli ucus suresi
        }

        Vector3 nokta = P + V * t;
        nokta -= Physics.gravity * yercekimiCarpani * 0.5f * t * t;               // dusme payi: biraz yukari nisan
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

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (kam == null) return;

        Vector3 nm = NamluNoktasi();
        GUI.color = gostergeRengi;

        for (int i = 0; i < HedefUcus.tumu.Count; i++)
        {
            HedefUcus h = HedefUcus.tumu[i];
            if (h == null || h.sahte || h.olu) continue;
            if (Vector3.Distance(nm, h.transform.position) > gostergeMenzili) continue;

            Vector3 s1 = kam.WorldToScreenPoint(h.transform.position);
            Vector3 s2 = kam.WorldToScreenPoint(Kestirim(nm, h));
            if (s1.z <= 0f || s2.z <= 0f) continue;

            Vector2 a = new Vector2(s1.x, Screen.height - s1.y);   // hedef (radar karesinin merkezi)
            Vector2 c = new Vector2(s2.x, Screen.height - s2.y);   // nisan alinacak nokta

            Vector2 fark = c - a;
            float boy = fark.magnitude;
            if (boy > halkaYaricapi)
                Cizgi(a, c - fark / boy * halkaYaricapi, 2f);      // hedeften cikan cubuk

            GUI.DrawTexture(new Rect(c.x - halkaYaricapi, c.y - halkaYaricapi, halkaYaricapi * 2f, halkaYaricapi * 2f), halka);
        }

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
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - (r - 4f)) / 2.5f);   // ince, ici bos halka
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        t.Apply();
        return t;
    }
}
