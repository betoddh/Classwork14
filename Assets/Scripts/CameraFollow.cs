using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public Renderer maze;

    Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (player == null) return;

        float height = cam.orthographicSize;
        float width = height * cam.aspect;

        Bounds walls = maze.bounds;

        float x = Mathf.Clamp(player.position.x, walls.min.x + width, walls.max.x - width);
        float y = Mathf.Clamp(player.position.y, walls.min.y + height, walls.max.y - height);

        transform.position = new Vector3(x, y, transform.position.z);
    }
}
