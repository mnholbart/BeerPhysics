using UnityEngine;
using System.Collections;

public class FollowTransform : MonoBehaviour {

	public Transform t;
	void LateUpdate () 
	{
		transform.position = t.position;
	}
}
