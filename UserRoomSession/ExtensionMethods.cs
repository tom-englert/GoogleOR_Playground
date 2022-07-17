internal static class ExtensionMethods
{
    public static IEnumerable<(T, T)> GetAllPairs<T>(this IList<T> items)
    {
        for (var i = 0; i < items.Count - 1; i++)
        {
            for (var k = i + 1; k < items.Count; k++)
            {
                yield return (items[i], items[k]);
            }
        }
    }
}
