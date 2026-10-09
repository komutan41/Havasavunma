using UnityEngine;

// HedefUcus.Start() ucaga otomatik ekler (elle eklemene gerek yok).
// Iki katmanli ucak motoru sesi. Unity'nin mesafe sonumlemesi + doppler'inin ustune,
// dinleyiciye (Main Camera) gore iki katman birbirine karisir:
//   - yakin: parlak, gurultulu  (Resources/Sesler/ucak_motor_yakin.wav)
//   - uzak : kisik, alcak frekansli (Resources/Sesler/ucak_motor_uzak.wav)
// Ucak vurulunca (olu) ses kesilir, yeniden dogunca geri gelir.
public class UcakSesi : MonoBehaviour
{
    [Header("Sesler (Resources/Sesler)")]
    public string yakinSes = "ucak_motor_yakin";
    public string uzakSes = "ucak_motor_uzak";

    [Header("Mesafe (m)")]
    public float tamSesMesafesi = 120f;   // bu mesafeye kadar ses azalmaz
    public float menzil = 2500f;          // bundan uzakta ses tamamen kesilir
    public float yakinBitis = 200f;       // bundan yakinda sadece "yakin" katman duyulur
    public float uzakBaslangic = 1100f;   // bundan uzakta sadece "uzak" katman duyulur

    [Header("Seviye")]
    [Range(0f, 1f)] public float yakinSeviye = 0.9f;
    [Range(0f, 1f)] public float uzakSeviye = 0.8f;
    public float doppler = 0.5f;          // 0 = yok, 1 = gercek
    public float tepkiSuresi = 0.25f;     // seviye gecisi (sn)

    HedefUcus hedef;
    AudioSource yakin;
    AudioSource uzak;
    Transform dinleyici;

    void Start()
    {
        hedef = GetComponent<HedefUcus>();

        yakin = SesYoneticisi.Ornek.DonguEkle(gameObject, yakinSes, 0f, menzil);
        uzak = SesYoneticisi.Ornek.DonguEkle(gameObject, uzakSes, 0f, menzil);

        float perde = Random.Range(0.97f, 1.03f);   // birden cok ucak ayni perdede olmasin
        Ayarla(yakin, perde);
        Ayarla(uzak, perde);
    }

    void Ayarla(AudioSource s, float perde)
    {
        if (s == null) return;
        s.rolloffMode = AudioRolloffMode.Logarithmic;   // mesafe iki katina cikinca yaklasik -6 dB
        s.minDistance = tamSesMesafesi;
        s.maxDistance = menzil;
        s.dopplerLevel = doppler;
        s.pitch = perde;
    }

    void Update()
    {
        if (yakin == null && uzak == null) return;

        if (dinleyici == null)
        {
            Camera c = Camera.main;
            if (c != null) dinleyici = c.transform;
        }

        float d = dinleyici != null ? Vector3.Distance(dinleyici.position, transform.position) : 0f;

        float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(yakinBitis, uzakBaslangic, d));          // 0 = yakin, 1 = uzak
        float sinir = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(menzil * 0.7f, menzil, d));      // menzil sonunda sessiz
        float ana = SesYoneticisi.Ornek.anaSes;

        float hy = (1f - k) * yakinSeviye * sinir * ana;
        float hu = Mathf.Lerp(0.35f, 1f, k) * uzakSeviye * sinir * ana;

        if (hedef != null && hedef.olu) { hy = 0f; hu = 0f; }

        float a = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(tepkiSuresi, 0.01f));
        if (yakin != null) yakin.volume = Mathf.Lerp(yakin.volume, hy, a);
        if (uzak != null) uzak.volume = Mathf.Lerp(uzak.volume, hu, a);
    }
}
