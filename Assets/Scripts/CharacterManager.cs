using UnityEngine;

public class CharacterManager : MonoBehaviour
{
    public GameObject[] characters;
    public Transform[] spawnPoints;
    public GameObject[] endPoints;
    public GameObject[] buttons;
    public CameraFollow cameraFollow;

    GameObject player;
    int chosen;

    void Start()
    {
        foreach (GameObject endPoint in endPoints)
        {
            endPoint.SetActive(false);
        }
    }

    public void Spawn(int number)
    {
        chosen = number;
        player = Instantiate(characters[number], spawnPoints[number].position, Quaternion.identity);
        cameraFollow.player = player.transform;
        endPoints[number].SetActive(true);

        foreach (GameObject button in buttons)
        {
            if (button != null) button.SetActive(false);
        }
    }

    void Update()
    {
        if (player == null) return;

        if (Vector2.Distance(player.transform.position, endPoints[chosen].transform.position) < 0.5f)
        {
            Destroy(player);
            Destroy(endPoints[chosen]);
            Destroy(buttons[chosen]);
            player = null;

            foreach (GameObject button in buttons)
            {
                if (button != null) button.SetActive(true);
            }
        }
    }
}
