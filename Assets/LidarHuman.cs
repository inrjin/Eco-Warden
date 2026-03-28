using System.Collections;
using UnityEngine;

public class LidarHuman : MonoBehaviour
{
    public Vector3 targetPosition;
    private Animator anim;
    private float currentAnimSpeed = 0f;
    
    // ⭐️ 쓰레기를 버리는 중인지 체크하는 스위치
    private bool isThrowing = false; 

    void Start()
    {
        targetPosition = transform.position;
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        // 1. 쓰레기를 버리는 중(true)이라면, 아래의 이동 코드는 무시하고 그 자리에 가만히 서 있음!
        if (isThrowing) return;

        // --- 여기서부터는 기존과 동일한 이동/회전 로직 ---
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 5f);

        Vector3 direction = targetPosition - transform.position;
        direction.y = 0; 
        
        if (direction.magnitude > 0.05f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
        }

        if (anim != null)
        {
            float targetSpeed = 0f;
            float distance = direction.magnitude;

            if (distance > 0.8f) targetSpeed = 1.0f; // 멀어지면 뛰기
            else if (distance > 0.1f) targetSpeed = 0.5f; // 가까우면 걷기
            else targetSpeed = 0f; // 멈춤
            
            currentAnimSpeed = Mathf.Lerp(currentAnimSpeed, targetSpeed, Time.deltaTime * 5f);
            anim.SetFloat("Speed", currentAnimSpeed);
        }
    }

    // ⭐️ 관제탑이 호출할 "쓰레기 버려!" 함수
    public void DoThrowTrash()
    {
        Debug.Log("🛑 사이드브레이크 작동!! 쓰레기 버리는 1.2초 동안 이동 금지!"); // <-- 이 줄 추가!
        StartCoroutine(ThrowRoutine());
    }

   private IEnumerator ThrowRoutine()
    {
        isThrowing = true; // 🛑 사이드브레이크 꽉 채움!
        Debug.Log("🛑 사이드브레이크 작동!");

        if (anim != null)
        {
            anim.SetFloat("Speed", 0f); // 걷는 동작 멈추기
            currentAnimSpeed = 0f;
            anim.SetTrigger("ThrowTrash"); // 투기 애니메이션 실행
        }

        // 1. 유니티가 애니메이션을 섞고 'Throwing' 상태로 완전히 넘어갈 때까지 넉넉히 0.5초 대기!
        // (이걸 안 기다려주면 코드가 냅다 브레이크를 풀어버립니다)
        yield return new WaitForSeconds(0.5f);

        // 2. 이제 확실히 'Throwing' 상태에 진입했으니, 애니메이션이 끝날 때까지 대기
        if (anim != null)
        {
            // 애니메이션 진행도(normalizedTime)가 95%가 될 때까지 무한 대기
            while (anim.GetCurrentAnimatorStateInfo(0).IsName("Throwing") && 
                   anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.95f)
            {
                yield return null; 
            }
        }
        isThrowing = false; // ✅ 애니메이션 완벽 종료! 사이드브레이크 해제!
        Debug.Log("✅ 사이드브레이크 해제! 맹추격 시작!");
    }
}