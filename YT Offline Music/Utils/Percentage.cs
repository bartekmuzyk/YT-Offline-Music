using System;

namespace YT_Offline_Music.Utils;

public static class Percentage
{
    public static int Calculate(int value, int maxValue) => (int)Math.Ceiling(value * 100.0 / maxValue);
}