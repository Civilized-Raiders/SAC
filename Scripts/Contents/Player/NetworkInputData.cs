using Fusion;
using UnityEngine;

public enum InputButton
{
    Interact,
    Sprint,
    Carry,
    Detach,
    HeadLight
}
public struct NetworkInputData : INetworkInput
{
    public Vector3 moveDirection;
    public Vector2 lookDelta;
    public NetworkButtons buttons;
}