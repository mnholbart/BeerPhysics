////////////////////////////////////////////////////////
//
// BeerContainer.cs
//
// description:	Gives a container the ability to pour and receive liquids
//
// usage: 		Currently only works with a cylindrical container, also needs the radius and height to 
//				be manually tuned in until an alternate and accurate method is found
//
// authors: 	Morgan Holbart
//
////////////////////////////////////////////////////////

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

//TODO
//Work with non cylindrical containers
//Figure out a way to get exact height/radius because it needs to be exact or it will look wrong (currently hardcoded might need to be done manually)
//Rename all beer references to liquid for portability to other projects
//Some clipping through the glass on top joint Z scaling when under 50% full
//Issues with bottom joint movement when rotating on the Y, doesn't know which way is down
//Could be optimized greatly, especially doing the same math multiple times in one frame, but probably unnecessary

public class BeerContainer : MonoBehaviour {

	public int numberLowPoints = 24;							//How many lowpoints to check for (Higher is more accurate but potentially bugged if the radius isnt large enough
	public float beerPercent = 0.6f;							//How full to start the container, accepts decimal or number percent
	public float radius;										//Radius of the container
	public float height;										//Height of the container
	public GameObject beerPrefab;								//Prefab for the beer used for this container
	public GameObject emitterPrefab;							//Prefab for the beer pouring emitter

	private float theta;										//Angle needed to pour current contents in radians
	private float deltaRot;										//Current degree rotation on x and/or z axis
	private float thetaDegrees;									//Angle needed to pour current contents in degrees
	private float halfThetaDegrees; 							//Degrees needed to empty half the container
	private float H;											//Height of the liquid when upright (0 degrees deltaRot)
	private float glassYScale;									//Scale of the containers Y to calculate height properly
	private float overfillTimer;								//Timer to see if we are still overflowing 
	private float totalVolume;									//Volume of container when full
	private bool pouring;										//if Currently pouring beer
	private bool overfilling;									//if Currently overflowing beer
	private GameObject beerObj;									//Reference to beer game object
	private Transform beer;										//Transform for the beer liquid object
	private Transform topJoint;									//Top joint of the beer
	private Transform bottomJoint;								//Bottom joint of the beer
	private Transform lowpoint;									//Current lowpoint on the container
	private ParticleEmitter emitter;							//Emitter used for pouring beer
	private List<Transform> lowpoints = new List<Transform>();	//List of all the placeholder objects
	
	public bool debugDontPour;									//Don't pour liquid 
	public bool debugDontScale;									//Don't scale liquid
	public bool debugDontMove;									//Don't move liquid

	void Awake() {
		beerObj = Instantiate(beerPrefab, transform.position, Quaternion.identity) as GameObject;
		beerObj.transform.SetParent(transform, false);
		beerObj.transform.localPosition = Vector3.zero;
		topJoint = beerObj.transform.FindChild("UpperJoint");
		bottomJoint = beerObj.transform.FindChild("LowerJoint");
		beer = beerObj.transform;
		beerObj.name = "BeerLiquid";
		GameObject go = Instantiate(emitterPrefab, transform.position, Quaternion.identity) as GameObject;
		emitter = go.GetComponent<ParticleEmitter>();
		emitter.transform.SetParent(transform, false);
		emitter.name = "ParticleEmitter";
	}

	void Start() {
		BoxCollider container = transform.FindChild("ContainerMesh").GetComponent<BoxCollider>();
		glassYScale = 1/transform.localScale.y;
		totalVolume = Mathf.PI * Mathf.Pow(radius, 2) * height; //Find total volume of container
		float halfHeight = .5f*height;
		halfThetaDegrees = 90 - (Mathf.Atan(radius/(height-halfHeight)) * Mathf.Rad2Deg); 

		if (beerPercent < 1) //If someone enters a decimal percent convert
			beerPercent *= 100;
		if (beerPercent >= 100) //Can't overfill it
			beerPercent = 100;

		SetupLowpoints ();
	}
	
	void Update() {
		UpdateLowpoint ();
		if (!IsContainerEmpty()) {
			LiquidMovement();
			PourBeer();
		}
	}

	void LiquidMovement () {

		Vector3 v = transform.rotation.eulerAngles;
		v.y = 0;
		deltaRot = Quaternion.Angle (Quaternion.identity, Quaternion.Euler (v)); //Get delta of current rotation to identity

		H = (beerPercent * .01f * height); //Get the Height of the liquid 
		float h = height; //Height of the container
		float r = beerPercent <= 50 ? radius * (beerPercent / 50) : radius; //Radius used for calculation, changes when <50% full because liquid no longer covers base of container

		theta = Mathf.Atan (r / (h - H)); //Calculate angle needed to spill liquid in radians
		thetaDegrees = 90 - (theta * Mathf.Rad2Deg); //Angle needed in degrees

		float x = 2 * r / Mathf.Tan (theta); //Space needed for top joint rotation
		float bH = h - x; //Space that shouldn't be touched at present % full
		x = Mathf.Clamp (x, 0, height);

//		Debug.Log (thetaDegrees);
//		Debug.Log("Height: " + height + " Radius: " + radius + " X: " + x + " bH: " + bH);


		//POSITIONAL
		float d = Mathf.Clamp (deltaRot, 0, thetaDegrees); //Add height based on the volume in the cup using the areas bH and x
		float yPos = H * glassYScale + .16f; //TODO .16f should be the container.size.y - height to account for the unfillable liquid sections 

		if (beerPercent < 50)
			yPos += -bH / 2 * glassYScale * (deltaRot / thetaDegrees); //Need to account for negative volume
		if (!debugDontMove) {
			topJoint.transform.localPosition = new Vector3 (topJoint.transform.localPosition.x, yPos, topJoint.transform.localPosition.z);
			if (beerPercent <= 50) {
				float xOffset = 0; //How much the liquid needs to be offset to stay at the bottom of the glass
				float xMax = 1 + 2 * radius + .2f; //Max amount of offset needed TODO take the -.2f off if can find alternative
				xOffset = xMax - xMax * (beerPercent / 50); //Current offset based on % full
//				beerObj.transform.localPosition = new Vector3 ((deltaRot / thetaDegrees) * xOffset, 0, 0);
				beerObj.transform.localPosition = Vector3.zero + transform.up * (deltaRot/thetaDegrees) * xOffset; //TODO find a way to compensate for y rotation
			}
		}


		//SCALING
		//When its less than half full, you have to scale down the liquid or it will clip into itself
		float scaleDist = 2 * radius; //Distance covered by a scale of 1 is 2r, could be changed to get the scale of the joint but it should always be 1 by default
		float a = 2 * r; 
		float xScaleNeeded = 0;
		if (beerPercent <= 50) {
			a = (beerPercent / 50) * 2 * radius; //distance of bottom of liquid at current percent at theta
			float bottomJointScale = a / scaleDist - 1;
			xScaleNeeded = bottomJointScale;
			if (!debugDontScale) 
				bottomJoint.localScale = new Vector3 (1 + (deltaRot/thetaDegrees) * bottomJointScale, 1, 1 + (deltaRot / thetaDegrees) * bottomJointScale);
		}
		float distNeeded = Mathf.Sqrt (Mathf.Pow (x, 2) + Mathf.Pow (a, 2)); //Pythag theorem to find dist needed to cover surface
		float scaleNeeded = distNeeded / scaleDist - 1; //Scale needed without base scale of 1

		if (!debugDontScale)
			topJoint.localScale = new Vector3 (1 + (deltaRot/thetaDegrees) * xScaleNeeded, 1, 1 + (deltaRot / thetaDegrees) * scaleNeeded);


		//ROTATIONAL
		//Top joint rotation
		//Rotate the top t based on the deltaRot but cap it at 90 so it goes between parallel and perpendicular with the container
		topJoint.localRotation = Quaternion.Slerp (topJoint.localRotation, Quaternion.AngleAxis (d, -Vector3.right), .35f);
		//Needs to rotate so that the top joint is always on the bottom of the glass
		float deg = (lowpoints.IndexOf (lowpoint) * 360 / numberLowPoints) - transform.localEulerAngles.y; //Find the rotation needed for the beer for the lowest point
		deg += transform.localEulerAngles.y; //Add the current rotation of the glass
		while (deg > 360) //just precautionary
			deg -= 360;
		Quaternion q = Quaternion.AngleAxis (deg, Vector3.up); //Rotate on the Y axis by the degrees needed
		beer.transform.localRotation = Quaternion.Slerp (beer.transform.localRotation, q, .25f);
	}

	/// <summary>
	/// Updates the current lowpoint on the lip of the glass
	/// </summary>
	void UpdateLowpoint () {
		foreach (Transform t in lowpoints) {
			//Find the lowest point of the glass
			if (t.transform.position.y < lowpoint.transform.position.y) {
				lowpoint = t;
				emitter.transform.position = lowpoint.transform.position;
			}
		}
	}

	/// <summary>
	/// Checks if empty.
	/// </summary>
	bool IsContainerEmpty () {
		if (beerPercent > 100)
			beerPercent = 100;

		if (beerPercent <= 0) {
			emitter.emit = false;
			beer.gameObject.SetActive (false);
			return true;
		}
		return false;
	}

	/// <summary>
	/// Pours the beer and attempts to fill another container
	/// The more the container is turned, the more it empties
	/// </summary>
	void PourBeer() {

		if (Time.time > overfillTimer && overfilling) { 
			overfilling = false;
			emitter.emit = false;
		}

		if (ShouldPour() && !pouring) {
			emitter.emit = true;
			pouring = true;
		}

		if (!ShouldPour() && pouring) { //If arent tilted enough to pour
			if (!overfilling)
				emitter.emit = false;
			pouring = false;
			return;
		}

		if (!pouring)
			return;

//		//Calculate how much liquid is in the difference of the thetaDegree angle and our current angle
		float Vi = Mathf.PI * Mathf.Pow(radius, 2) * H; //Volume we start at 
		float overpour = .5f * (deltaRot-thetaDegrees);
		float percentDelta = .5f + overpour; //How much % we are dropping
		float Vf = Mathf.PI * Mathf.Pow(radius, 2) * ((beerPercent-percentDelta) * .01f * height); //Volume we end at

		if (beerPercent - percentDelta < 0) //Cant empty more than we have
			percentDelta = beerPercent;

		if (!debugDontPour)
			beerPercent -= percentDelta; 

		emitter.minSize = Mathf.Clamp(.25f * percentDelta, .02f, .06f);
		emitter.maxSize = Mathf.Clamp(emitter.minSize*2.5f, .045f, .12f);

		BeerContainer bc = GetPourContainer();
		if (bc != null)
			bc.ReceiveBeer(Vi - Vf);
	}

	/// <summary>
	/// Pours the overflow beer if we attempt to fill an already full beer
	/// </summary>
	void PourOverflowBeer(float volume) {
		BeerContainer bc = GetPourContainer();
		if (bc != null) {

			float percent = volume/totalVolume;
			emitter.minSize = .045f * percent;
			emitter.minSize = Mathf.Clamp(.25f * percent, .02f, .06f);
			emitter.maxSize = Mathf.Clamp(emitter.minSize*2.5f, .045f, .12f);
			bc.ReceiveBeer(volume);
		}
	}

	/// <summary>
	/// Receives beer from something pouring beer
	/// </summary>
	public void ReceiveBeer(float volume) {
		//Convert volume received into a %
		float percentReceived = volume/totalVolume * 100;

		if (beerPercent + percentReceived > 100) { //cant overfill
			if (!overfilling) {
				overfilling = true;
				emitter.emit = true;
			}
			PourOverflowBeer(volume);
			overfillTimer = Time.time + 2 * Time.fixedDeltaTime;
			return;
		}

		beerPercent += percentReceived;
		
		if (!beer.gameObject.activeSelf && beerPercent > .01f) //Enough beer to show up in the glass
			beer.gameObject.SetActive(true);
	}

	/// <summary>
	/// Gets the container we should pour into.
	/// </summary>
	public BeerContainer GetPourContainer() {
		RaycastHit hit;
		if (Physics.Raycast(lowpoint.transform.position, Vector3.down, out hit, 5)) {
			if (hit.transform.name == "CenterTop") 
				return hit.transform.GetComponentInParent<BeerContainer>();
			else return null;
		}
		return null;
	}

	/// <summary>
	/// Degrees rotated needed to pour given current percent full
	/// </summary>
	bool ShouldPour() {
		return deltaRot >= thetaDegrees ? true : false;
	}

	/// <summary>
	/// Setups the lowpoints in a circle around the top of the glass
	/// </summary>
	void SetupLowpoints () {
		GameObject centerTop = new GameObject("CenterTop"); //Center point at the container opening
		BoxCollider c = centerTop.AddComponent<BoxCollider>(); 
		centerTop.AddComponent<Rigidbody>();

		centerTop.GetComponent<Rigidbody>().isKinematic = true;
		SkinnedMeshRenderer b = beer.FindChild("Liquid").GetComponent<SkinnedMeshRenderer>(); 
		c.size = new Vector3(b.localBounds.extents.x*2, .1f, b.localBounds.extents.z*2); //Set size equal to that of the liquid (needs change if container openings change in size)

		centerTop.transform.SetParent(transform, false);
		centerTop.transform.localPosition = Vector3.zero;
		centerTop.transform.position = new Vector3 (centerTop.transform.position.x, transform.FindChild ("ContainerMesh").GetComponent<Renderer> ().bounds.max.y, centerTop.transform.position.z);
		float radius = beer.FindChild ("Liquid").GetComponent<Renderer> ().bounds.extents.x / 2 + .3f;
		float angle = 0;
		for (int i = 0; i < numberLowPoints; i++) {
			Vector3 pos;
			pos.x = centerTop.transform.position.x + radius * Mathf.Sin (angle * Mathf.Deg2Rad);
			pos.y = centerTop.transform.position.y;
			pos.z = centerTop.transform.position.z + radius * Mathf.Cos (angle * Mathf.Deg2Rad);
			GameObject empty = new GameObject(i.ToString());
			empty.transform.parent = centerTop.transform;
			empty.transform.position = pos;
			angle += 360/numberLowPoints;
			lowpoints.Add (empty.transform);
		}
		lowpoint = lowpoints[0];
		emitter.transform.position = lowpoint.transform.position;
	}
}
