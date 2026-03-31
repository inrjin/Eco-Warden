using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; 

public class LidarHuman : MonoBehaviour
{
    [Header("Color & UI")]
    public List<Material> dummyMaterials;
    public TMP_Text humanIdText; 

    public Vector3 targetPosition;
    private Animator anim;
    private TrailRenderer trail; // ⭐️ 추가: 궤적 그리는 녀석
    
    private float currentAnimSpeed = 0f;
    private bool isThrowing = false; 
    private string myId = "Unknown"; 

    public void SetupId(int id)
    {
        myId = id.ToString();
        
        if (humanIdText != null)
        {
            humanIdText.text = $"ID: {myId}";
            humanIdText.color = Color.green; 
        }
    }

    void Start()
    {
        targetPosition = transform.position;
        anim = GetComponent<Animator>();
        
        // ⭐️ 시작할 때 Trail Renderer를 찾아서 연결해 둠
        trail = GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.emitting = false; // 처음엔 무조건 선 그리기 끄기!
            trail.time = 10f; // 10초 동안 선이 바닥에 남아있게 설정
        }

        if (dummyMaterials != null && dummyMaterials.Count > 0)
        {
            SkinnedMeshRenderer renderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                int randomIndex = Random.Range(0, dummyMaterials.Count);
                renderer.material = dummyMaterials[randomIndex];
            }
        }
    }

    void Update()
    {
        if (isThrowing) return;

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

            if (distance > 0.8f) targetSpeed = 1.0f; 
            else if (distance > 0.1f) targetSpeed = 0.5f; 
            else targetSpeed = 0f; 
            
            currentAnimSpeed = Mathf.Lerp(currentAnimSpeed, targetSpeed, Time.deltaTime * 5f);
            anim.SetFloat("Speed", currentAnimSpeed);
        }

        // 텍스트가 항상 카메라(관제탑)를 바라보게 만들기! (해바라기 모드)
        if (humanIdText != null && Camera.main != null)
        {
            humanIdText.transform.parent.rotation = Camera.main.transform.rotation;
        }
    }

    public void DoThrowTrash()
    {
        StartCoroutine(ThrowRoutine());
    }

    private IEnumerator ThrowRoutine()
    {
        isThrowing = true; 

        if (humanIdText != null)
        {
            humanIdText.text = $"🚨 WARNING!\nID: {myId}";
            humanIdText.color = Color.red;
        }

        // ⭐️ 핵심: 범행 순간부터 도망가는 경로 추적 시작!!
        if (trail != null)
        {
            trail.emitting = true; 
            trail.startColor = Color.red; // 빨간색 궤적으로 변경
            trail.endColor = new Color(1f, 0f, 0f, 0f); // 꼬리표는 투명하게 사라지도록
        }

        if (anim != null)
        {
            anim.SetFloat("Speed", 0f); 
            currentAnimSpeed = 0f;
            anim.SetTrigger("ThrowTrash"); 
        }

        yield return new WaitForSeconds(0.5f);

        if (anim != null)
        {
            while (anim.GetCurrentAnimatorStateInfo(0).IsName("Throwing") && 
                   anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.95f)
            {
                yield return null; 
            }
        }

        isThrowing = false; 
    }
}