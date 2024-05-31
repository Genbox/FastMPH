namespace Genbox.FastMPH.Benchmarks.Misc;

public static class StringHelper
{
    private const string _alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public static IEnumerable<string> GetRandomStrings(int length, int count)
    {
        Random r = new Random(42);
        char[] buffer = new char[length];

        for (int i = 0; i < count; i++)
        {
            for (int j = 0; j < length; j++)
            {
                buffer[j] = _alphabet[r.Next(0, _alphabet.Length)];
            }

            yield return new string(buffer);
        }
    }
}