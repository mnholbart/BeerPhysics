using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BeerTap : MonoBehaviour {

	public float VolumeFillPerSecond;
	public GameObject BeerEmitterPrefab;

	private List<Transform> Taps = new List<Transform>();
	private List<ParticleEmitter> TapEmitters = new List<ParticleEmitter>();

	void Awake() {
		foreach (Transform t in transform.FindChild("Taps")) {
			Taps.Add (t);
			GameObject go = Instantiate(BeerEmitterPrefab);
			go.transform.SetParent(t, false);
			TapEmitters.Add(go.GetComponent<ParticleEmitter>());
		}
	}

	void Start () {
		foreach (ParticleEmitter emitter in TapEmitters) {
			emitter.minSize = Mathf.Clamp(.075f * VolumeFillPerSecond, 0, .04f);
			emitter.maxSize = Mathf.Clamp(emitter.minSize*8f, 0, .125f);
		}

	}

	void Update () {
		CheckForBeerContainer();
	}

	/// <summary>
	/// Checks if there is something to pour into below the tap
	/// </summary>
	void CheckForBeerContainer() {

		for (int i = 0; i < Taps.Count; i++) {
			Transform t = Taps [i];
			RaycastHit hit;
			if (Physics.Raycast (t.position, Vector3.down, out hit, 5)) {
				if (hit.transform.name == "CenterTop") {
					BeerContainer b = hit.transform.GetComponentInParent<BeerContainer> ();
					if (b != null) {
						TapEmitters[i].emit = true;
						b.ReceiveBeer (VolumeFillPerSecond*Time.deltaTime);
					}
				}
				else TapEmitters[i].emit = false;
			}
		}
	}
}
