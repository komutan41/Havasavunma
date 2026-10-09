using UnityEngine;
using UnityEngine.InputSystem;

public class KameraKontrol : MonoBehaviour
{
    [Header("Takip")]
    public Transform hedef;
    public Vector3 hedefOfseti = new Vector3(0f, 0f, 0f);
    public float mesafe = 14f;
    public float yukseklik = 1.5f;

    [Header("Nisan ekrani")]
    public bool nisanModu = false;
    public Vector3 nisanOfseti = new Vector3(0f, 4.5f, 0f);
    public float normalGorus = 60f;
    public float nisanGorus = 25f;
    public float nisanHassasiyet = 0.06f;

    [Header("Durbun zoomu")]
    public float minNisanGorus = 8f;
    public float maxNisanGorus = 25f;
    public float tekerZoomAdimi = 2f;
    public float sagTikZoomCarpani = 0.65f;
    float nisanGorusAnlik = 25f;
    bool sagTikZoomAcik;

    [Header("Kontrol")]
    public float hassasiyet = 0.15f;
    public float minPitch = -30f;
    public float maxPitch = 80f;

    [Header("Radar kilidi")]
    public float kilitYumusaklik = 0.35f;   // kucuk = hizli takip, buyuk = daha yumusak
    public float chaffYumusaklik = 2.5f;    // chaff'e kilitliyken takip cok yavas, arti yavasca kayar
    public float kilitMaxHiz = 60f;         // saniyede derece
    public float elDuzeltmeMax = 4f;
    public float elDuzeltmeCarpani = 0.3f;

    [HideInInspector] public float gorusCarpani = 1f;

    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public bool Kilitli { get { return kilitHedef != null; } }

    Camera cam;
    Transform kilitHedef;
    bool sahteKilit;
    float ofsetYaw;
    float ofsetPitch;
    float yawHizi;
    float pitchHizi;

    void Awake()
    {
        cam = GetComponent<Camera>();
        Vector3 e = transform.eulerAngles;
        Yaw = e.y;
        float x = e.x > 180f ? e.x - 360f : e.x;
        Pitch = -x;
        nisanGorusAnlik = Mathf.Clamp(nisanGorus, minNisanGorus, maxNisanGorus);
    }

    public void KilitAyarla(Transform t)
    {
        HedefUcus hu = t != null ? t.GetComponent<HedefUcus>() : null;
        bool yeniSahte = hu != null && hu.sahte;
        bool gecis = yeniSahte && kilitHedef != null;   // kilitliyken chaff'e kayma: akici gec

        kilitHedef = t;
        sahteKilit = yeniSahte;

        if (!gecis)
        {
            ofsetYaw = 0f;
            ofsetPitch = 0f;
            yawHizi = 0f;
            pitchHizi = 0f;
        }
    }

    void LateUpdate()
    {
        var k = Keyboard.current;
        if (k != null && k.cKey.wasPressedThisFrame) nisanModu = !nisanModu;

        var mouse = Mouse.current;
        if (mouse != null && nisanModu)
        {
            // Sag tikla hizli yakinlastir/normal durbune don.
            if (mouse.rightButton.wasPressedThisFrame)
            {
                sagTikZoomAcik = !sagTikZoomAcik;
                nisanGorusAnlik = sagTikZoomAcik
                    ? Mathf.Max(minNisanGorus, nisanGorus * sagTikZoomCarpani)
                    : Mathf.Clamp(nisanGorus, minNisanGorus, maxNisanGorus);
            }

            // Tekerlekle kademeli yakinlastir/uzaklastir.
            float teker = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(teker) > 0.01f)
            {
                nisanGorusAnlik = Mathf.Clamp(nisanGorusAnlik - Mathf.Sign(teker) * tekerZoomAdimi,
                    minNisanGorus, maxNisanGorus);
                sagTikZoomAcik = false;
            }
        }
        if (!nisanModu)
        {
            sagTikZoomAcik = false;
            nisanGorusAnlik = Mathf.Clamp(nisanGorus, minNisanGorus, maxNisanGorus);
        }

        var p = Pointer.current;
        if (p != null && p.press.isPressed)
        {
            Vector2 d = p.delta.ReadValue();
            float h = nisanModu ? nisanHassasiyet : hassasiyet;

            if (kilitHedef != null)
            {
                float c = elDuzeltmeCarpani;
                ofsetYaw = Mathf.Clamp(ofsetYaw + d.x * h * c, -elDuzeltmeMax, elDuzeltmeMax);
                ofsetPitch = Mathf.Clamp(ofsetPitch + d.y * h * c, -elDuzeltmeMax, elDuzeltmeMax);
            }
            else
            {
                Yaw += d.x * h;
                Pitch += d.y * h;
                Pitch = Mathf.Clamp(Pitch, minPitch, maxPitch);
            }
        }

        if (hedef == null) return;

        if (kilitHedef != null)
        {
            Vector3 kaynak = hedef.position + nisanOfseti;
            Vector3 fark = kilitHedef.position - kaynak;
            float hedefYaw = Mathf.Atan2(fark.x, fark.z) * Mathf.Rad2Deg + ofsetYaw;
            float yatay = new Vector2(fark.x, fark.z).magnitude;
            float hedefPitch = Mathf.Atan2(fark.y, yatay) * Mathf.Rad2Deg + ofsetPitch;
            hedefPitch = Mathf.Clamp(hedefPitch, minPitch, maxPitch);

            float yum = sahteKilit ? chaffYumusaklik : kilitYumusaklik;
            Yaw = Mathf.SmoothDampAngle(Yaw, hedefYaw, ref yawHizi, yum, kilitMaxHiz);
            Pitch = Mathf.SmoothDamp(Pitch, hedefPitch, ref pitchHizi, yum, kilitMaxHiz);
        }

        Quaternion bakis = Quaternion.Euler(-Pitch, Yaw, 0f);

        if (nisanModu)
        {
            transform.position = hedef.position + nisanOfseti;
            transform.rotation = bakis;
            if (cam != null) cam.fieldOfView = Mathf.Clamp(nisanGorusAnlik * gorusCarpani, minNisanGorus, maxNisanGorus);
        }
        else
        {
            Quaternion yawDonus = Quaternion.Euler(0f, Yaw, 0f);
            Vector3 merkez = hedef.position + hedefOfseti;
            transform.position = merkez + yawDonus * new Vector3(0f, yukseklik, -mesafe);
            transform.rotation = bakis;
            if (cam != null) cam.fieldOfView = normalGorus;
        }
    }
}