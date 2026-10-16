using WetWare.Core.Mathematics;

namespace WetWare.Core.Recognition;

/// <summary>7×9 pixel digit templates used as the "retina" input of the recognition demo.</summary>
public static class DigitPatterns
{
    public const int Width = 7;
    public const int Height = 9;
    public const int PixelCount = Width * Height;

    private static readonly string[][] Glyphs =
    [
        ["..###..", ".#...#.", "#.....#", "#.....#", "#.....#", "#.....#", "#.....#", ".#...#.", "..###.."],
        ["...#...", "..##...", ".#.#...", "...#...", "...#...", "...#...", "...#...", "...#...", ".#####."],
        [".#####.", "#.....#", "......#", ".....#.", "....#..", "...#...", "..#....", ".#.....", "#######"],
        [".#####.", "#.....#", "......#", "......#", "..####.", "......#", "......#", "#.....#", ".#####."],
        ["....##.", "...#.#.", "..#..#.", ".#...#.", "#....#.", "#######", ".....#.", ".....#.", ".....#."],
        ["#######", "#......", "#......", "######.", "......#", "......#", "......#", "#.....#", ".#####."],
        ["..####.", ".#.....", "#......", "#......", "######.", "#.....#", "#.....#", "#.....#", ".#####."],
        ["#######", "......#", ".....#.", "....#..", "...#...", "..#....", "..#....", "..#....", "..#...."],
        [".#####.", "#.....#", "#.....#", "#.....#", ".#####.", "#.....#", "#.....#", "#.....#", ".#####."],
        [".#####.", "#.....#", "#.....#", "#.....#", ".######", "......#", "......#", ".....#.", ".####.."],
    ];

    public static int ClassCount => Glyphs.Length;

    /// <summary>Clean template of <paramref name="digit"/> as 0/1 intensities.</summary>
    public static double[] Template(int digit)
    {
        var glyph = Glyphs[digit];
        var pixels = new double[PixelCount];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                pixels[y * Width + x] = glyph[y][x] == '#' ? 1.0 : 0.0;
        return pixels;
    }

    /// <summary>Template with each pixel flipped with probability <paramref name="noise"/>.</summary>
    public static double[] Noisy(int digit, double noise, RandomSource random)
    {
        var pixels = Template(digit);
        for (int i = 0; i < pixels.Length; i++)
            if (random.Bernoulli(noise)) pixels[i] = 1.0 - pixels[i];
        return pixels;
    }
}
