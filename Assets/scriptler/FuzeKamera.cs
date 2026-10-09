using System.Collections.Generic;
using UnityEngine;

public class FuzeKamera : MonoBehaviour
{
    [Header("Baglantilar")]
    public KameraKontrol kamera;
    public Transform losKaynagi;         // taret_govdesi
    public Transform hedef;              // test ucagi (zoom icin)

    [Header("Baglanti kalitesi")]
    public float sinyalMenzili = 1200f;
    public float maxTitremeDerece = 1f;
    public float titremeHizi = 3f;
    public float artiGorselTitreme = 0.15f;

    [Header("Zoom")]
    public float zoomMenzili = 800f;
    public float zoomOrani = 0.88f;

    class Aktif
    {
        public GameObject obje;
        public FuzeHareket hareket;
        public float tohum;
    }

    List<Aktif> fuzeler = new List<Aktif>();
    Vector2 artiTitreme;
    float enKotuKayip;
    float ilkMenzil;
    float ilkSinyal;
    int kopukSayisi;
    float kopukSure;
    bool alarmCaldi;
    bool modAcik;
    bool nisanOncesi;

    public bool FuzeAktifMi { get { return fuzeler.Count > 0; } }
    public Vector2 ArtiTitreme { get { return artiTitreme; } }
    public float GorselTitremeYaricapi { get { return Screen.height * 0.08f * artiGorselTitreme; } }

    public void FuzeyeGec(GameObject f)
    {
        Aktif a = new Aktif();
        a.obje = f;
        a.hareket = f.GetComponent<FuzeHareket>();
        a.tohum = Random.value * 100f;
        fuzeler.Add(a);

        if (!modAcik)
        {
            modAcik = true;
            nisanOncesi = kamera.nisanModu;
            kamera.nisanModu = true;
        }
    }

    void Update()
    {
        if (!modAcik) return;

        fuzeler.RemoveAll(x => x.obje == null);

        if (fuzeler.Count == 0)
        {
            modAcik = false;
            kamera.nisanModu = nisanOncesi;
            kamera.gorusCarpani = 1f;
            artiTitreme = Vector2.zero;
            kopukSure = 0f;
            alarmCaldi = false;
            enKotuKayip = 0f;
            kopukSayisi = 0;
            return;
        }

        Vector3 kaynak = losKaynagi != null ? losKaynagi.position : transform.position;
        Quaternion bakis = Quaternion.Euler(-kamera.Pitch, kamera.Yaw, 0f);

        enKotuKayip = 0f;
        kopukSayisi = 0;
        float enYakinHedef = float.MaxValue;

        for (int i = 0; i < fuzeler.Count; i++)
        {
            Aktif a = fuzeler[i];
            float menzil = Vector3.Distance(a.obje.transform.position, kaynak);
            float kayip = Mathf.Clamp01((menzil - sinyalMenzili) / sinyalMenzili);
            enKotuKayip = Mathf.Max(enKotuKayip, kayip);

            if (i == 0)
            {
                ilkMenzil = menzil;
                ilkSinyal = (1f - kayip) * 100f;
            }

            float t = Time.time * titremeHizi + a.tohum;
            float nx = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(0.91f, t) - 0.5f) * 2f;
            Vector2 tit = new Vector2(nx, ny) * kayip;
            Quaternion titDonus = Quaternion.Euler(-tit.y * maxTitremeDerece, tit.x * maxTitremeDerece, 0f);

            if (a.hareket != null)
            {
                a.hareket.kumandaVar = true;
                a.hareket.losKaynak = kaynak;
                a.hareket.losYon = (bakis * titDonus) * Vector3.forward;
                if (!a.hareket.BaglantiVar) kopukSayisi++;
            }

            if (hedef != null)
            {
                float d = Vector3.Distance(a.obje.transform.position, hedef.position);
                enYakinHedef = Mathf.Min(enYakinHedef, d);
            }
        }

        // Baglanti kopma alarmi (firlatma aninda bir karelik yanlis alarm olmasin diye 0.35 sn beklenir)
        if (kopukSayisi > 0)
        {
            kopukSure += Time.deltaTime;
            if (kopukSure > 0.35f && !alarmCaldi)
            {
                alarmCaldi = true;
                SesYoneticisi.Ornek.Cal2D("baglanti_kopt", 0.45f);
            }
        }
        else
        {
            kopukSure = 0f;
            alarmCaldi = false;
        }

        float tt = Time.time * titremeHizi;
        artiTitreme = new Vector2(
            (Mathf.PerlinNoise(tt, 0.37f) - 0.5f) * 2f,
            (Mathf.PerlinNoise(0.91f, tt) - 0.5f) * 2f) * enKotuKayip;

        float zoom = 1f;
        if (hedef != null && enYakinHedef < float.MaxValue)
        {
            float yakin = 1f - Mathf.Clamp01(enYakinHedef / zoomMenzili);
            zoom = Mathf.Lerp(1f, zoomOrani, yakin);
        }
        kamera.gorusCarpani = zoom;
    }

    void OnGUI()
    {
        if (kamera == null || !kamera.nisanModu) return;

        // Artı artık Nisangah.cs tarafından çiziliyor

        if (modAcik && fuzeler.Count > 0)
        {
            GUIStyle k = new GUIStyle(GUI.skin.label);
            k.fontSize = Mathf.RoundToInt(Screen.height * 0.03f);
            k.normal.textColor = Color.white;
            GUI.Label(new Rect(20f, 20f, 700f, 40f),
                "Sinyal: %" + Mathf.RoundToInt(ilkSinyal) + "   Menzil: " + Mathf.RoundToInt(ilkMenzil) + " m   Fuze: " + fuzeler.Count, k);

            if (kopukSayisi > 0)
            {
                GUIStyle b = new GUIStyle(GUI.skin.label);
                b.alignment = TextAnchor.UpperCenter;
                b.fontSize = Mathf.RoundToInt(Screen.height * 0.05f);
                b.normal.textColor = Color.red;
                GUI.Label(new Rect(0f, Screen.height * 0.12f, Screen.width, 80f), "BAGLANTI KOPTU (" + kopukSayisi + ")", b);
            }
        }
    }
}