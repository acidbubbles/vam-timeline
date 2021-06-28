using System.Runtime.CompilerServices;
using UnityEngine;

namespace VamTimeline
{
    public static class QuaternionUtil
    {
        // https://wiki.unity3d.com/index.php/Averaging_Quaternions_and_Vectors

        [MethodImpl(256)]
        public static void AverageQuaternion(ref Vector4 cumulative, Quaternion newRotation, Quaternion firstRotation, float weight)
        {
            //Before we add the new rotation to the average (mean), we have to check whether the quaternion has to be inverted. Because
            //q and -q are the same rotation, but cannot be averaged, we have to make sure they are all the same.
            if (!AreQuaternionsClose(newRotation, firstRotation))
            {
                newRotation = InverseSignQuaternion(newRotation);
            }

            //Average the values
            cumulative.w += newRotation.w * weight;
            cumulative.x += newRotation.x * weight;
            cumulative.y += newRotation.y * weight;
            cumulative.z += newRotation.z * weight;
        }

        [MethodImpl(256)]
        public static Quaternion FromVector(Vector4 cumulative)
        {
            //note: if speed is an issue, you can skip the normalization step
            return NormalizeQuaternion(cumulative.x, cumulative.y, cumulative.z, cumulative.w);
        }

        [MethodImpl(256)]
        private static Quaternion NormalizeQuaternion(float x, float y, float z, float w)
        {
            var lengthD = 1.0f / (w * w + x * x + y * y + z * z);
            w *= lengthD;
            x *= lengthD;
            y *= lengthD;
            z *= lengthD;

            return new Quaternion(x, y, z, w);
        }

        //Changes the sign of the quaternion components. This is not the same as the inverse.
        [MethodImpl(256)]
        private static Quaternion InverseSignQuaternion(Quaternion q)
        {
            return new Quaternion(-q.x, -q.y, -q.z, -q.w);
        }

        //Returns true if the two input quaternions are close to each other. This can
        //be used to check whether or not one of two quaternions which are supposed to
        //be very similar but has its component signs reversed (q has the same rotation as
        //-q)
        [MethodImpl(256)]
        private static bool AreQuaternionsClose(Quaternion q1, Quaternion q2)
        {

            var dot = Quaternion.Dot(q1, q2);
            return !(dot < 0.0f);
        }
    }
}
