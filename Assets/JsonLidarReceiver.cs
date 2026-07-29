using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class LidarObject { public int id; public float x; public float y; public string type; public float width; public bool is_abandoned; }
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
    public float timeoutSeconds = 2.0f; 
    
    [Header("ID 재연결 및 애니메이션 방어")]
    public float recycleRadius = 1.5f;
    // ⭐️ 쓰레기 버리는 애니메이션이 끝날 때까지 걸리는 시간 (에셋에 맞게 조절하세요!)
    public float throwAnimDuration = 2.5f; 

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning;
    private Queue<string> messageQueue = new Queue<string>();
    
    private Dictionary<int, GameObject> spawnedObjects = new Dictionary<int, GameObject>();
    private Dictionary<int, float> lastUpdateTimes = new Dictionary<int, float>();
    
    // ⭐️ [신규] 특정 ID의 애니메이션 종료 시간(무적 시간)을 기록하는 장부
    private Dictionary<int, float> throwAnimEndTime = new Dictionary<int, float>();

    void Start()
    {
        isRunning = true;
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log($"📡 [수신부] 가동 완료. 포트: {port}");
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
        } 
        catch (Exception e) 
        { 
            if (isRunning) Debug.LogError($"[UDP 수신 에러]: {e.Message}");
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

        CheckForTimeouts();
    }

    private void ProcessJson(string json)
    {
        try 
        {
            LidarPayload payload = JsonUtility.FromJson<LidarPayload>(json);

            if (payload.type == "FRAME")
            {
                // ⭐️ [신규] 현재 프레임에 살아있는 ID들만 모아둔 세트 (즉시 가로채기 판단용)
                HashSet<int> currentFrameIds = new HashSet<int>();
                if (payload.objects != null)
                {
                    foreach (var obj in payload.objects) currentFrameIds.Add(obj.id);
                }

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
                            // ⭐️ [신규] 새 ID가 뜨면, 죽었거나 애니메이션 중인데 증발한 객체를 찾습니다.
                            int recycledId = FindOrphanObjectNearby(targetPos, currentFrameIds);
                            
                            if (recycledId != -1 && obj.type == "normal")
                            {
                                GameObject recycledObj = spawnedObjects[recycledId];
                                spawnedObjects.Remove(recycledId);
                                lastUpdateTimes.Remove(recycledId);
                                
                                // 무적 시간(애니메이션)도 새 ID에게 그대로 물려줍니다!
                                if (throwAnimEndTime.ContainsKey(recycledId))
                                {
                                    throwAnimEndTime[obj.id] = throwAnimEndTime[recycledId];
                                    throwAnimEndTime.Remove(recycledId);
                                }
                                
                                spawnedObjects.Add(obj.id, recycledObj);
                                LidarHuman script = recycledObj.GetComponent<LidarHuman>();
                                if (script != null) script.SetupId(obj.id); 
                                
                                Debug.Log($"🔄 [애니메이션 이어받기] {recycledId}번 → {obj.id}번으로 인수인계 성공!");
                            }
                            else
                            {
                                GameObject prefabToSpawn = (obj.type == "normal") ? humanPrefab : trashPrefab;
                                Vector3 spawnPos = (obj.type == "normal") ? targetPos : new Vector3(targetPos.x, targetPos.y + 0.2f, targetPos.z);
                                
                                GameObject newObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
                                spawnedObjects.Add(obj.id, newObj);
                                
                                LidarHuman script = newObj.GetComponent<LidarHuman>();
                                if (script != null) script.SetupId(obj.id);
                            }
                        }
                        lastUpdateTimes[obj.id] = Time.time;
                    }
                }

                if (payload.events != null)
                {
                    foreach (var evt in payload.events)
                    {
                        if (evt.type == "dumping")
                        {
                            if (spawnedObjects.ContainsKey(evt.person_track_id))
                            {
                                LidarHuman human = spawnedObjects[evt.person_track_id].GetComponent<LidarHuman>();
                                if (human != null) human.DoThrowTrash();
                                
                                // ⭐️ [신규] 투기 애니메이션 시작! 지정된 시간 동안 이 ID에 무적 판정을 줍니다.
                                throwAnimEndTime[evt.person_track_id] = Time.time + throwAnimDuration;
                            }
                        }
                        else if (evt.type == "departure")
                        {
                            if (lastUpdateTimes.ContainsKey(evt.object_track_id))
                            {
                                lastUpdateTimes[evt.object_track_id] = Time.time - (timeoutSeconds * 0.8f);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[통신] JSON 파싱 에러: {e.Message}");
        }
    }

    // ⭐️ [수정됨] 애니메이션 중인 객체를 즉시 가로채는 로직 추가
    private int FindOrphanObjectNearby(Vector3 newPos, HashSet<int> currentFrameIds)
    {
        int bestOrphanId = -1;
        float minDist = recycleRadius;

        foreach (var kvp in spawnedObjects)
        {
            bool isOrphan = (Time.time - lastUpdateTimes[kvp.Key] > 0.5f);
            
            // 이 녀석이 지금 쓰레기 던지는 애니메이션 중인가?
            bool isThrowing = throwAnimEndTime.ContainsKey(kvp.Key) && Time.time < throwAnimEndTime[kvp.Key];
            // 애니메이션 중인데 이번 프레임 데이터에서 센서가 이 ID를 날려버렸는가?
            bool isMissingThisFrame = !currentFrameIds.Contains(kvp.Key);

            // 가로채기 조건: 0.5초 이상 끊겼거나 OR (지금 애니메이션 중인데 센서에서 ID가 증발한 경우 즉시!)
            if (isOrphan || (isThrowing && isMissingThisFrame))
            {
                float dist = Vector3.Distance(kvp.Value.transform.position, newPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestOrphanId = kvp.Key;
                }
            }
        }
        return bestOrphanId;
    }

    private void CheckForTimeouts()
    {
        var ghostIds = lastUpdateTimes.Where(kvp => Time.time - kvp.Value > timeoutSeconds).Select(kvp => kvp.Key).ToList();

        foreach (int id in ghostIds)
        {
            // ⭐️ [신규] 애니메이션 무적 시간이 아직 남아있다면 삭제(Destroy)를 건너뜁니다!
            if (throwAnimEndTime.ContainsKey(id) && Time.time < throwAnimEndTime[id])
            {
                continue; 
            }

            if (spawnedObjects.ContainsKey(id))
            {
                Destroy(spawnedObjects[id]);
                spawnedObjects.Remove(id);
            }
            lastUpdateTimes.Remove(id);
            throwAnimEndTime.Remove(id);
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (udpClient != null) udpClient.Close();
        if (receiveThread != null) receiveThread.Join(100); 
    }
}