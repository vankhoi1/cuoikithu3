using System;

public static class VectorMath
{
    // Chuyển đổi byte[] trong DB ngược lại thành mảng float[] để tính toán
    public static float[] ToFloatArray(byte[] bytes)
    {
        if (bytes == null || bytes.Length % 4 != 0) return null;
        var floatArray = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, floatArray, 0, bytes.Length);
        return floatArray;
    }

    // Hàm tính độ tương đồng Cosine giữa 2 vector
    public static double CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA == null || vectorB == null || vectorA.Length != vectorB.Length)
        {
            return -1;
        }

        double dotProduct = 0.0;
        double magA = 0.0;
        double magB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magA += Math.Pow(vectorA[i], 2);
            magB += Math.Pow(vectorB[i], 2);
        }

        magA = Math.Sqrt(magA);
        magB = Math.Sqrt(magB);

        if (magA == 0 || magB == 0) return 0;

        return dotProduct / (magA * magB);
    }
}