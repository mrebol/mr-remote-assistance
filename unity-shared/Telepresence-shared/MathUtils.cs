using UnityEngine;

namespace Telepresence
{
    public static class MathUtils
    {
        // from here: https://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/
        public static Quaternion rotToQu(float[] m1) { 
            var w = Mathf.Sqrt(1.0f + m1[0] + m1[4] + m1[8]) / 2.0f;
            float w4 = (4.0f * w);
            var x = (m1[7] - m1[5]) / w4 ;
            var y = (m1[2] - m1[6]) / w4 ;
            var z = (m1[3] - m1[1]) / w4 ;
            return new Quaternion(x, y, z, w);
        }
    }
}