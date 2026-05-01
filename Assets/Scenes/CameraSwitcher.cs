using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    [Header("카메라 목록 (여기에 카메라들을 드래그해서 넣으세요)")]
    public GameObject[] cameras;
    
    private int currentIndex = 0;

    void Start()
    {
        // 시작할 때 첫 번째 카메라만 켜고, 나머지는 전부 끕니다.
        if (cameras.Length > 0)
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].SetActive(i == 0);
            }
        }
    }

    // 버튼을 누를 때마다 실행될 함수
    public void SwitchNextCamera()
    {
        if (cameras.Length == 0) return;

        // 1. 현재 켜져 있는 카메라를 끕니다.
        cameras[currentIndex].SetActive(false);

        // 2. 다음 카메라 인덱스로 넘어갑니다. (마지막이면 다시 0번으로 돌아옴)
        currentIndex = (currentIndex + 1) % cameras.Length;

        // 3. 다음 카메라를 켭니다.
        cameras[currentIndex].SetActive(true);
    }
}