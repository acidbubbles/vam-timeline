using UnityEngine;

#pragma warning disable IDE1006
/// <summary>
/// Converted from https://github.com/unity3d-jp/MeshSync/blob/dev/Plugin%7E/Src/mscore/msUnitySpecific.cpp
/// </summary>
public static class UnitySpecific
{
    const float kDefaultWeight = 1.0f / 3.0f;
    const float kCurveTimeEpsilon = 0.00001f;

    private static Quaternion GetValue(AnimationCurve x, AnimationCurve y, AnimationCurve z, AnimationCurve w, int key)
    {
        return new Quaternion(x[key].value, y[key].value, z[key].value, w[key].value);
    }

    private static void SetValue(AnimationCurve x, AnimationCurve y, AnimationCurve z, AnimationCurve w, int key, Quaternion q)
    {
        SetValue(x, key, q.x);
        SetValue(y, key, q.y);
        SetValue(z, key, q.z);
        SetValue(w, key, q.w);
    }

    private static void SetValue(AnimationCurve curve, int key, float value)
    {
        var keyframe = curve[key];
        keyframe.value = value;
        curve.MoveKey(key, keyframe);
    }

    public static void EnsureQuaternionContinuityAndRecalculateSlope(AnimationCurve x, AnimationCurve y, AnimationCurve z, AnimationCurve w)
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

        for (int i = 0; i < keyCount; i++)
        {
            RecalculateSplineSlopeT(x, i);
            RecalculateSplineSlopeT(y, i);
            RecalculateSplineSlopeT(z, i);
            RecalculateSplineSlopeT(w, i);
        }
    }

    private static void RecalculateSplineSlopeT(AnimationCurve curve, int key, float b = 0.0f)
    {
        if (curve.length < 2)
            return;

        var keyframe = curve[key];
        if (key == 0)
        {
            float dx = curve[1].time - curve[0].time;
            float dy = curve[1].value - curve[0].value;
            float m = dy / dx;
            keyframe.inTangent = m;
            keyframe.outTangent = m;
            keyframe.outWeight = kDefaultWeight;
        }
        else if (key == curve.length - 1)
        {
            float dx = keyframe.time - curve[key - 1].time;
            float dy = keyframe.value - curve[key - 1].value;
            float m = dy / dx;
            keyframe.inTangent = m;
            keyframe.outTangent = m;
            keyframe.inWeight = kDefaultWeight;
        }
        else
        {
            float dx1 = keyframe.time - curve[key - 1].time;
            float dy1 = keyframe.value - curve[key - 1].value;

            float dx2 = curve[key + 1].time - keyframe.time;
            float dy2 = curve[key + 1].value - keyframe.value;

            float m1 = SafeDiv(dy1, dx1);
            float m2 = SafeDiv(dy2, dx2);

            float m = (1.0f + b) * 0.5f * m1 + (1.0f - b) * 0.5f * m2;
            keyframe.inTangent = m;
            keyframe.outTangent = m;
            keyframe.inWeight = kDefaultWeight;
            keyframe.outWeight = kDefaultWeight;
        }

        curve.MoveKey(key, keyframe);
    }

    private static float SafeDiv(float y, float x)
    {
        if (Mathf.Abs(x) > kCurveTimeEpsilon)
            return y / x;
        else
            return 0;
    }
}
#pragma warning restore IDE1006
