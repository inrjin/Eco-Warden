using UnityEngine;

public class DummySensor : MonoBehaviour
{
    [Header("관제 대상 (쓰레기 봉투)")]
    public GameObject trashObject; // 아까 만든 빨간 공을 연결할 변수

    [Header("가상 센서 데이터 (임베디드 x, y)")]
    public float sensorX = 0f;
    public float sensorY = 0f;

    // 초당 이동 속도 (부드러운 이동을 위해 Lerp 사용)
    public float moveSpeed = 5f;

    void Update()
    {
        // 1. 임베디드(2D) 데이터를 유니티(3D) 좌표로 매핑
        // 센서의 x는 유니티의 X축, 센서의 y는 유니티의 Z축(깊이)으로 들어갑니다.
        // Y축(높이)은 바닥 위쪽인 0.5f로 고정합니다.
        Vector3 targetPosition = new Vector3(sensorX, 0.5f, sensorY);

        // 2. 쓰레기 봉투 객체가 해당 위치로 부드럽게 이동
        if (trashObject != null)
        {
            trashObject.transform.position = Vector3.Lerp(
                trashObject.transform.position, 
                targetPosition, 
                Time.deltaTime * moveSpeed
            );
        }
    }
}