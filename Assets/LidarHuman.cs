using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // TMP 사용 필수

public class LidarHuman : MonoBehaviour
{
    [Header("Color & UI")]
    public List<Material> dummyMaterials;

    // ⭐️ 새 이름: humanIdText
    public TMP_Text humanIdText; 

    public Vector3 targetPosition;
    private Animator anim;
    private float currentAnimSpeed = 0f;
    private bool isThrowing = false; 
    private string myId = "Unknown"; 

    public void SetupId(int id)
    {
        myId = id.ToString();
        
        // ⭐️ 옛날 statusText 대신 전부 humanIdText로 통일됨!
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

        if (humanIdText != null && Camera.main != null)
        {
            // 캔버스가 항상 메인 카메라와 똑같은 방향을 보게 만듭니다!
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

        // ⭐️ 여기도 humanIdText로 깔끔하게 수정됨!
        if (humanIdText != null)
        {
            humanIdText.text = $"WARNING!!\nID: {myId}";
            humanIdText.color = Color.red;
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