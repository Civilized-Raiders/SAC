using Fusion;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RandomFace : NetworkBehaviour
{
    [SerializeField] private List<Sprite> Faces;
    [SerializeField] private Image face;


    [Networked, OnChangedRender(nameof(OnFaceChanged))]
    private int FaceIndex { get; set; }
    
    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            FaceIndex = Random.Range(0, Faces.Count);
        }

        ApplyFace();

    }
    private void OnFaceChanged()
    {
        ApplyFace();
    }
    private void ApplyFace()
    {
        if (face == null || Faces == null || Faces.Count == 0)
            return;

        if (FaceIndex < 0 || FaceIndex >= Faces.Count)
            return;

        face.sprite = Faces[FaceIndex];
    }
}
