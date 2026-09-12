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
		private UnitCreep highlighted;
		private GUIStyle boxStyle;

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

			Camera cam=Camera.main;
			if(cam==null){ SetHighlight(null); return; }

			Ray ray=cam.ScreenPointToRay(Input.mousePosition);
			RaycastHit hit;

			int creepMask=1<<TDTK.GetLayerCreep();
			if(Physics.Raycast(ray, out hit, Mathf.Infinity, creepMask)){
				UnitCreep creep=hit.collider.GetComponentInParent<UnitCreep>();
				if(creep!=null && creep.hp>0) hovered=creep;
			}

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

			SetHighlight(hovered!=null ? hovered : hoveredGhostCreep);
		}

		// highlight the active pair; clear the previous one when the hover target changes
		private void SetHighlight(UnitCreep c){
			if(highlighted!=null && highlighted!=c) highlighted.SetHovered(false);
			highlighted=c;
			if(highlighted!=null) highlighted.SetHovered(true);	//reapplied each frame so rebuilt ghosts stay lit
		}

		void OnGUI(){
			if(hovered!=null) DrawCreepBox(hovered);
			else if(hoveredGhostCreep!=null) DrawCreepBox(hoveredGhostCreep);
			else if(hoveredTower!=null) DrawTowerBox();
		}

		private void EnsureStyle(){
			if(boxStyle==null){
				boxStyle=new GUIStyle(GUI.skin.box);
				boxStyle.alignment=TextAnchor.UpperLeft;
				boxStyle.padding=new RectOffset(8, 8, 6, 6);
				boxStyle.normal.textColor=Color.white;
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

			float w=180;
			float h=hasShield ? 74 : 56;
			DrawBox(text, w, h);
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
			float h=hasDamage ? 92 : 38;
			DrawBox(text, w, h);
		}

		private void DrawBox(string text, float w, float h){
			float x=Input.mousePosition.x+16;
			float y=(Screen.height-Input.mousePosition.y)+16;

			x=Mathf.Clamp(x, 0, Screen.width-w);
			y=Mathf.Clamp(y, 0, Screen.height-h);

			GUI.Box(new Rect(x, y, w, h), text, boxStyle);
		}

	}

}
