
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// small utilities used by scripts
public static class Utils
{
    // Wrap X modulo world width (2^z)
    public static int WrapX(int x, int z)
    {
        int n = 1 << z;
        if (n == 0) return x;
        int r = x % n;
        if (r < 0) r += n;
        return r;
    }
}
