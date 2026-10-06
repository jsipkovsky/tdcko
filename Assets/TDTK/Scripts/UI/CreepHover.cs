using System.Collections.Generic;
using UnityEngine;

namespace TDTK{

	// Shows a small popup with a creep's stats (name/HP/shield) or a tower's stats (name/damage/range/cooldown)
	// while the mouse hovers over it. Hovering a creep (or its movement ghost) highlights the pair (ghost recolors
	// and both grow slightly), so the creep<->ghost relationship reads clearly from either end.
	// Self-bootstraps at scene load so no manual scene wiring is required.
	public class CreepHover : MonoBehaviour {

		private UnitCreep hovered;
		private UnitCreep hoveredGhostCreep;
		private UnitTower hoveredTower;
		private BuildPlatform hoveredPlatform;
		private UnitCreep highlighted;
		private GUIStyle boxStyle;
		private GUIStyle hpStyle;

		// int->string cache so HP numbers don't allocate a new string every frame
		private static readonly Dictionary<int, string> hpStringCache = new Dictionary<int, string>();
		private static readonly GUIContent sharedContent = new GUIContent();

		private static readonly Color CreepHPColor = new Color(1f, 0.6f, 0.6f);

		// how close (world units) the mouse ray must pass to a creep's body to count as a hover
		private const float CreepHoverRadius = 0.55f;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		static void Bootstrap(){
			if(FindObjectOfType<CreepHover>()!=null) return;
			GameObject obj=new GameObject("_CreepHover");
			obj.AddComponent<CreepHover>();
		}

		void Update(){
			hovered=null;
			hoveredGhostCreep=null;
			hoveredTower=null;
			hoveredPlatform=null;

			Camera cam=Camera.main;
			if(cam==null){ SetHighlight(null); return; }

			Ray ray=cam.ScreenPointToRay(Input.mousePosition);
			RaycastHit hit;

			// creep hover is proximity-based: the creep's physics collider is a small sphere at its
			// feet, so raycasting the body misses. Instead pick the creep whose body center lies
			// closest to the mouse ray (nearest-to-camera on ties).
			hovered=FindCreepUnderRay(ray, cam);

			if(hovered==null){
				int ghostMask=1<<2;	//ghosts sit on the Ignore Raycast layer; reachable only via explicit mask
				if(Physics.Raycast(ray, out hit, Mathf.Infinity, ghostMask)){
					GhostRef gr=hit.collider.GetComponentInParent<GhostRef>();
					if(gr!=null && gr.creep!=null && !gr.creep.IsDestroyed() && gr.creep.hp>0) hoveredGhostCreep=gr.creep;
				}
			}

			if(hovered==null && hoveredGhostCreep==null){
				int towerMask=1<<TDTK.GetLayerTower();
				if(Physics.Raycast(ray, out hit, Mathf.Infinity, towerMask)){
					UnitTower tower=hit.collider.GetComponentInParent<UnitTower>();
					if(tower!=null && !tower.IsDestroyed() && !tower.IsPreview() && !tower.InConstruction()) hoveredTower=tower;
				}
			}

			// a special build platform shows its effect description when nothing else is hovered
			if(hovered==null && hoveredGhostCreep==null && hoveredTower==null){
				int platformMask=1<<TDTK.GetLayerPlatform();
				if(Physics.Raycast(ray, out hit, Mathf.Infinity, platformMask)){
					BuildPlatform pl=hit.collider.GetComponentInParent<BuildPlatform>();
					if(pl!=null && pl.HasSpecial()) hoveredPlatform=pl;
				}
			}

			SetHighlight(hovered!=null ? hovered : hoveredGhostCreep);
		}

		// returns the active creep whose body center is within CreepHoverRadius of the mouse ray,
		// choosing the one nearest the camera when several overlap
		private UnitCreep FindCreepUnderRay(Ray ray, Camera cam){
			UnitCreep best=null;
			float bestAlong=Mathf.Infinity;

			List<Unit> list=SpawnManager.GetActiveUnitList();
			for(int i=0; i<list.Count; i++){
				if(list[i]==null) continue;
				UnitCreep creep=list[i].GetCreep();
				if(creep==null || creep.IsDestroyed() || creep.hp<=0) continue;

				Vector3 center=creep.GetTargetPoint();
				Vector3 v=center-ray.origin;
				float along=Vector3.Dot(v, ray.direction);
				if(along<0) continue;	//behind the camera

				Vector3 closest=ray.origin+ray.direction*along;
				if(Vector3.Distance(closest, center)>CreepHoverRadius) continue;

				if(along<bestAlong){ bestAlong=along; best=creep; }
			}

			return best;
		}

		// highlight the active pair; clear the previous one when the hover target changes
		private void SetHighlight(UnitCreep c){
			if(highlighted!=null && highlighted!=c) highlighted.SetHovered(false);
			highlighted=c;
			if(highlighted!=null) highlighted.SetHovered(true);	//reapplied each frame so rebuilt ghosts stay lit
		}

		void OnGUI(){
			if(Event.current.type!=EventType.Repaint) return;	//fixed-rect IMGUI needs no layout pass; skip all other events

			DrawUnitHPNumbers();

			if(hovered!=null) DrawCreepBox(hovered);
			else if(hoveredGhostCreep!=null) DrawCreepBox(hoveredGhostCreep);
			else if(hoveredTower!=null) DrawTowerBox();
			else if(hoveredPlatform!=null) DrawPlatformBox();
		}

		// small always-on HP number floating above every creep so the player can gauge unit strength
		private void DrawUnitHPNumbers(){
			Camera cam=Camera.main;
			if(cam==null) return;

			if(hpStyle==null){
				hpStyle=new GUIStyle(GUI.skin.label);
				hpStyle.alignment=TextAnchor.MiddleCenter;
				hpStyle.fontStyle=FontStyle.Bold;
			}

			List<Unit> creeps=SpawnManager.GetActiveUnitList();
			for(int i=0; i<creeps.Count; i++) DrawHPNumber(cam, creeps[i]);
		}

		private void DrawHPNumber(Camera cam, Unit unit){
			if(unit==null || unit.IsDestroyed()) return;

			Vector3 screenPos=cam.WorldToScreenPoint(unit.GetTargetPoint()+Vector3.up*0.4f);
			if(screenPos.z<=0) return;	//behind the camera

			hpStyle.normal.textColor=CreepHPColor;

			float w=48, h=18;
			float x=screenPos.x-w*0.5f;
			float y=(Screen.height-screenPos.y)-h;
			sharedContent.text=GetCachedIntString(Mathf.CeilToInt(unit.GetHP()));
			GUI.Label(new Rect(x, y, w, h), sharedContent, hpStyle);
		}

		private static string GetCachedIntString(int value){
			string s;
			if(!hpStringCache.TryGetValue(value, out s)){
				if(hpStringCache.Count>4096) hpStringCache.Clear();	//safety cap
				s=value.ToString();
				hpStringCache.Add(value, s);
			}
			return s;
		}

		private void EnsureStyle(){
			if(boxStyle==null){
				boxStyle=new GUIStyle(GUI.skin.box);
				boxStyle.alignment=TextAnchor.UpperLeft;
				boxStyle.padding=new RectOffset(8, 8, 6, 6);
				boxStyle.normal.textColor=Color.white;
				boxStyle.wordWrap=true;	//long effect descriptions wrap to new lines instead of being clipped
			}
		}

		private void DrawCreepBox(UnitCreep creep){
			EnsureStyle();

			bool hasShield=creep.GetFullSH()>0;

			string text=creep.unitName
				+ "\nHP: " + Mathf.CeilToInt(creep.hp) + " / " + Mathf.CeilToInt(creep.GetFullHP());
			if(hasShield){
				text += "\nShield: " + Mathf.CeilToInt(creep.sh) + " / " + Mathf.CeilToInt(creep.GetFullSH());
			}

			DrawBox(text, 180);
		}

		private void DrawTowerBox(){
			EnsureStyle();

			bool hasDamage=hoveredTower.GetDamageMax()>0;

			string text=hoveredTower.unitName;
			if(hasDamage){
				text += "\nDamage: " + Mathf.RoundToInt(hoveredTower.GetDamageMin()) + "-" + Mathf.RoundToInt(hoveredTower.GetDamageMax());
				text += "\nRange: " + hoveredTower.GetAttackRange().ToString("f1");
				text += "\nCooldown: " + hoveredTower.GetCooldown().ToString("f1") + "s";
			}

			float w=180;

			if(hoveredTower.HasPlatformEffect() && !string.IsNullOrEmpty(hoveredTower.platformEffectDesc)){
				text += "\n\n" + hoveredTower.platformEffectDesc;
				w=260;
			}

			DrawBox(text, w);
		}

		private void DrawPlatformBox(){
			EnsureStyle();
			DrawBox(hoveredPlatform.specialDesc, 260);
		}

		private void DrawBox(string text, float w){
			// height grows to fit the wrapped text at the given width so nothing is clipped
			sharedContent.text=text;
			float h=boxStyle.CalcHeight(sharedContent, w);

			float x=Input.mousePosition.x+16;
			float y=(Screen.height-Input.mousePosition.y)+16;

			x=Mathf.Clamp(x, 0, Screen.width-w);
			y=Mathf.Clamp(y, 0, Screen.height-h);

			GUI.Box(new Rect(x, y, w, h), text, boxStyle);
		}

	}

}
