using UnityEngine;

// Aracin ana (kok) objesine eklenir. W/S veya yukari/asagi ok: ileri-geri,
// A/D veya sag/sol ok: direksiyon. Arac durmadan donmez; geri giderken direksiyon tersine doner.
public class AracHareket : MonoBehaviour
{
    public float maxIleriHiz = 14f;      // m/sn (yaklasik 50 km/s)
    public float maxGeriHiz = 6f;
    public float ivme = 6f;
    public float frenIvmesi = 10f;       // tusu birakinca yavaslama
    public float donusHizi = 40f;        // derece/sn (tam hizda)

    public float Hiz { get; private set; }

    void Update()
    {
        float dt = Time.deltaTime;
        float gaz = Input.GetAxisRaw("Vertical");
        float dir = Input.GetAxisRaw("Horizontal");

        float hedef = gaz > 0f ? gaz * maxIleriHiz : gaz * maxGeriHiz;
        float oran = Mathf.Abs(gaz) > 0.01f ? ivme : frenIvmesi;
        Hiz = Mathf.MoveTowards(Hiz, hedef, oran * dt);

        float hizOrani = Mathf.Clamp(Hiz / maxIleriHiz, -1f, 1f);
        transform.Rotate(0f, dir * donusHizi * hizOrani * dt, 0f, Space.World);
        transform.position += transform.forward * Hiz * dt;
    }
}
