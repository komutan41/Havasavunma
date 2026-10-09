using UnityEngine;

// Anten (veya direk) objesine eklenir. Anten TABANINDAN esnek bir sapmis gibi savrulur:
//  - Arac ileri gidince uc geriye, geri gidince one dogru yatar (hiz + ivme)
//  - Virajda disa dogru hafif savrulur
//  - Hizliyken cok hafif titrer
// Objenin pivot (origin) noktasi antenin TABANINDA olmali, yoksa ortasindan egilir.
public class AntenSalinim : MonoBehaviour
{
    public AracHareket arac;                  // bos birakirsan ustteki objelerde otomatik bulunur

    [Header("Egilme eksenleri (antenin KENDI yerel ekseni)")]
    public Vector3 ileriGeriEkseni = new Vector3(1f, 0f, 0f);   // ileri-geri savrulma bu eksen etrafinda
    public Vector3 yanEkseni = new Vector3(0f, 0f, 1f);         // yana savrulma bu eksen etrafinda
    public bool tersCevir = false;                              // yanlis yone savruluyorsa isaretle

    [Header("Etki (derece)")]
    public float hizEtkisi = 2.5f;     // tam hizda sabit egilme
    public float ivmeEtkisi = 5f;      // hizlanirken/frenlerken ek egilme (1 g)
    public float yanEtkisi = 3f;       // virajda
    public float maxAci = 7f;
    public float titreme = 0.25f;      // tam hizda hafif titresim (derece)

    [Header("Yay")]
    public float yay = 30f;            // buyudukce sertlesir
    public float sonum = 4f;           // buyudukce sallanma azalir

    Quaternion baz;
    float aciA, hizA, aciB, hizB;

    void Start()
    {
        baz = transform.localRotation;
        if (arac == null) arac = GetComponentInParent<AracHareket>();
    }

    void LateUpdate()
    {
        if (arac == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        if (dt <= 0f) return;

        float s = tersCevir ? -1f : 1f;
        float hizOrani = arac.Hiz / Mathf.Max(arac.maxIleriHiz, 0.1f);

        float hedefA = Mathf.Clamp(-(hizOrani * hizEtkisi + arac.BoyunaIvme / 9.81f * ivmeEtkisi), -maxAci, maxAci) * s;
        float hedefB = Mathf.Clamp(-arac.YanalIvme / 9.81f * yanEtkisi, -maxAci, maxAci) * s;

        hizA += ((hedefA - aciA) * yay - hizA * sonum) * dt;
        aciA += hizA * dt;
        hizB += ((hedefB - aciB) * yay - hizB * sonum) * dt;
        aciB += hizB * dt;

        float n = (Mathf.PerlinNoise(Time.time * 7f, 0.31f) - 0.5f) * 2f * titreme * Mathf.Abs(hizOrani);

        Vector3 e1 = ileriGeriEkseni.sqrMagnitude > 0.001f ? ileriGeriEkseni.normalized : Vector3.right;
        Vector3 e2 = yanEkseni.sqrMagnitude > 0.001f ? yanEkseni.normalized : Vector3.forward;
        transform.localRotation = baz * Quaternion.AngleAxis(aciA + n, e1) * Quaternion.AngleAxis(aciB, e2);
    }
}
