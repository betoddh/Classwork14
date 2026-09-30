using System.Collections;
using UnityEngine;

public class SprintAbility : Ability
{
    public float speedMultiplier = 2f;
    public float duration = 5f;

    MazeMovement movement;
    bool active;

    void Awake()
    {
        movement = GetComponent<MazeMovement>();
    }

    protected override void TryActivate()
    {
        if (active || movement == null) return;
        StartCoroutine(Use());
    }

    IEnumerator Use()
    {
        active = true;
        float original = movement.speed;

        movement.speed = original * speedMultiplier;
        yield return new WaitForSeconds(duration);

        movement.speed = original;
        active = false;
    }
}