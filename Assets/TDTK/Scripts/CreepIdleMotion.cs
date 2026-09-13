using UnityEngine;

namespace TDTK {

	// Lightweight procedural idle motion for creep models (no rig required).
	// Attach to the visual "Model" child so it layers on top of TDTK path movement.
	public class CreepIdleMotion : MonoBehaviour {

		public float bobHeight = 0.05f;		// vertical rise/fall (world-ish, local units)
		public float bobSpeed = 1.6f;		// bob cycles per second
		public float swayAngle = 4f;		// max roll/sway in degrees
		public float swaySpeed = 0.9f;		// sway cycles per second

		Vector3 basePos;
		Quaternion baseRot;
		float seed;

		void OnEnable() {
			basePos = transform.localPosition;
			baseRot = transform.localRotation;
			seed = Random.value * 10f;		// desync creeps so they don't move in lockstep
		}

		void Update() {
			float t = Time.time + seed;
			float bob = Mathf.Sin(t * bobSpeed * Mathf.PI * 2f) * bobHeight;
			float sway = Mathf.Sin(t * swaySpeed * Mathf.PI * 2f) * swayAngle;

			transform.localPosition = basePos + new Vector3(0f, bob, 0f);
			transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, sway);
		}

		void OnDisable() {
			transform.localPosition = basePos;
			transform.localRotation = baseRot;
		}
	}

}
