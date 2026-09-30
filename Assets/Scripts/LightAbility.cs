using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LightAbility : Ability
{
    public float extraRadius = 3f;
    public float duration = 5f;
    public float cooldown = 3f;

    Light2D playerLight;
    bool busy;

    void Awake()
    {
        playerLight = GetComponentInChildren<Light2D>();
    }

    protected override void TryActivate()
    {
        if (busy || playerLight == null) return;
        StartCoroutine(Use());
    }

    IEnumerator Use()
    {
        busy = true;
        float original = playerLight.pointLightOuterRadius;

        playerLight.pointLightOuterRadius = original + extraRadius;
        yield return new WaitForSeconds(duration);

        playerLight.pointLightOuterRadius = original;
        yield return new WaitForSeconds(cooldown);

        busy = false;
    }
} 