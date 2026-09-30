using System.Collections;
using UnityEngine;

public class WallJumpAbility : Ability
{
    public int maxJumps = 3;
    public float duration = 0.5f;

    int jumpsLeft;
    bool active;
    CircleCollider2D col;
    Collider2D mazeCollider;

    void Awake()
    {
        col = GetComponent<CircleCollider2D>();
        jumpsLeft = maxJumps;

        // Busca el Tilemap "Maze" en la escena
        GameObject maze = GameObject.Find("Maze");
        if (maze != null) mazeCollider = maze.GetComponent<Collider2D>();
    }

    protected override void TryActivate()
    {
        if (active || jumpsLeft <= 0) return;
        StartCoroutine(Use());
    }

    IEnumerator Use()
    {
        active = true;
        jumpsLeft--;
        col.enabled = false;

        yield return new WaitForSeconds(duration);

        // No reactivar mientras el jugador siga dentro de una pared del laberinto
        float r = col.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
        while (mazeCollider != null && IsInsideWall(r))
            yield return null;

        col.enabled = true;
        active = false;
    }

    bool IsInsideWall(float radius)
    {
        var filter = new ContactFilter2D();
        filter.NoFilter();
        Collider2D[] results = new Collider2D[8];
        int count = Physics2D.OverlapCircle(transform.position, radius, filter, results);

        for (int i = 0; i < count; i++)
            if (results[i] == mazeCollider) return true;

        return false;
    }
}