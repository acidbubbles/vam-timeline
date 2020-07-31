using UnityEngine;
using VamTimeline;

#pragma warning disable IDE1006
/// <summary>
/// Converted from https://github.com/unity3d-jp/MeshSync/blob/dev/Plugin%7E/Src/mscore/msUnitySpecific.cpp
/// </summary>
public static class UnitySpecific
{
    const float kDefaultWeight = 1.0f / 3.0f;
    const float kCurveTimeEpsilon = 0.00001f;

    private static Quaternion GetValue(BezierAnimationCurve x, BezierAnimationCurve y, BezierAnimationCurve z, BezierAnimationCurve w, int key)
    {
        return new Quaternion(x.GetKeyframe(key).value, y.GetKeyframe(key).value, z.GetKeyframe(key).value, w.GetKeyframe(key).value);
    }

    private static void SetValue(BezierAnimationCurve x, BezierAnimationCurve y, BezierAnimationCurve z, BezierAnimationCurve w, int key, Quaternion q)
    {
        SetValue(x, key, q.x);
        SetValue(y, key, q.y);
        SetValue(z, key, q.z);
        SetValue(w, key, q.w);
    }

    private static void SetValue(BezierAnimationCurve curve, int key, float value)
    {
        var keyframe = curve.GetKeyframe(key);
        keyframe.value = value;
    }

    public static void EnsureQuaternionContinuityAndRecalculateSlope(BezierAnimationCurve x, BezierAnimationCurve y, BezierAnimationCurve z, BezierAnimationCurve w)
    {
        var keyCount = x.length;
        if (keyCount < 2) return;
        var last = GetValue(x, y, z, w, keyCount - 1);
        for (int i = 0; i < keyCount; i++)
        {
            var cur = GetValue(x, y, z, w, i);
            if (Quaternion.Dot(cur, last) < 0.0f)
                cur = new Quaternion(-cur.x, -cur.y, -cur.z, -cur.w);
            last = cur;
            SetValue(x, y, z, w, i, cur);
        }

        // for (int i = 0; i < keyCount; i++)
        // {
        //     RecalculateSplineSlopeT(x, i);
        //     RecalculateSplineSlopeT(y, i);
        //     RecalculateSplineSlopeT(z, i);
        //     RecalculateSplineSlopeT(w, i);
        // }
    }

    // private static void RecalculateSplineSlopeT(BezierAnimationCurve curve, int key, float b = 0.0f)
    // {
    //     if (curve.length < 2)
    //         return;

    //     var keyframe = curve.GetKeyframe(key);
    //     if (key == 0)
    //     {
    //         float dx = curve.GetKeyframe(1).time - curve.GetKeyframe(0).time;
    //         float dy = curve.GetKeyframe(1).value - curve.GetKeyframe(0).value;
    //         float m = dy / dx;
    //         keyframe.inTangent = m;
    //         keyframe.outTangent = m;
    //         keyframe.outWeight = kDefaultWeight;
    //     }
    //     else if (key == curve.length - 1)
    //     {
    //         float dx = keyframe.time - curve.GetKeyframe(key - 1).time;
    //         float dy = keyframe.value - curve.GetKeyframe(key - 1).value;
    //         float m = dy / dx;
    //         keyframe.inTangent = m;
    //         keyframe.outTangent = m;
    //         keyframe.inWeight = kDefaultWeight;
    //     }
    //     else
    //     {
    //         float dx1 = keyframe.time - curve.GetKeyframe(key - 1).time;
    //         float dy1 = keyframe.value - curve.GetKeyframe(key - 1).value;

    //         float dx2 = curve.GetKeyframe(key + 1).time - keyframe.time;
    //         float dy2 = curve.GetKeyframe(key + 1).value - keyframe.value;

    //         float m1 = SafeDiv(dy1, dx1);
    //         float m2 = SafeDiv(dy2, dx2);

    //         float m = (1.0f + b) * 0.5f * m1 + (1.0f - b) * 0.5f * m2;
    //         keyframe.inTangent = m;
    //         keyframe.outTangent = m;
    //         keyframe.inWeight = kDefaultWeight;
    //         keyframe.outWeight = kDefaultWeight;
    //     }

    //     curve.MoveKey(key, keyframe);
    // }

    // private static float SafeDiv(float y, float x)
    // {
    //     if (Mathf.Abs(x) > kCurveTimeEpsilon)
    //         return y / x;
    //     else
    //         return 0;
    // }
}
#pragma warning restore IDE1006
