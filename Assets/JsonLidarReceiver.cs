using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq; // 💡 ToList() 사용을 위해 추가

// (LidarObject, LidarEvent, LidarPayload 구조체는 기존과 100% 동일하므로 생략하지 않고 모두 넣었습니다)
[Serializable]
public class LidarObject { public int id; public float x; public float y; public float width; public bool is_abandoned; }
[Serializable]
public class LidarEvent { public string type; public int person_track_id; public float person_x; public float person_y; public int object_track_id; public float object_x; public float object_y; public ulong timestamp; }
[Serializable]
public class LidarPayload { public string type; public ulong timestamp; public List<LidarObject> objects; public List<LidarEvent> events; }

public class JsonLidarReceiver : MonoBehaviour
{
    [Header("통신 설정")]
    public int port = 5005;

    [Header("시각화 에셋")]
    public GameObject humanPrefab;
    public GameObject trashPrefab;
    public Transform sensorOrigin;

    [Header("스케일 및 타임아웃")]
    [Range(0.1f, 10f)]
    public float scaleMultiplier = 2.0f;
    
    // ⭐️ [신규 기능] 몇 초 동안 신호가 없으면 유령으로 간주하고 삭제할 것인가? (기본 1초)
    public float timeoutSeconds = 1.0f; 

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning;
    private Queue<string> messageQueue = new Queue<string>();
    
    private Dictionary<int, GameObject> spawnedObjects = new Dictionary<int, GameObject>();
    
    // ⭐️ [신규 기능] 객체별 '마지막 생존 신고 시간'을 기록하는 장부
    private Dictionary<int, float> lastUpdateTimes = new Dictionary<int, float>();

    void Start()
    {
        isRunning = true;
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void ReceiveData()
    {
        try {
            udpClient = new UdpClient(port);
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

            while (isRunning)
            {
                byte[] data = udpClient.Receive(ref anyIP);
                string json = Encoding.UTF8.GetString(data);
                lock (messageQueue) { messageQueue.Enqueue(json); }
            }
        } catch (Exception) { /* 종료 시 에러 무시 */ }
    }

    void Update()
    {
        // 1. 수신된 JSON 데이터 처리
        while (messageQueue.Count > 0)
        {
            string json;
            lock (messageQueue) { json = messageQueue.Dequeue(); }
            ProcessJson(json);
        }

        // 2. ⭐️ [자동 청소 로직] 매 프레임마다 유령 객체를 검사하고 삭제합니다.
        CheckForTimeouts();
    }

    private void ProcessJson(string json)
    {
        LidarPayload payload = JsonUtility.FromJson<LidarPayload>(json);

        if (payload.type == "FRAME")
        {
            if (payload.objects != null)
            {
                foreach (var obj in payload.objects)
                {
                    Vector3 localTargetPos = new Vector3((obj.x / 1000f) * scaleMultiplier, 0.3f, (obj.y / 1000f) * scaleMultiplier);
                    Vector3 targetPos = sensorOrigin.TransformPoint(localTargetPos);

                    if (spawnedObjects.ContainsKey(obj.id))
                    {
                        LidarHuman human = spawnedObjects[obj.id].GetComponent<LidarHuman>();
                        if (human != null) human.targetPosition = targetPos;
                    }
                    else
                    {
                        GameObject newObj = Instantiate(humanPrefab, targetPos, Quaternion.identity);
                        spawnedObjects.Add(obj.id, newObj);
                        LidarHuman script = newObj.GetComponent<LidarHuman>();
                        if (script != null) script.SetupId(obj.id);
                    }

                    // ⭐️ [생존 신고] 좌표가 들어올 때마다 해당 ID의 생존 시간을 '현재 시간'으로 갱신!
                    lastUpdateTimes[obj.id] = Time.time;
                }
            }

            // 이벤트 처리 (투기는 그대로 두고, 명시적 departure도 혹시 모르니 남겨둠)
            if (payload.events != null)
            {
                foreach (var evt in payload.events)
                {
                    if (evt.type == "dumping")
                    {
                        Vector3 localTrashPos = new Vector3((evt.object_x / 1000f) * scaleMultiplier, 0.5f, (evt.object_y / 1000f) * scaleMultiplier);
                        Vector3 trashPos = sensorOrigin.TransformPoint(localTrashPos);
                        GameObject trash = Instantiate(trashPrefab, trashPos, Quaternion.identity);
                        
                        spawnedObjects[evt.object_track_id] = trash;
                        // 쓰레기도 자동 청소되지 않도록 생존 시간 기록 (단, 쓰레기는 영구 보존하려면 타임아웃 예외 처리 가능)
                        lastUpdateTimes[evt.object_track_id] = Time.time + 99999f; 

                        if (spawnedObjects.ContainsKey(evt.person_track_id))
                        {
                            LidarHuman human = spawnedObjects[evt.person_track_id].GetComponent<LidarHuman>();
                            if (human != null) human.DoThrowTrash();
                        }
                    }
                    else if (evt.type == "departure")
                    {
                        RemoveObject(evt.object_track_id);
                    }
                }
            }
        }
    }

    // ⭐️ [신규 기능] 타임아웃 검사 함수
    private void CheckForTimeouts()
    {
        // 현재 시간(Time.time)에서 마지막 갱신 시간을 뺐을 때, timeoutSeconds(1초)를 넘긴 녀석들 색출
        var ghostIds = lastUpdateTimes.Where(kvp => Time.time - kvp.Value > timeoutSeconds)
                                      .Select(kvp => kvp.Key)
                                      .ToList();

        foreach (int id in ghostIds)
        {
            RemoveObject(id);
        }
    }

    // ⭐️ [신규 기능] 삭제 로직을 한 곳으로 통합
    private void RemoveObject(int id)
    {
        if (spawnedObjects.ContainsKey(id))
        {
            Destroy(spawnedObjects[id]);
            spawnedObjects.Remove(id);
        }
        if (lastUpdateTimes.ContainsKey(id))
        {
            lastUpdateTimes.Remove(id);
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (udpClient != null) udpClient.Close();
        if (receiveThread != null) receiveThread.Join(100); 
    }
}