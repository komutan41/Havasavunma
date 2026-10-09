using System.Collections.Generic;
using UnityEngine;

// Sahneye eklemen GEREKMEZ, ilk kullanildiginda kendini olusturur ("SesYoneticisi" objesi).
// Sesler: Assets/Resources/Sesler/ klasorune atilan WAV dosyalaridir. Dosya adi = ses adi
// (ornek: "patlama.wav" -> Cal("patlama", ...)).
// Sahnede (genelde Main Camera'da) bir AudioListener olmali.
public class SesYoneticisi : MonoBehaviour
{
    static SesYoneticisi ornek;
    public static SesYoneticisi Ornek
    {
        get
        {
            if (ornek == null)
            {
                GameObject g = new GameObject("SesYoneticisi");
                ornek = g.AddComponent<SesYoneticisi>();
            }
            return ornek;
        }
    }

    [Header("Genel")]
    [Range(0f, 1f)] public float anaSes = 1f;
    public float yakinMesafe = 30f;      // bu mesafeye kadar ses tam seviyede
    public float uzakMesafe = 1500f;     // varsayilan duyulma siniri (m); sonra sessiz
    public float patlamaUzakMesafe = 450f; // patlama dinleyiciden bu kadar m'den uzaksa "patlama_uzak" calar

    AudioSource[] kaynaklar;
    AudioSource kaynak2D;
    int sira;
    Dictionary<string, AudioClip> klipler = new Dictionary<string, AudioClip>();
    Dictionary<string, float> sonCalma = new Dictionary<string, float>();
    HashSet<string> eksikler = new HashSet<string>();

    void Awake()
    {
        ornek = this;

        kaynaklar = new AudioSource[32];
        for (int i = 0; i < kaynaklar.Length; i++)
        {
            GameObject g = new GameObject("Ses3D_" + i);
            g.transform.SetParent(transform, false);
            AudioSource s = g.AddComponent<AudioSource>();
            Ayarla3D(s, uzakMesafe);
            kaynaklar[i] = s;
        }

        GameObject g2 = new GameObject("Ses2D");
        g2.transform.SetParent(transform, false);
        kaynak2D = g2.AddComponent<AudioSource>();
        kaynak2D.playOnAwake = false;
        kaynak2D.spatialBlend = 0f;
    }

    void Ayarla3D(AudioSource s, float menzil)
    {
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;     // oyun icin: mesafeyle duzgun azalir
        s.minDistance = yakinMesafe;
        s.maxDistance = menzil;
        s.dopplerLevel = 0.3f;
    }

    // Resources/Sesler/<ad> icindeki klibi yukler (bir kere)
    AudioClip Klip(string ad)
    {
        AudioClip c;
        if (klipler.TryGetValue(ad, out c)) return c;

        c = Resources.Load<AudioClip>("Sesler/" + ad);
        if (c == null && eksikler.Add(ad))
            Debug.LogWarning("SesYoneticisi: 'Sesler/" + ad + "' bulunamadi. WAV dosyasini Assets/Resources/Sesler klasorune koy.");
        klipler[ad] = c;
        return c;
    }

    // Dunyada bir noktadan (3D) ses cal. minAralik: ayni sesin tekrar calma siniri (saniye)
    public void Cal(string ad, Vector3 poz, float ses = 1f, float perde = 1f, float menzil = 0f, float minAralik = 0f)
    {
        AudioClip c = Klip(ad);
        if (c == null) return;

        if (minAralik > 0f)
        {
            float son;
            if (sonCalma.TryGetValue(ad, out son) && Time.unscaledTime - son < minAralik) return;
            sonCalma[ad] = Time.unscaledTime;
        }

        AudioSource s = BosKaynak();
        s.transform.position = poz;
        s.clip = c;
        s.volume = Mathf.Clamp01(ses * anaSes);
        s.pitch = perde;
        s.maxDistance = menzil > 0f ? menzil : uzakMesafe;
        s.Play();
    }

    // Patlama: dinleyiciye (Main Camera) yakinsa "patlama", uzaksa "patlama_uzak" calar
    public void PatlamaCal(Vector3 poz, float ses = 1f, float perde = 1f, float menzil = 0f)
    {
        float mesafe = 0f;
        Camera cam = Camera.main;
        if (cam != null) mesafe = Vector3.Distance(cam.transform.position, poz);

        string ad = mesafe > patlamaUzakMesafe ? "patlama_uzak" : "patlama";
        Cal(ad, poz, ses, perde, menzil);
    }

    // Ekran sesi (arayuz, radar): mesafeden etkilenmez
    public void Cal2D(string ad, float ses = 1f)
    {
        AudioClip c = Klip(ad);
        if (c == null) return;
        kaynak2D.PlayOneShot(c, Mathf.Clamp01(ses * anaSes));
    }

    // Bir objeye surekli donen (dongu) ses ekler, obje yok olunca ses de biter
    public AudioSource DonguEkle(GameObject hedef, string ad, float ses = 1f, float menzil = 1200f)
    {
        AudioClip c = Klip(ad);
        if (c == null || hedef == null) return null;

        AudioSource s = hedef.AddComponent<AudioSource>();
        Ayarla3D(s, menzil);
        s.clip = c;
        s.loop = true;
        s.volume = Mathf.Clamp01(ses * anaSes);
        s.time = Random.value * c.length;   // birden cok fuze ayni anda ayni fazda calmasin
        s.Play();
        return s;
    }

    AudioSource BosKaynak()
    {
        for (int i = 0; i < kaynaklar.Length; i++)
        {
            int k = (sira + i) % kaynaklar.Length;
            if (!kaynaklar[k].isPlaying)
            {
                sira = (k + 1) % kaynaklar.Length;
                return kaynaklar[k];
            }
        }

        // hepsi mesgul: siradakini kes (en eskisi)
        AudioSource s = kaynaklar[sira];
        sira = (sira + 1) % kaynaklar.Length;
        return s;
    }
}
