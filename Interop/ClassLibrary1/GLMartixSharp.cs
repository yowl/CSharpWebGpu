// Thanks to https://github.com/DominikNITA/Blazor3D-Playground of which this is a direct 
namespace GLMatrixSharp
{
    public class Mat4
    {
        public static float[] Create()
        {
            var res = new float[16];
            res[1] = 0;
            res[2] = 0;
            res[3] = 0;
            res[4] = 0;
            res[6] = 0;
            res[7] = 0;
            res[8] = 0;
            res[9] = 0;
            res[11] = 0;
            res[12] = 0;
            res[13] = 0;
            res[14] = 0;
            res[0] = 1;
            res[5] = 1;
            res[10] = 1;
            res[15] = 1;
            return res;
        }

        public static void Identity(float[] res)
        {
            res[0] = 1;
            res[1] = 0;
            res[2] = 0;
            res[3] = 0;

            res[4] = 0;
            res[5] = 1;
            res[6] = 0;
            res[7] = 0;

            res[8] = 0;
            res[9] = 0;
            res[10] = 1;
            res[11] = 0;

            res[12] = 0;
            res[13] = 0;
            res[14] = 0;
            res[15] = 1;
        }

        public static float[] Translate(float[] a, float[] v)
        {
            var res = new float[16];

            if (a.Length < 16 || v.Length < 3) throw new ArgumentException();

            float x = v[0], y = v[1], z = v[2];

            res[0] = a[0];
            res[1] = a[1];
            res[2] = a[2];
            res[3] = a[3];
            res[4] = a[4];
            res[5] = a[5];
            res[6] = a[6];
            res[7] = a[7];
            res[8] = a[8];
            res[9] = a[9];
            res[10] = a[10];
            res[11] = a[11];

            res[12] = a[0] * x + a[4] * y + a[8] * z + a[12];
            res[13] = a[1] * x + a[5] * y + a[9] * z + a[13];
            res[14] = a[2] * x + a[6] * y + a[10] * z + a[14];
            res[15] = a[3] * x + a[7] * y + a[11] * z + a[15];

            return res;
        }

        public static float[] Perspective(float fovy, float aspect, float near, float far)
        {
            var res = new float[16];
            var f = (float)(1.0 / Math.Tan(fovy / 2));
            res[0] = (f / aspect);
            res[1] = 0;
            res[2] = 0;
            res[3] = 0;
            res[4] = 0;
            res[5] = f;
            res[6] = 0;
            res[7] = 0;
            res[8] = 0;
            res[9] = 0;
            res[11] = -1;
            res[12] = 0;
            res[13] = 0;
            res[15] = 0;
            if (!float.IsInfinity(far))
            {
                var nf = 1 / (near - far);
                res[10] = (far + near) * nf;
                res[14] = 2 * far * near * nf;
            }
            else
            {
                res[10] = -1;
                res[14] = -2 * near;
            }
            return res;
        }

        // these are not as efficient as Mat4 as we create new arrays
        public static float[] Rotate(float[] a, float rad, float[] axis)
        {
            float x = axis[0], y = axis[1], z = axis[2];
            float[] res = new float[16];
            float len = (float)Math.Sqrt(x * x + y * y + z * z);
            float s, c, t;
            float a00, a01, a02, a03;
            float a10, a11, a12, a13;
            float a20, a21, a22, a23;
            float b00, b01, b02;
            float b10, b11, b12;
            float b20, b21, b22;
            //if (len < glMatrix.EPSILON)
            //{
            //    return null;
            //}
            len = 1 / len;
            x *= len;
            y *= len;
            z *= len;
            s = (float)Math.Sin(rad);
            c = (float)Math.Cos(rad);
            t = 1 - c;
            a00 = a[0];
            a01 = a[1];
            a02 = a[2];
            a03 = a[3];
            a10 = a[4];
            a11 = a[5];
            a12 = a[6];
            a13 = a[7];
            a20 = a[8];
            a21 = a[9];
            a22 = a[10];
            a23 = a[11];
            // Construct the elements of the rotation matrix
            b00 = x * x * t + c;
            b01 = y * x * t + z * s;
            b02 = z * x * t - y * s;
            b10 = x * y * t - z * s;
            b11 = y * y * t + c;
            b12 = z * y * t + x * s;
            b20 = x * z * t + y * s;
            b21 = y * z * t - x * s;
            b22 = z * z * t + c;
            // Perform rotation-specific matrix multiplication
            res[0] = a00 * b00 + a10 * b01 + a20 * b02;
            res[1] = a01 * b00 + a11 * b01 + a21 * b02;
            res[2] = a02 * b00 + a12 * b01 + a22 * b02;
            res[3] = a03 * b00 + a13 * b01 + a23 * b02;
            res[4] = a00 * b10 + a10 * b11 + a20 * b12;
            res[5] = a01 * b10 + a11 * b11 + a21 * b12;
            res[6] = a02 * b10 + a12 * b11 + a22 * b12;
            res[7] = a03 * b10 + a13 * b11 + a23 * b12;
            res[8] = a00 * b20 + a10 * b21 + a20 * b22;
            res[9] = a01 * b20 + a11 * b21 + a21 * b22;
            res[10] = a02 * b20 + a12 * b21 + a22 * b22;
            res[11] = a03 * b20 + a13 * b21 + a23 * b22;

            //copy the unchanged last row
            res[12] = a[12];
            res[13] = a[13];
            res[14] = a[14];
            res[15] = a[15];
            return res;
        }

        /**
 * Rotates a matrix by the given angle around the X axis
 *
 * @param {mat4} out the receiving matrix
 * @param {ReadonlyMat4} a the matrix to rotate
 * @param {Number} rad the angle to rotate the matrix by
 * @returns {mat4} out
 */
        public static float[] RotateX(float[] a, float rad)
        {
            var s = (float)Math.Sin(rad);
            var c = (float)Math.Cos(rad);

            float a10 = a[4];
            float a11 = a[5];
            float a12 = a[6];
            float a13 = a[7];
            float a20 = a[8];
            float a21 = a[9];
            float a22 = a[10];
            float a23 = a[11];
            float[] res = new float[16];

            res[0] = a[0];
            res[1] = a[1];
            res[2] = a[2];
            res[3] = a[3];
            res[12] = a[12];
            res[13] = a[13];
            res[14] = a[14];
            res[15] = a[15];

            // Perform axis-specific matrix multiplication
            res[4] = a10 * c + a20 * s;
            res[5] = a11 * c + a21 * s;
            res[6] = a12 * c + a22 * s;
            res[7] = a13 * c + a23 * s;
            res[8] = a20 * c - a10 * s;
            res[9] = a21 * c - a11 * s;
            res[10] = a22 * c - a12 * s;
            res[11] = a23 * c - a13 * s;

            return res;
        }

        public static float[] Multiply(float[] a, float[] b)
        {
            float[] res = new float[16];

            float a00 = a[0];
            float a01 = a[1];
            float a02 = a[2];
            float a03 = a[3];
            float a10 = a[4];
            float a11 = a[5];
            float a12 = a[6];
            float a13 = a[7];

            float a20 = a[8];
            float a21 = a[9];
            float a22 = a[10];
            float a23 = a[11];

            float a30 = a[12];
            float a31 = a[13];
            float a32 = a[14];
            float a33 = a[15];

            // Cache only the current line of the second matrix
            float b0 = b[0];
            float b1 = b[1];
            float b2 = b[2];
            float b3 = b[3];

            res[0] = b0 * a00 + b1 * a10 + b2 * a20 + b3 * a30;
            res[1] = b0 * a01 + b1 * a11 + b2 * a21 + b3 * a31;
            res[2] = b0 * a02 + b1 * a12 + b2 * a22 + b3 * a32;
            res[3] = b0 * a03 + b1 * a13 + b2 * a23 + b3 * a33;

            b0 = b[4];
            b1 = b[5];
            b2 = b[6];
            b3 = b[7];

            res[4] = b0 * a00 + b1 * a10 + b2 * a20 + b3 * a30;
            res[5] = b0 * a01 + b1 * a11 + b2 * a21 + b3 * a31;
            res[6] = b0 * a02 + b1 * a12 + b2 * a22 + b3 * a32;
            res[7] = b0 * a03 + b1 * a13 + b2 * a23 + b3 * a33;

            b0 = b[8];
            b1 = b[9];
            b2 = b[10];
            b3 = b[11];

            res[8] = b0 * a00 + b1 * a10 + b2 * a20 + b3 * a30;
            res[9] = b0 * a01 + b1 * a11 + b2 * a21 + b3 * a31;
            res[10] = b0 * a02 + b1 * a12 + b2 * a22 + b3 * a32;
            res[11] = b0 * a03 + b1 * a13 + b2 * a23 + b3 * a33;

            b0 = b[12];
            b1 = b[13];
            b2 = b[14];
            b3 = b[15];

            res[12] = b0 * a00 + b1 * a10 + b2 * a20 + b3 * a30;
            res[13] = b0 * a01 + b1 * a11 + b2 * a21 + b3 * a31;
            res[14] = b0 * a02 + b1 * a12 + b2 * a22 + b3 * a32;
            res[15] = b0 * a03 + b1 * a13 + b2 * a23 + b3 * a33;

            return res;
        }
    }
}
