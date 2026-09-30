using UnityEngine;

public abstract class Ability : MonoBehaviour
{
    public KeyCode key = KeyCode.Space;

    void Update()
    {
        if (Input.GetKeyDown(key)) TryActivate();
    }

    protected abstract void TryActivate();
}