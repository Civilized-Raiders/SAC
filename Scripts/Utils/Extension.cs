using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/*
 * Note : Extension.cs 5/29
 * Null 체크, GetOrAddComponent 등 자주 쓰이는 확장 메서드들을 모아둔 클래스입니다.
 * 없어도 되는 기능들이지만, 있으면 개발할때 시간을 조금이나마 절약할 수 있는 코드입니다.
 */

public static class Extension
{
    public static T GetOrAddComponent<T>(this GameObject go) where T : UnityEngine.Component
    {
        return Util.GetOrAddComponent<T>(go);
    }


    #region Null Check Extensions
    public static bool IsNull(this UnityEngine.Object go) { return ReferenceEquals(go, null); }
    public static bool IsNull(this System.Object go) { return ReferenceEquals(go, null); }

    public static bool IsFakeNull(this UnityEngine.Object go) { return (go.IsNull() == false && go == true) == false; }

    public static bool isValid(this GameObject go) { return go.IsNull() == false && go.activeSelf == true; }
    #endregion
}
public static class VectorExtensions
{
    public static Vector2 XY(this Vector3 v)
    {
        return new Vector2(v.x, v.y);
    }
    public static Vector2 XZ(this Vector3 v)
    {
        return new Vector2(v.x, v.z);
    }
    public static Vector2 YZ(this Vector3 v)
    {
        return new Vector2(v.y, v.z);
    }
}

