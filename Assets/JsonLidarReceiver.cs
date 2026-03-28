using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

// 1. 임베디드 담당자의 JSON 규격과 100% 일치하는 데이터 구조체 (포장지)
[Serializable]
public class LidarObject
{
    public int id;
    public float x;
    public float y;
    public float width;
    public bool is_abandoned;
}

[Serializable]
public class LidarPayload
{
    public string type;           // "FRAME", "ALERT", "INFO", "LOST"
    public ulong timestamp;
    public List<LidarObject> objects; // FRAME용 배열
    
    // 이벤트(ALERT, INFO, LOST) 전용 필드들
    public int person_id;
    public float person_x;
    public float person_y;
    public int object_id;
    public float object_x;
    public float object_y;
}

// 2. 메인 수신부 클래스
public class JsonLidarReceiver : MonoBehaviour
{
    [Header("통신 설정")]
    public int port = 5005;

    [Header("시각화 에셋")]
    public GameObject humanPrefab;
    public GameObject trashPrefab;

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning;
    private Queue<string> messageQueue = new Queue<string>();

    // 화면에 띄워둔 3D 모델들을 ID별로 관리하는 장부
    private Dictionary<int, GameObject> spawnedObjects = new Dictionary<int, GameObject>();

    void Start()
    {
        isRunning = true;
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log("📡 [시스템] 고급 JSON 라이다 수신부 가동 완료! 포트: " + port);
    }

    private void ReceiveData()
    {
        udpClient = new UdpClient(port);
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref anyIP);
                string json = Encoding.UTF8.GetString(data);
                
                lock (messageQueue) { messageQueue.Enqueue(json); }
            }
            catch { /* 종료 시 에러 무시 */ }
        }
    }

    void Update()
    {
        while (messageQueue.Count > 0)
        {
            string json;
            lock (messageQueue) { json = messageQueue.Dequeue(); }
            ProcessJson(json);
        }
    }

    // 3. ⭐️ 핵심 로직: 들어온 JSON 타입에 따라 행동 결정
    private void ProcessJson(string json)
    {
        // 유니티 내장 JSON 파서로 텍스트를 C# 객체로 변환
        LidarPayload payload = JsonUtility.FromJson<LidarPayload>(json);

        switch (payload.type)
        {
            case "FRAME":
                // 100ms마다 들어오는 전체 객체들의 위치 갱신
                foreach (var obj in payload.objects)
                {
                    // [좌표 변환] mm를 m로 나누기(/1000f), 라이다 Y축을 유니티 Z축으로 매핑!
                    Vector3 targetPos = new Vector3(obj.x / 1000f, 0.5f, obj.y / 1000f);

                    if (spawnedObjects.ContainsKey(obj.id))
                    {
                        // [수정된 부분] 직접 Lerp하지 않고, LidarHuman 스크립트의 '목표 좌표'만 갱신!
                        LidarHuman human = spawnedObjects[obj.id].GetComponent<LidarHuman>();
                        if (human != null)
                        {
                            human.targetPosition = targetPos;
                            
                        }
                    }
                    else
                    {
                        // 새로운 객체면 일단 사람 더미로 소환
                        GameObject newObj = Instantiate(humanPrefab, targetPos, Quaternion.identity);
                        spawnedObjects.Add(obj.id, newObj);

                        LidarHuman script = newObj.GetComponent<LidarHuman>();
                        if (script != null)
                        {
                             script.SetupId(obj.id); // 소환 직후 ID 번호표 달아주기!
                        }
                        
                    }
                }
                break;

            case "INFO":
                // 투기 의심 상태 (시각화 전, 콘솔로만 확인)
                Debug.Log($"👀 [관제] {payload.person_id}번 사람이 {payload.object_id}번 물체를 떨어뜨린 것 같습니다. 3초 검증 시작...");
                break;

            case "ALERT":
                Vector3 trashPos = new Vector3(payload.object_x / 1000f, 0.5f, payload.object_y / 1000f);
                GameObject trash = Instantiate(trashPrefab, trashPos, Quaternion.identity);
                spawnedObjects[payload.object_id] = trash; 

                // 👇👇👇 수정된 부분 👇👇👇
                if (spawnedObjects.ContainsKey(payload.person_id))
                {
                    // 아까는 Animator를 찾았지만, 이제는 LidarHuman 스크립트를 찾습니다.
                    LidarHuman human = spawnedObjects[payload.person_id].GetComponent<LidarHuman>();
                    if (human != null)
                    {
                        human.DoThrowTrash(); // 멈추고 버리는 풀코스 지시!
                    }
                }
                // 👆👆👆 ---------------- 👆👆👆

                Debug.Log($"🚨 [경보] 무단 투기 확정!!");
                break;

            case "LOST":
                // 사람이 시야에서 벗어났거나, 쓰레기가 회수되었을 때 깔끔하게 지워줌
                if (spawnedObjects.ContainsKey(payload.object_id))
                {
                    Destroy(spawnedObjects[payload.object_id]);
                    spawnedObjects.Remove(payload.object_id);
                    Debug.Log($"🧹 [정리] {payload.object_id}번 객체가 시야에서 사라져 삭제했습니다.");
                }
                break;
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (receiveThread != null) receiveThread.Abort();
        if (udpClient != null) udpClient.Close();
    }
}