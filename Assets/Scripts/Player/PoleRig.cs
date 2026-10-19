using UnityEngine;

/// <summary>
/// Faz a vara balançar para cima e para baixo (no plano lateral) em torno do ponto entre as mãos,
/// e mantém as mãos agarradas a ela com IK de dois ossos (braço + antebraço).
///
/// Roda em LateUpdate, depois do Animator. Os ossos que este script altera voltam à pose de
/// repouso a cada quadro antes de serem ajustados, então a rotação nunca se acumula.
/// </summary>
public class PoleRig : MonoBehaviour
{
    [Header("Ossos")]
    public Transform shoulderRig;
    public Transform upperArmL, lowerArmL, handL;
    public Transform upperArmR, lowerArmR, handR;

    [Header("Vara")]
    [Tooltip("Pivô da vara, no ponto entre as mãos (filho do osso Chest).")]
    public Transform pivot;
    [Tooltip("Pontos de pegada na vara (filhos do pivô).")]
    public Transform gripL, gripR;
    [Tooltip("Referência de frente do personagem: a vara gira em torno deste eixo.")]
    public Transform forwardReference;

    [Header("Ajustes")]
    [Tooltip("Fração do ângulo que os ombros acompanham (encolher um ombro).")]
    [Range(0f, 0.5f)] public float shoulderFollow = 0.15f;
    [Tooltip("Quanto a vara sobe (m) quando está muito inclinada, para o braço de baixo alcançar.")]
    public float liftAtMaxTilt = 0.1f;
    [Tooltip("Quanto a vara se aproxima do corpo (m) quando está muito inclinada.")]
    public float pullInAtMaxTilt = 0.06f;

    /// <summary>Ângulo atual da vara em graus. Positivo levanta o lado direito.</summary>
    public float Angle { get; set; }

    Quaternion shoulderRest, lowerLRest, lowerRRest, handLRest, handRRest, pivotRestRot;
    Vector3 pivotRestPos;
    Quaternion handLToPivot, handRToPivot;

    void Awake()
    {
        shoulderRest = shoulderRig.localRotation;
        lowerLRest = lowerArmL.localRotation;
        lowerRRest = lowerArmR.localRotation;
        handLRest = handL.localRotation;
        handRRest = handR.localRotation;
        pivotRestPos = pivot.localPosition;
        pivotRestRot = pivot.localRotation;
        // orientação das mãos relativa à vara na pose de repouso
        handLToPivot = Quaternion.Inverse(pivot.rotation) * handL.rotation;
        handRToPivot = Quaternion.Inverse(pivot.rotation) * handR.rotation;
    }

    void LateUpdate()
    {
        // 1. Parte da pose de repouso (sem acumular rotação entre quadros).
        shoulderRig.localRotation = shoulderRest;
        lowerArmL.localRotation = lowerLRest;
        lowerArmR.localRotation = lowerRRest;
        handL.localRotation = handLRest;
        handR.localRotation = handRRest;
        pivot.localRotation = pivotRestRot;

        Vector3 forward = forwardReference.forward;
        float tilt = Mathf.Abs(Mathf.Sin(Angle * Mathf.Deg2Rad));

        // 2. Ombros acompanham um pouco; a vara gira no plano lateral em torno do centro das mãos.
        shoulderRig.rotation = Quaternion.AngleAxis(Angle * shoulderFollow, forward) * shoulderRig.rotation;
        pivot.localPosition = pivotRestPos;
        pivot.position += forwardReference.up * (liftAtMaxTilt * tilt) - forward * (pullInAtMaxTilt * tilt);
        pivot.rotation = Quaternion.AngleAxis(Angle, forward) * pivot.rotation;

        // 3. Cada mão vai até seu ponto de pegada.
        SolveTwoBone(upperArmL, lowerArmL, handL, gripL.position);
        SolveTwoBone(upperArmR, lowerArmR, handR, gripR.position);
        handL.rotation = pivot.rotation * handLToPivot;
        handR.rotation = pivot.rotation * handRToPivot;
    }

    /// <summary>
    /// IK analítico de dois ossos (lei dos cossenos): ajusta o ângulo do cotovelo para a distância
    /// ombro-alvo e depois gira o braço inteiro para apontar a mão ao alvo.
    /// </summary>
    static void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 target)
    {
        Vector3 a = upper.position, b = lower.position, c = end.position;
        float lab = Vector3.Distance(a, b);
        float lcb = Vector3.Distance(b, c);
        float lat = Mathf.Clamp(Vector3.Distance(a, target), 0.01f, lab + lcb - 0.001f);

        float acAb0 = AngleBetween(c - a, b - a);
        float baBc0 = AngleBetween(a - b, c - b);
        float acAt0 = AngleBetween(c - a, target - a);

        float acAb1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
        float baBc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

        Vector3 bendAxis = Vector3.Cross(c - a, b - a).normalized;
        Vector3 aimAxis = Vector3.Cross(c - a, target - a);
        aimAxis = aimAxis.sqrMagnitude > 1e-8f ? aimAxis.normalized : bendAxis;

        Quaternion r0 = Quaternion.AngleAxis((acAb1 - acAb0) * Mathf.Rad2Deg, bendAxis);
        Quaternion r1 = Quaternion.AngleAxis((baBc1 - baBc0) * Mathf.Rad2Deg, bendAxis);
        Quaternion r2 = Quaternion.AngleAxis(acAt0 * Mathf.Rad2Deg, aimAxis);

        lower.rotation = r1 * lower.rotation;
        upper.rotation = r2 * r0 * upper.rotation;
    }

    static float AngleBetween(Vector3 from, Vector3 to)
    {
        return Mathf.Acos(Mathf.Clamp(Vector3.Dot(from.normalized, to.normalized), -1f, 1f));
    }
}
