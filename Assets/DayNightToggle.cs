using UnityEngine;

public class DayNightToggle : MonoBehaviour
{
    [Header("조명 세팅")]
    public Light sunLight;           // 낮을 밝혀줄 태양 (Directional Light)
    public GameObject lightingGroup; // 에셋에 원래 있던 'LIGHTING' 그룹 (가로등 모델 + 불빛)

    private bool isDay = false;      // 시작은 기본 '밤' 상태로 세팅
    private Light[] streetBulbs;     // 그룹 안에서 '전구(Light)'만 찾아올 배열

    void Start()
    {
        // 시작할 때 LIGHTING 그룹을 뒤져서 '빛(Light)' 역할만 하는 녀석들을 싹 다 찾아냅니다!
        if (lightingGroup != null)
        {
            streetBulbs = lightingGroup.GetComponentsInChildren<Light>();
        }
    }

    public void OnToggleClicked()
    {
        isDay = !isDay; // 상태 뒤집기

        if (isDay)
        {
            // ☀️ [낮 모드]
            if(sunLight != null) sunLight.intensity = 1.5f; // 태양 켜기
            
            // 가로등 기둥은 살려두고 '전구'만 끕니다!
            if(streetBulbs != null)
            {
                foreach(Light bulb in streetBulbs) bulb.enabled = false;
            }
            Debug.Log("☀️ 낮 모드: 가로등 소등!");
        }
        else
        {
            // 🌙 [밤 모드]
            if(sunLight != null) sunLight.intensity = 0.05f; // 태양 끄기 (달빛)
            
            // '전구'만 다시 켭니다!
            if(streetBulbs != null)
            {
                foreach(Light bulb in streetBulbs) bulb.enabled = true;
            }
            Debug.Log("🌙 밤 모드: 가로등 점등!");
        }
    }
}