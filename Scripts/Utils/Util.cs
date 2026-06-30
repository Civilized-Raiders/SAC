using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Note :   Util.cs 5/29
 * 컴포넌트 추가, 자식 객체 탐색 등 자주 쓰이는 유틸리티 함수들을 모아둔 클래스입니다.
 */

public class Util
{
    // 컴포넌트 찾은 후 추가하기
    public static T GetOrAddComponent<T>(GameObject go) where T : UnityEngine.Component
    {
        T component = go.GetComponent<T>();

        if (component.IsNull() == true)
            component = go.AddComponent<T>();

        return component;
    }

    // GameObject는 컴포넌트가 아님 --> Transform을 통해 찾음
    public static GameObject FindChild(GameObject go, string name = null, bool recursive = false)
    {
        Transform transform = FindChild<Transform>(go, name, recursive);
        if (transform.IsNull() == true)
            return null;

        return transform.gameObject;
    }

    // 자식 객체 컴포넌트 찾기
    public static T FindChild<T>(GameObject go, string name = null, bool recursive = false) where T : UnityEngine.Object
    {
        if (go.IsNull() == true) return null;

        // recursive : 하위 자식의 자식까지 찾는지 여부
        if (recursive == false)
        {
            // 직속 자식(1단계 하위)만 탐색
            for (int i = 0; i < go.transform.childCount; i++)
            {
               
                Transform transform = go.transform.GetChild(i);

                if (string.IsNullOrEmpty(name) || transform.name == name)
                {
                    // 해당 T(Button, Text, ...) 컴포넌트 반환
                    T component = transform.GetComponent<T>();
                    if (component.IsNull() == false)
                        return component;
                }
            }
        }
        else
        {
            
            foreach (T component in go.GetComponentsInChildren<T>())
            {
                if (string.IsNullOrEmpty(name) || component.name == name)
                    return component;
            }
        }

        return null;
    }
}
