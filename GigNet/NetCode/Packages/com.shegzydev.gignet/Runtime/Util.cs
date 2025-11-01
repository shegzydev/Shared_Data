public static class Util
{
    public static byte[] MergeArrays(params byte[][] byteArrays)
    {
        int length = 0;
        foreach (var byteArray in byteArrays) { length += byteArray.Length; }

        byte[] buffer = new byte[length];

        int offset = 0;
        foreach (var byteArray in byteArrays)
        {
            if (byteArray.Length == 0) continue;
            System.Buffer.BlockCopy(byteArray, 0, buffer, offset, byteArray.Length);
            offset += byteArray.Length;
        }

        return buffer;
    }

    public static int LengthOfArrays(params byte[][] byteArrays)
    {
        int length = 0;
        foreach (var byteArray in byteArrays) { length += byteArray.Length; }
        return length;
    }
}