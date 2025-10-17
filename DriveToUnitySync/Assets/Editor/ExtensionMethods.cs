using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ExtensionMethods
{
    //Use an Extention Method
    //Static function in a static class
    public static Vector3 Round(this Vector3 v)
    {
        v.x = Mathf.Round(v.x);
        v.y = Mathf.Round(v.y);
        v.z = Mathf.Round(v.z);

        return v;
    }

    public static Vector3 Round(this Vector3 v, float size)
    {
        //rescale it, put it in a different space then snap it in that space, then scale it up again
        return (v / size).Round() * size;
    }

    public static float Round(this float v, float size)
    {
        //rescale it, put it in a different space then snap it in that space, then scale it up again
        return Mathf.Round(v / size) * size;
    }

    public static float AtLeast(this float v, float min) => Mathf.Max(v, min);
    public static int AtLeast(this int v, int min) => Mathf.Max(v, min);
    public static float AtMost(this float v, float max) => Mathf.Min(v, max);
    public static int AtMost(this int v, int max) => Mathf.Min(v, max);
}