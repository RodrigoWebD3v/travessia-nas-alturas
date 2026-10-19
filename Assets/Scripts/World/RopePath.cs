using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Percurso fixo de uma corda entre duas âncoras, com leve curvatura (sag).
/// O jogador sobe pela âncora inicial e anda ao longo de Evaluate(t).
/// </summary>
[ExecuteAlways]
public class RopePath : MonoBehaviour
{
    public static readonly List<RopePath> All = new List<RopePath>();

    public Transform startAnchor;
    public Transform endAnchor;
    public float sag = 0.35f;
    public LineRenderer line;
    public int segments = 32;
    [Tooltip("Amplitude do balanço visual quando alguém está na corda.")]
    public float wobble = 0.04f;

    public Vector3 StartPoint => startAnchor.position;
    public Vector3 EndPoint => endAnchor.position;
    public float Length => Vector3.Distance(StartPoint, EndPoint);

    public Vector3 Forward
    {
        get
        {
            Vector3 f = EndPoint - StartPoint;
            f.y = 0f;
            return f.normalized;
        }
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        UpdateLine();
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    public Vector3 Evaluate(float t)
    {
        t = Mathf.Clamp01(t);
        Vector3 p = Vector3.Lerp(StartPoint, EndPoint, t);
        p.y -= sag * 4f * t * (1f - t);
        return p;
    }

    void LateUpdate()
    {
        UpdateLine();
    }

    public void UpdateLine()
    {
        if (line == null || startAnchor == null || endAnchor == null) return;
        line.positionCount = segments + 1;
        float time = Application.isPlaying ? Time.time : 0f;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = Evaluate(t);
            p.y += Mathf.Sin(time * 3f + t * 9f) * wobble * 4f * t * (1f - t);
            line.SetPosition(i, p);
        }
    }
}
