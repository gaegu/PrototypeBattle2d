using UnityEngine;

public class AutoDespawnTime : MonoBehaviour
{
    [SerializeField]
    private bool isDestroy = true;

    public float time;
    private float _elapsedTime;

    public System.Action<Transform> callback = null;

    void OnSpawned()
    {
        _elapsedTime = 0.0f;
    }

    public void Reset()
    {
        _elapsedTime = 0.0f;

        if (!isDestroy)
            this.SafeSetActive(false);
    }

    void Update()
    {
        _elapsedTime += Time.deltaTime;

        if (_elapsedTime < time)
            return;

        if (callback != null)
        {
            callback(transform);

            callback = null;
        }

        if (isDestroy)
        {
            Destroy(gameObject);
        }
        else
        {
            this.SafeSetActive(false);
        }
    }
}
