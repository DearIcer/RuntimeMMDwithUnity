using UnityEngine;

public class FrameSkipper : MonoBehaviour
{
    [Header("帧率设置")]
    [Tooltip("目标帧率")]
    [Range(1, 30)]
    public int targetFPS = 12;

    [Header("时间控制")]
    [Tooltip("是否使用未缩放时间")]
    public bool useUnscaledTime = false;

    private float frameTimer = 0f;
    private float frameDuration;

    private void Start()
    {
        // 计算每帧持续时间
        UpdateFrameDuration();
    }

    private void Update()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        frameTimer += deltaTime;

        // 当累积时间超过帧持续时间时更新
        if (frameTimer >= frameDuration)
        {
            frameTimer = 0f;
        }
    }

    private void UpdateFrameDuration()
    {
        frameDuration = 1f / targetFPS;
    }

    private void OnValidate()
    {
        UpdateFrameDuration();
    }

    public void SetTargetFPS(int newFPS)
    {
        targetFPS = Mathf.Clamp(newFPS, 1, 30);
        UpdateFrameDuration();
    }
}