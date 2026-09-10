using UnityEngine;

namespace TDTK{

	// Shows a small popup with a creep's stats (name/HP/shield) while the mouse hovers over it.
	// Self-bootstraps at scene load so no manual scene wiring is required.
	public class CreepHover : MonoBehaviour {

		private UnitCreep hovered;
		private GUIStyle boxStyle;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		static void Bootstrap(){
			if(FindObjectOfType<CreepHover>()!=null) return;
			GameObject obj=new GameObject("_CreepHover");
			obj.AddComponent<CreepHover>();
		}

		void Update(){
			hovered=null;

			Camera cam=Camera.main;
			if(cam==null) return;

			Ray ray=cam.ScreenPointToRay(Input.mousePosition);
			int mask=1<<TDTK.GetLayerCreep();
			RaycastHit hit;
			if(Physics.Raycast(ray, out hit, Mathf.Infinity, mask)){
				UnitCreep creep=hit.collider.GetComponentInParent<UnitCreep>();
				if(creep!=null && creep.hp>0) hovered=creep;
			}
		}

		void OnGUI(){
			if(hovered==null) return;

			if(boxStyle==null){
				boxStyle=new GUIStyle(GUI.skin.box);
				boxStyle.alignment=TextAnchor.UpperLeft;
				boxStyle.padding=new RectOffset(8, 8, 6, 6);
				boxStyle.normal.textColor=Color.white;
			}

			bool hasShield=hovered.GetFullSH()>0;

			string text=hovered.unitName
				+ "\nHP: " + Mathf.CeilToInt(hovered.hp) + " / " + Mathf.CeilToInt(hovered.GetFullHP());
			if(hasShield){
				text += "\nShield: " + Mathf.CeilToInt(hovered.sh) + " / " + Mathf.CeilToInt(hovered.GetFullSH());
			}

			float w=180;
			float h=hasShield ? 74 : 56;
			float x=Input.mousePosition.x+16;
			float y=(Screen.height-Input.mousePosition.y)+16;

			x=Mathf.Clamp(x, 0, Screen.width-w);
			y=Mathf.Clamp(y, 0, Screen.height-h);

			GUI.Box(new Rect(x, y, w, h), text, boxStyle);
		}

	}

}
