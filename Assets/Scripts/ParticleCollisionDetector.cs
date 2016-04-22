using UnityEngine;
using System.Collections;

public class ParticleCollisionDetector : MonoBehaviour {

	public Transform attachedBeer;
	
	void OnParticleCollision(GameObject other) 
	{
		Vector3 newLocalScale = attachedBeer.localScale;
		if(newLocalScale.y < 1) 
			newLocalScale.y += 0.0005f;
		attachedBeer.localScale = newLocalScale;
	}
}
