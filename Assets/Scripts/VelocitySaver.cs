using UnityEngine;
using System.Collections;
using System.Collections.Generic;

	public class VelocitySaver : MonoBehaviour
	{
		public List<Vector3> velocities = new List<Vector3>();
		public Vector3 angularVelocity;
		public Vector3 prevPos;
		public Quaternion prevRot;
		
		private const int numOfFramesRecordedThrowRelase = 60;
		
		void Update()
		{
			//determine velocity
			Vector3 newPos = transform.position;
			Vector3 calculatedVelocity = (newPos - prevPos) / Time.deltaTime;
			
			velocities.Add(calculatedVelocity);
			
			if (velocities.Count > numOfFramesRecordedThrowRelase)
			{
				velocities.RemoveAt(0);
			}
			
			//determine angular velocity
			Quaternion newRot = transform.rotation; //possibly rotation
			Quaternion deltaQuat = newRot * Quaternion.Inverse(prevRot); //possibly swap positions
			
			float angle = 2 * Mathf.Acos(deltaQuat.w);
			float x = deltaQuat.x / Mathf.Sqrt(1 - deltaQuat.w * deltaQuat.w);
			float y = deltaQuat.y / Mathf.Sqrt(1 - deltaQuat.w * deltaQuat.w);
			float z = deltaQuat.z / Mathf.Sqrt(1 - deltaQuat.w * deltaQuat.w);
			
			angularVelocity = (new Vector3(x, y, z) * angle) * (1 / Time.deltaTime);
			
			prevPos = transform.position;
			prevRot = transform.rotation;
			
		}
	}