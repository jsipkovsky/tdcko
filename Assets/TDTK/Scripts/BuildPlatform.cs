using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TDTK;

namespace TDTK{

	public class BuildPlatform : MonoBehaviour {
		
		public List<int> unavailablePrefabIDList=new List<int>();	//prefabID of the tower can't be built on this 
		
		[HideInInspector] public Vector2 size;
		
		[Header("Run time variable")] 
		public bool walkable=false;
		public List<Path> pathList=new List<Path>(); 	//all path that use the platform as waypoint
		public void AddPath(Path path){ pathList.Add(path); }

		//special effect assigned by PlatformEffectManager (0=none, 1..4=effect); desc shown on hover
		[HideInInspector] public int specialEffect=0;
		[HideInInspector] public string specialDesc="";
		private GameObject specialOverlay;

		public bool HasSpecial(){ return specialEffect!=0; }

		public void SetSpecial(int effect, string desc){
			specialEffect=effect;
			specialDesc=desc;
			ApplyOverlay(PlatformEffectColor(effect));
		}

		public void ClearSpecial(){
			specialEffect=0;
			specialDesc="";
			if(specialOverlay!=null) specialOverlay.SetActive(false);
		}

		private static Color PlatformEffectColor(int effect){
			switch(effect){
				case 1: return new Color(0.3f, 1f, 0.3f, 0.45f);	//cheaper/weaker - green
				case 2: return new Color(0.3f, 0.6f, 1f, 0.45f);	//skip then faster - blue
				case 3: return new Color(1f, 0.85f, 0.15f, 0.45f);	//pricier/more gold - yellow
				case 4: return new Color(0.75f, 0.4f, 1f, 0.45f);	//buff nearest - purple
				default: return new Color(1f, 1f, 1f, 0f);
			}
		}

		//the grid tile texture is mostly transparent, so a _Color tint is invisible;
		//drop a semi-transparent colored quad on top as the visible marker instead
		private void ApplyOverlay(Color c){
			if(specialOverlay==null){
				specialOverlay=GameObject.CreatePrimitive(PrimitiveType.Quad);
				specialOverlay.name="SpecialEffectOverlay";
				Collider col=specialOverlay.GetComponent<Collider>();
				if(col!=null) Destroy(col);
				Transform t=specialOverlay.transform;
				t.SetParent(transform, false);
				t.localPosition=new Vector3(0, 0, -0.03f);
				t.localRotation=Quaternion.identity;
				t.localScale=Vector3.one*0.95f;
				MeshRenderer mr=specialOverlay.GetComponent<MeshRenderer>();
				mr.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
				mr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
				mr.receiveShadows=false;
			}
			specialOverlay.SetActive(true);
			specialOverlay.GetComponent<MeshRenderer>().sharedMaterial.color=c;
		}
		
		[HideInInspector] public Transform thisT;
		
		void Awake(){
			gameObject.layer=TDTK.GetLayerPlatform();	//this should have been preassigned
			thisT=transform;
		}
		
		void Start(){
			TowerManager.FormatPlatform(this);
		}
		
		//[ContextMenu("Format")] IEnumerator Start(){
		//	yield return null;
		//	formatted=false; Format(TowerManager.GetGridSize(), true);
		//}
		
		public Vector3 GetPos(){ return thisT!=null ? thisT.position : transform.position; }
		public Quaternion GetRot(){ return thisT!=null ? thisT.rotation : transform.rotation; }
		
		private bool formatted=false;
		public void SetIsFormatted(bool aa)
        {
			formatted = aa;
		}
		public void Format(float gridSize=1, bool autoAdjustTextureToGrid=true){
			if(formatted) return;
			
			formatted=true;
			if(thisT==null) thisT=transform;
			
			//make sure the plane is perfectly horizontal, rotation around the y-axis is presreved
			thisT.eulerAngles=new Vector3(90, thisT.rotation.eulerAngles.y, 0);
			
			//adjusting the scale
			float scaleX=Mathf.Max(1, Mathf.Round(TDTK.GetWorldScale(thisT).x/gridSize))*gridSize;
			float scaleY=Mathf.Max(1, Mathf.Round(TDTK.GetWorldScale(thisT).y/gridSize))*gridSize;
			
			thisT.localScale=new Vector3(scaleX, scaleY, 1);
			
			//Vector2 
			size=new Vector2((int)(scaleX/gridSize), (int)(scaleY/gridSize));
			
			//adjusting the texture
			if(autoAdjustTextureToGrid){
				Material mat=thisT.GetComponent<Renderer>().material;
				
				float x=(TDTK.GetWorldScale(thisT).x)/gridSize;
				float y=(TDTK.GetWorldScale(thisT).y)/gridSize;
				
				mat.mainTextureOffset=new Vector2(0.5f, 0.5f);
				mat.mainTextureScale=new Vector2(x, y);
			}
			
			GenerateGraph(gridSize);
			
			//for(int i=0; i<nodeGraph.Length; i++){
			//	if(nodeGraph[i].IsBlockedForTower()) continue;
			//	Instantiate(tilePrefab, nodeGraph[i].GetPos(), thisT.rotation);
			//}
		}
		public void SingleNodePlatform(){
			nodeGraph=new NodeTD[1];
			nodeGraph[0]=new NodeTD(GetPos(), 0);
		}
		
		//[Space(8)]	public GameObject tilePrefab;
		
		
		public Vector3 GetTilePos(Vector3 hitPos, float gridSize=1){ return GetTilePos(this, hitPos, gridSize); }
		public static Vector3 GetTilePos(BuildPlatform platform, Vector3 hitPos, float gridSize=1){
			Vector3 v=hitPos-platform.thisT.position;	//get the vector from platform origin to hit point
			
			//transform the vector to the platform local space, so we know the (x, y)
			v=Quaternion.Euler(0, -platform.thisT.rotation.eulerAngles.y, 0) * v;	
			
			//check the size of the platform for odd/even columen and then set the offset in corresponding axis
			float osX=platform.size.x%2==0 ? gridSize/2 : 0;
			float osZ=platform.size.y%2==0 ? gridSize/2 : 0;
			
			//calculate the x and z position (this is the relative position in platform local space to the platform origin)
			float x=Mathf.Round((osX+v.x)/gridSize)*gridSize-osX;
			float z=Mathf.Round((osZ+v.z)/gridSize)*gridSize-osZ;
			
			//transform the calculated position to world space
			return platform.thisT.position+platform.thisT.TransformDirection(new Vector3(x, z, 0));
		}
		
		
		
		public void BuildTower(int nodeIdx, UnitTower tower=null, bool updatePath=true){
			nodeGraph[nodeIdx].SetTower(tower);
			if(updatePath) UpdatePath(tower);
		}
		public void RemoveTower(int nodeIdx, bool updatePath=true){
			nodeGraph[nodeIdx].ClearTower();
			if(updatePath) UpdatePath();
		}
		
		//test function, no longer in use
		/*
		public void BlockNode(int nodeIdx){
			nodeGraph[nodeIdx].SetWalkable(false);
			UpdatePath();
		}
		public void UnblockNode(int nodeIdx){
			nodeGraph[nodeIdx].SetWalkable(true);
			UpdatePath();
		}
		*/
		
		
		public bool CheckForNode(int nodeIdx){
			if(!walkable) return true;
			
			List<Path> blockedPathList=new List<Path>();
			
			for(int i=0; i<pathList.Count; i++){
				if(!pathList[i].CheckForNode(this, nodeIdx)) blockedPathList.Add(pathList[i]);
			}
			
			for(int i=0; i<blockedPathList.Count; i++){
				if(!blockedPathList[i].CheckForNodeAltPath(this, nodeIdx, blockedPathList)) return false;
			}
			
			return true;
		}
		
		
		public void UpdatePath(UnitTower tower=null){
			if(walkable) StartCoroutine(_UpdatePath(tower));
		}
		IEnumerator _UpdatePath(UnitTower tower=null){
			yield return null;
			for(int i=0; i<pathList.Count; i++) pathList[i].UpdatePlatformPath(this, tower);
			Path.UpdateDistToEndOnAllPath();
		}
		
		
		
		//the graph-node covering this platform
		private NodeTD[] nodeGraph;
		public NodeTD[] GetNodeGraph(){ return nodeGraph; }
		public void OverrideNodeGraph(NodeTD[] newGraph){ nodeGraph=newGraph; }	//for extension
		
		public NodeTD GetNode(int idx){ return nodeGraph[idx]; }
		
		public void GenerateGraph(float gridSize){ nodeGraph=AStar.GenerateNode(this, gridSize); }
		
		//searchMode:  0-any node, 1-walkable only, 2-unwalkable only
		public NodeTD GetNearestNode(Vector3 point, int searchMode=0){ 
			NodeTD node=AStar.GetNearestNode(point, nodeGraph, searchMode);
			if(node==null){
				Debug.Log(nodeGraph.Length+"    node is null");
				return nodeGraph[0];
			}
			return node;
		}
		
		
		public List<Vector3> searchPath(int entryID, int exitID, int blockIdx=-1, bool smooth=true){
			List<Vector3> list=AStar.Search(nodeGraph[entryID], nodeGraph[exitID], nodeGraph, blockIdx>=0 ? nodeGraph[blockIdx] : null, smooth);
			AStar.ResetGraph(nodeGraph);
			return list;
		}
		
		
		
		[Space(10)] public bool GizmoShowNodes=true;
		void OnDrawGizmos(){
			if(GizmoShowNodes){
				if(nodeGraph!=null && nodeGraph.Length>0){
					foreach(NodeTD node in nodeGraph){
						if(node.IsBlocked()){
							Gizmos.color=Color.red;
							Gizmos.DrawSphere(node.pos, .15f);
						}
						else if(node.IsBlockedForTower()){
							Gizmos.color=new Color(.5f, .5f, .5f, 1f);
							Gizmos.DrawSphere(node.pos, .15f);
						}
						else{
							Gizmos.color=Color.white;
							Gizmos.DrawSphere(node.pos, .15f);
						}
					}
				}
			}
		}
		
	}

}