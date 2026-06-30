using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/*
 * [Mesh Slicer Tool] 5/ 29
 * 통짜 메쉬에서 특정 영역의 정점을 선택해 서브 메쉬를 추출하고,
 * 잘려 나간 단면(Open Edge)의 구멍을 자동으로 메워주는 에디터 확장 툴입니다.
*/
public class MeshSlicerTool : EditorWindow
{
    private GameObject targetObject;
    private float selectionRadius = 1.0f;
    private Vector3 selectionCenter = Vector3.zero;

 
    private struct Edge
    {
        public int v1;
        public int v2;

        public Edge(int a, int b)
        {
            v1 = Mathf.Min(a, b);
            v2 = Mathf.Max(a, b);
        }
    }


    [MenuItem("Tools/Mesh Slicing Tool")]
    public static void ShowWindow()
    {
        GetWindow<MeshSlicerTool>("Mesh Slicer");
    }


    private void OnGUI()
    {
        GUILayout.Label("Mesh Slicing Tool", EditorStyles.boldLabel);

        targetObject = (GameObject)EditorGUILayout.ObjectField("대상 오브젝트", targetObject, typeof(GameObject), true);
        selectionRadius = EditorGUILayout.FloatField("선택 반경 (Sphere)", selectionRadius);
        selectionCenter = EditorGUILayout.Vector3Field("선택 중심점", selectionCenter);

        if (GUILayout.Button("선택 영역 메쉬 분리 및 구멍 메우기") && targetObject != null)
        {
            ExecuteSlicing();
        }
    }


    private void ExecuteSlicing()
    {
        Mesh originalMesh = null;
        MeshFilter filter = targetObject.GetComponent<MeshFilter>();
        SkinnedMeshRenderer smr = targetObject.GetComponent<SkinnedMeshRenderer>();

        if (filter != null) originalMesh = filter.sharedMesh;
        else if (smr != null) originalMesh = smr.sharedMesh;

        if (originalMesh == null)
        {
            Debug.LogError("대상 오브젝트에 Mesh 데이터가 존재하지 않습니다!");
            return;
        }

        Vector3[] vertices = originalMesh.vertices;
        List<int> selectedIndices = new List<int>();


        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = targetObject.transform.TransformPoint(vertices[i]);
            if (Vector3.Distance(worldPos, selectionCenter) <= selectionRadius)
            {
                selectedIndices.Add(i);
            }
        }

        if (selectedIndices.Count < 3)
        {
            Debug.LogWarning("선택된 영역 내 정점이 너무 적습니다 (최소 3개 이상 필요).");
            return;
        }


        Mesh newMesh = ExtractAndCapMesh(originalMesh, selectedIndices);

        if (newMesh != null)
        {

            GameObject newObj = new GameObject(targetObject.name + "_Ex");
            newObj.transform.position = targetObject.transform.position;
            newObj.transform.rotation = targetObject.transform.rotation;
            newObj.transform.localScale = targetObject.transform.localScale;

            if (smr != null)
            {
                SkinnedMeshRenderer newSmr = newObj.AddComponent<SkinnedMeshRenderer>();
                newSmr.sharedMesh = newMesh;
                newSmr.materials = smr.sharedMaterials;
                newSmr.bones = smr.bones;
                newSmr.rootBone = smr.rootBone;
            }
            else
            {
                MeshFilter newFilter = newObj.AddComponent<MeshFilter>();
                newFilter.sharedMesh = newMesh;
                MeshRenderer newRenderer = newObj.AddComponent<MeshRenderer>();
                newRenderer.material = targetObject.GetComponent<MeshRenderer>().sharedMaterial;
            }


            string timeStamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
            AssetDatabase.CreateAsset(newMesh, "Assets/" + newMesh.name + "_" + timeStamp + ".asset");
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = newObj;
            Debug.Log("메쉬 분리 및 구멍 메우기 처리가 완료되었습니다. (Assets/ 폴더 내 파일 저장됨)");
        }
    }


    private Mesh ExtractAndCapMesh(Mesh sourceMesh, List<int> selectedIndices)
    {

        Vector3[] srcVertices = sourceMesh.vertices;
        Vector3[] srcNormals = sourceMesh.normals;
        Vector2[] srcUVs = sourceMesh.uv;
        BoneWeight[] srcBoneWeights = sourceMesh.boneWeights;
        int[] srcTriangles = sourceMesh.triangles;


        List<Vector3> newVertices = new List<Vector3>(srcVertices.Length);
        List<Vector3> newNormals = new List<Vector3>(srcNormals.Length);
        List<Vector2> newUVs = new List<Vector2>(srcUVs.Length);
        List<BoneWeight> newBoneWeights = new List<BoneWeight>(srcBoneWeights.Length);
        List<int> newTriangles = new List<int>();


        Dictionary<int, int> indexMap = new Dictionary<int, int>();

        for (int i = 0; i < selectedIndices.Count; i++)
        {
            int oldIdx = selectedIndices[i];
            indexMap[oldIdx] = i;

            newVertices.Add(srcVertices[oldIdx]);
            if (srcNormals.Length > 0) newNormals.Add(srcNormals[oldIdx]);
            if (srcUVs.Length > 0) newUVs.Add(srcUVs[oldIdx]);
            if (srcBoneWeights.Length > 0) newBoneWeights.Add(srcBoneWeights[oldIdx]);
        }


        Dictionary<Edge, int> edgeCount = new Dictionary<Edge, int>();


        for (int i = 0; i < srcTriangles.Length; i += 3)
        {
            int t1 = srcTriangles[i];
            int t2 = srcTriangles[i + 1];
            int t3 = srcTriangles[i + 2];

            if (indexMap.ContainsKey(t1) && indexMap.ContainsKey(t2) && indexMap.ContainsKey(t3))
            {
                int nT1 = indexMap[t1];
                int nT2 = indexMap[t2];
                int nT3 = indexMap[t3];

                newTriangles.Add(nT1);
                newTriangles.Add(nT2);
                newTriangles.Add(nT3);


                AddEdge(edgeCount, new Edge(nT1, nT2));
                AddEdge(edgeCount, new Edge(nT2, nT3));
                AddEdge(edgeCount, new Edge(nT3, nT1));
            }
        }


        Dictionary<int, int> directedEdges = new Dictionary<int, int>();


        for (int i = 0; i < srcTriangles.Length; i += 3)
        {
            if (indexMap.ContainsKey(srcTriangles[i]) && indexMap.ContainsKey(srcTriangles[i + 1]) && indexMap.ContainsKey(srcTriangles[i + 2]))
            {
                int n1 = indexMap[srcTriangles[i]];
                int n2 = indexMap[srcTriangles[i + 1]];
                int n3 = indexMap[srcTriangles[i + 2]];

                if (edgeCount[new Edge(n1, n2)] == 1) directedEdges[n1] = n2;
                if (edgeCount[new Edge(n2, n3)] == 1) directedEdges[n2] = n3;
                if (edgeCount[new Edge(n3, n1)] == 1) directedEdges[n3] = n1;
            }
        }


        HashSet<int> visited = new HashSet<int>();

        foreach (var startNode in directedEdges.Keys)
        {
            if (visited.Contains(startNode)) continue;


            List<int> loopVertices = new List<int>();
            int curr = startNode;

            while (curr != -1 && !visited.Contains(curr))
            {
                visited.Add(curr);
                loopVertices.Add(curr);

                if (directedEdges.TryGetValue(curr, out int next))
                    curr = next;
                else
                    curr = -1;

                if (curr == startNode) break;
            }


            if (loopVertices.Count >= 3)
            {

                Vector3 centerPos = Vector3.zero;
                Vector3 centerNormal = Vector3.zero;

                foreach (int idx in loopVertices)
                {
                    centerPos += newVertices[idx];
                    if (newNormals.Count > 0) centerNormal += newNormals[idx];
                }
                centerPos /= loopVertices.Count;
                centerNormal = centerNormal.magnitude > 0 ? centerNormal.normalized : Vector3.up;


                int centerIdx = newVertices.Count;
                newVertices.Add(centerPos);
                newNormals.Add(-centerNormal);
                newUVs.Add(new Vector2(0.5f, 0.5f));

                if (newBoneWeights.Count > 0)
                    newBoneWeights.Add(newBoneWeights[loopVertices[0]]);


                for (int i = 0; i < loopVertices.Count; i++)
                {
                    int vCurrent = loopVertices[i];
                    int vNext = loopVertices[(i + 1) % loopVertices.Count];

                    newTriangles.Add(vNext);
                    newTriangles.Add(vCurrent);
                    newTriangles.Add(centerIdx);
                }
            }
        }


        Mesh generatedMesh = new Mesh();
        generatedMesh.name = "Extracted_Mesh";
        generatedMesh.vertices = newVertices.ToArray();
        generatedMesh.triangles = newTriangles.ToArray();

        //노말맵 및 UV, 본 웨이트 정보가 존재할 경우에만 설정
        if (newNormals.Count > 0) generatedMesh.normals = newNormals.ToArray();
        if (newUVs.Count > 0) generatedMesh.uv = newUVs.ToArray();
        if (newBoneWeights.Count > 0) generatedMesh.boneWeights = newBoneWeights.ToArray();
        if (sourceMesh.bindposes.Length > 0) generatedMesh.bindposes = sourceMesh.bindposes;

        // 단면 셰이더 왜곡 및 라이팅 깨짐 방지를 위한 유니티 내장 최적화 함수
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();
        generatedMesh.RecalculateTangents();

        return generatedMesh;
    }


    private void AddEdge(Dictionary<Edge, int> dict, Edge edge)
    {
        if (dict.ContainsKey(edge)) dict[edge]++;

        else dict[edge] = 1;
    }
}