using Avalonia.Media.Imaging;
using System.IO;

namespace Milo.Helpers;

public static class Helpers
{
    public static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }
}