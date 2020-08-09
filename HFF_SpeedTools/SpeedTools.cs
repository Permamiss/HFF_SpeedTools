/* SpeedTools Mod for Human: Fall Flat
 * Written by Permamiss | http://twitter.com/Permamiss | https://steamcommunity.com/id/Permamiss | https://www.twitch.tv/Permamiss
 * Feel free to contact me if you have any ideas or suggestions. Thanks!
 */

using BepInEx;

namespace HFF_SpeedTools
{
	using UnityEngine;

	[BepInPlugin("org.bepinex.plugins.humanfallflat.speedtools", "Speedrun-Practice Tools", "1.0.0.1")]
	[BepInProcess("Human.exe")]
	public class SpeedTools : BaseUnityPlugin
	{
		private GUIStyle theStyle;
		private Color[] colorSwatch;

		private int gameLevel;

		private int curColor;
		private int curSpeedIter;
		private int prevCpNum, curCpNum;

		private float curSpeed;
		private float maxSpeedReached;
		private float[] speedsOverTime = new float[average_SecondsTracked * 60]; // seconds desired times expected frames per second

		static bool CheatsActivated = false; // set to true when player uses any of these
		private bool renderMenu;
		private bool renderCheckpoints;
		private bool renderDebug;
		private bool renderLoadingZone;
		private bool renderSpeed;
		private bool renderCpNum;

		private string debugText;
		private string menuMessage;
		private GameObject[] primitives;
		private GameObject cubePrimitive, capsulePrimitive, spherePrimitive, meshPrimitive;
		private GameObject[] checkpointVisuals;
		private GameObject loadingZoneVisual;
		private HumanAPI.LevelPassTrigger curLoadingZone;

		private const int average_SecondsTracked = 45; // how many seconds the speedometer's average tracks for
		private const string modAuthor = "Permamiss";
		private const string modName = "Speedrun-Practice Tools";
		private const string modGame = "Human: Fall Flat";

		//private int curPage = 0;

		public static System.Collections.ArrayList UndestroyedObjects = new System.Collections.ArrayList();



		public void DebugMessage(string theText, int amtSeconds, bool logIt = true)
		{
			if (logIt)
				Shell.Print("<#00AA00>SpeedTools ></color> " + theText);
			else
			{
				StopCoroutine("ClearMessage");

				this.renderDebug = true;
				this.debugText = theText;

				if (amtSeconds > 0)
					StartCoroutine("ClearMessage", (float)amtSeconds);
			}
		}

		public void DebugMessage(string theText, float amtSeconds = 2.0f, bool logIt = true)
		{
			if (logIt)
				Shell.Print("<#00AA00>SpeedTools></color> " + theText);
			else
			{
				amtSeconds = System.Math.Max(amtSeconds, 1.0f); // any less may cause the game to lag
				StopCoroutine("ClearMessage");

				this.renderDebug = true;
				this.debugText = theText;

				StartCoroutine("ClearMessage", amtSeconds);
			}
		}

		private System.Collections.IEnumerator ClearMessage(float seconds)
		{
			yield return new WaitForSeconds(seconds);
			this.renderDebug = false;
		}

		private void ClearMessage()
		{
			this.renderDebug = false;
		}

		private static new void DontDestroyOnLoad(Object target)
		{
			Object.DontDestroyOnLoad(target as GameObject);
			UndestroyedObjects.Add(target);
		}

		private void ResetAverage() // set average back to 0 and start over
		{
			this.curSpeedIter = 0;

			for (int i = 0; i < this.speedsOverTime.Length; i++)
			{
				this.speedsOverTime[i] = 0.0f;
			}
		}

		private void RefreshMenuMessage()
		{
			this.menuMessage = " =" + modName + " Menu= \n";
			//menuMessage += string.Format("  Page {0} of {1}", this.curPage + 1, (int)System.Math.Ceiling((this.curPage + 1) / 7.0));
			for (int i = 0; i < (" =" + modName + " Menu= ").Length; i++)
				this.menuMessage += "-";
			this.menuMessage += "\n" +
				string.Format("1) Toggle Speedometer [{0}]\n" +
				"2) Toggle Hit-Box Visual for Checkpoints [{1}]\n" +
				"3) Toggle Hit-Box Visual for Loading Zones [{2}]\n" +
				"4) Change Speedometer text color\n" +
				"5) Reset Speedometer\n" +
				//"6) Toggle firework launcher [{3}]"
				"6) Toggle Checkpoint number display [{3}]\n" +
				'\u2190' + ") Change framerate cap [{4}]"
				, this.renderSpeed ? "ENABLED" : "DISABLED", this.renderCheckpoints ? "ENABLED" : "DISABLED", this.renderLoadingZone ? "ENABLED" : "DISABLED", this.renderCpNum ? "ENABLED" : "DISABLED", Application.targetFrameRate.ToString()) +
				//(Fireworks.instance && Fireworks.instance.enableWeapons) ? "ENABLED" : "DISABLED") +

				"\n" +
				"0) Exit";
		}

		private void MenuMessages()
		{
			DebugMessage(this.menuMessage, 0, false);
		}

		private void SpeedometerMessages()
		{
			float averageSpeed = 0.0f;
			foreach (float newSpeed in this.speedsOverTime)
			{
				averageSpeed += newSpeed;
			}
			averageSpeed /= (this.curSpeedIter + 1); // divide sum of existing speeds by amount to obtain TRUE average

			GUI.Label(new Rect(Screen.width / 2, Screen.height - 150, 0, 50), string.Format("Current Speed: {0:0.0}", Human.instance ? Mathf.Sqrt(Mathf.Pow(Human.instance.velocity.x, 2.0f) + Mathf.Pow(Human.instance.velocity.z, 2.0f)) : 0.0f), this.theStyle);
			GUI.Label(new Rect(Screen.width / 2, Screen.height - 100, 0, 50), string.Format("Average Speed: {0:0.0}", averageSpeed), this.theStyle);
			GUI.Label(new Rect(Screen.width / 2, Screen.height - 50, 0, 50), string.Format("Maximum Speed: {0:0.0}", this.maxSpeedReached), this.theStyle);
		}

		private void ShowCheckpointNum()
		{
			GUI.Label(new Rect(Screen.width - 100, 30, 100, 50), "Checkpoint: " + Game.instance.currentCheckpointNumber + " (Previous: " + this.prevCpNum + ")");
		}

		private GameObject generateHitboxVisual(HumanAPI.Checkpoint sourceObject)
		{
			/*
			* 0 = box/cube
			* 1 = capsule
			* 2 = sphere
			* 3 = mesh
			*/
			int colType = -1;
			GameObject hitboxVisual;
			BoxCollider boxCol = sourceObject.gameObject.GetComponent<BoxCollider>();
			CapsuleCollider capsuleCol = sourceObject.gameObject.GetComponent<CapsuleCollider>();
			SphereCollider sphereCol = sourceObject.gameObject.GetComponent<SphereCollider>();
			MeshCollider meshCol = sourceObject.gameObject.GetComponent<MeshCollider>();
			//maybe add meshcollider as well

			if (boxCol)
				colType = 0;
			else if (capsuleCol)
				colType = 1;
			else if (sphereCol)
				colType = 2;
			else if (meshCol)
				colType = 3;
			
			switch (colType)
			{
				case 0:
					hitboxVisual = Instantiate(this.cubePrimitive);
					hitboxVisual.transform.localScale = new Vector3(boxCol.size.x * sourceObject.transform.localScale.x, boxCol.size.y * sourceObject.transform.localScale.y, boxCol.size.z * sourceObject.transform.localScale.z);
					hitboxVisual.transform.SetParent(sourceObject.transform, false);
					hitboxVisual.transform.position = boxCol.bounds.center;
					break;
				case 1:
					DebugMessage("Collider type is CapsuleCollider");
					hitboxVisual = Instantiate(this.capsulePrimitive);
					hitboxVisual.transform.localScale = new Vector3(capsuleCol.radius * 2 * capsuleCol.transform.localScale.x, capsuleCol.height * capsuleCol.transform.localScale.y, capsuleCol.radius * 2 * capsuleCol.transform.localScale.z);
					hitboxVisual.transform.SetParent(capsuleCol.transform, false);
					break;
				case 2:
					DebugMessage("Collider type is SphereCollider");
					hitboxVisual = Instantiate(this.spherePrimitive);
					hitboxVisual.transform.localScale = new Vector3(sphereCol.radius * 2 * sphereCol.transform.localScale.x, sphereCol.radius * 2 * sphereCol.transform.localScale.y, sphereCol.radius * 2 * sphereCol.transform.localScale.z);
					hitboxVisual.transform.SetParent(sphereCol.transform, false);
					break;
				default:
					DebugMessage("Collider type is MeshCollider or something else or nonexistant");
					hitboxVisual = Instantiate(this.cubePrimitive);
					hitboxVisual.transform.localScale = sourceObject.transform.localScale;
					hitboxVisual.transform.SetParent(sourceObject.transform, false);
					break;
			}

			//hitboxVisual.transform.SetParent(sourceObject.transform, false);
			hitboxVisual.name = "CheckpointVisual";
			return hitboxVisual;
		}

		private GameObject generateHitboxVisual(HumanAPI.LevelPassTrigger sourceObject)
		{
			/*
			* 0 = box/cube
			* 1 = capsule
			* 2 = sphere
			* 3 = mesh
			*/
			int colType = -1;
			GameObject hitboxVisual;
			BoxCollider boxCol = sourceObject.gameObject.GetComponent<BoxCollider>();
			CapsuleCollider capsuleCol = sourceObject.gameObject.GetComponent<CapsuleCollider>();
			SphereCollider sphereCol = sourceObject.gameObject.GetComponent<SphereCollider>();
			MeshCollider meshCol = sourceObject.gameObject.GetComponent<MeshCollider>();
			//maybe add meshcollider as well

			if (boxCol)
				colType = 0;
			else if (capsuleCol)
				colType = 1;
			else if (sphereCol)
				colType = 2;
			else if (meshCol)
				colType = 3;

			switch (colType)
			{
				case 0:
					hitboxVisual = Instantiate(this.cubePrimitive);
					hitboxVisual.transform.localScale = new Vector3(boxCol.size.x * sourceObject.transform.localScale.x, boxCol.size.y * sourceObject.transform.localScale.y, boxCol.size.z * sourceObject.transform.localScale.z);
					hitboxVisual.transform.SetParent(sourceObject.transform, false);
					hitboxVisual.transform.position = boxCol.bounds.center;
					break;
				case 1:
					DebugMessage("Collider type is CapsuleCollider");
					hitboxVisual = Instantiate(this.capsulePrimitive);
					hitboxVisual.transform.localScale = new Vector3(capsuleCol.radius * 2 * capsuleCol.transform.localScale.x, capsuleCol.height * capsuleCol.transform.localScale.y, capsuleCol.radius * 2 * capsuleCol.transform.localScale.z);
					hitboxVisual.transform.SetParent(capsuleCol.transform, false);
					break;
				case 2:
					DebugMessage("Collider type is SphereCollider");
					hitboxVisual = Instantiate(this.spherePrimitive);
					hitboxVisual.transform.localScale = new Vector3(sphereCol.radius * 2 * sphereCol.transform.localScale.x, sphereCol.radius * 2 * sphereCol.transform.localScale.y, sphereCol.radius * 2 * sphereCol.transform.localScale.z);
					hitboxVisual.transform.SetParent(sphereCol.transform, false);
					break;
				default:
					DebugMessage("Collider type is MeshCollider or something else or nonexistant");
					hitboxVisual = Instantiate(this.cubePrimitive);
					hitboxVisual.transform.localScale = sourceObject.transform.localScale;
					hitboxVisual.transform.SetParent(sourceObject.transform, false);
					break;
			}
			
			hitboxVisual.name = "LoadingZoneVisual";
			return hitboxVisual;
		}

		private void RenderCheckpoints(bool active)
		{
			HumanAPI.Checkpoint[] lvlCPs = FindObjectsOfType<HumanAPI.Checkpoint>();
			// get rid of previous checkpoint visuals
			foreach (GameObject go in this.checkpointVisuals)
			{
				Destroy(go);
			}
			this.checkpointVisuals.Initialize(); // reset array
			//DebugMessage("Reset checkpointVisuals array");
			if (active)
			{
				HumanAPI.LevelPassTrigger theLoadZone = FindObjectOfType<HumanAPI.LevelPassTrigger>();
				for (int i = 0; i < lvlCPs.Length; i++)
				{
					if (lvlCPs[i].number != 0) // only generate visual if it's not the "first" checkpoint (where you spawn)
					{
						this.checkpointVisuals[i] = this.generateHitboxVisual(lvlCPs[i]);
						this.checkpointVisuals[i].GetComponent<Renderer>().material.SetColor("_Color", new Color(1.0f, 0.66f, 0.0f, 0.33f)); // orange for checkpoints :)
						this.checkpointVisuals[i].SetActive(true);
					}
				}
			}
		}

		private void RenderLoadingZone(bool active)
		{
			this.curLoadingZone = FindObjectOfType<HumanAPI.LevelPassTrigger>();
			// get rid of previous loading zone visual
			if (this.loadingZoneVisual != null)
				Destroy(this.loadingZoneVisual);
			if (active && this.curLoadingZone)
			{
				this.loadingZoneVisual = this.generateHitboxVisual(this.curLoadingZone);
				this.loadingZoneVisual.SetActive(true);
			}
		}

		private void SetRenderCheckpoints(string txt)
		{
			SpeedTools.CheatsActivated = true;
			if (string.IsNullOrEmpty(txt))
				this.renderCheckpoints = !this.renderCheckpoints; // toggles if no words after command
			else
			{
				string[] words = txt.Split(new char[]
				{
					' '
				}, System.StringSplitOptions.RemoveEmptyEntries);

				if (words.Length != 1)
					return;

				int num;
				if (int.TryParse(words[0], out num))
					this.renderCheckpoints = num != 0;
				else if (words[0].ToLower() == "true")
					this.renderCheckpoints = true;
				else if (words[0].ToLower() == "false")
					this.renderCheckpoints = false;
			}
			this.RenderCheckpoints(this.renderCheckpoints);
			Shell.Print(string.Format("checkpoint hit-box renderer {0}", this.renderCheckpoints ? "enabled" : "disabled"));
		}

		private void SetRenderLoadingZone(string txt)
		{
			SpeedTools.CheatsActivated = true;
			if (string.IsNullOrEmpty(txt))
				this.renderLoadingZone = !this.renderLoadingZone; // toggles if no words after command
			else
			{
				string[] words = txt.Split(new char[]
				{
					' '
				}, System.StringSplitOptions.RemoveEmptyEntries);

				if (words.Length != 1)
					return;

				int num;
				if (int.TryParse(words[0], out num))
					this.renderLoadingZone = num != 0;
				else if (words[0].ToLower() == "true")
					this.renderLoadingZone = true;
				else if (words[0].ToLower() == "false")
					this.renderLoadingZone = false;
			}
			this.RenderLoadingZone(this.renderLoadingZone);
			Shell.Print(string.Format("loading zone hit-box renderer {0}", this.renderLoadingZone ? "enabled" : "disabled"));
		}

		private void SetDisplaySpeedometer(string txt)
		{
			SpeedTools.CheatsActivated = true;
			if (string.IsNullOrEmpty(txt))
				this.renderSpeed = !this.renderSpeed; // toggles if no words after command
			else
			{
				string[] words = txt.Split(new char[]
				{
					' '
				}, System.StringSplitOptions.RemoveEmptyEntries);

				if (words.Length != 1)
					return;

				int num;
				if (int.TryParse(words[0], out num))
					this.renderSpeed = num != 0;
				else if (words[0].ToLower() == "true")
					this.renderSpeed = true;
				else if (words[0].ToLower() == "false")
					this.renderSpeed = false;
			}
			Shell.Print(string.Format("speedometer {0}", this.renderSpeed ? "enabled" : "disabled"));
		}

		private void SetDisplayCheckpointNum(string txt)
		{
			SpeedTools.CheatsActivated = true;
			if (string.IsNullOrEmpty(txt))
				this.renderCpNum = !this.renderCpNum; // toggles if no words after command
			else
			{
				string[] words = txt.Split(new char[]
				{
					' '
				}, System.StringSplitOptions.RemoveEmptyEntries);

				if (words.Length != 1)
					return;

				int num;
				if (int.TryParse(words[0], out num))
					this.renderSpeed = num != 0;
				else if (words[0].ToLower() == "true")
					this.renderCpNum = true;
				else if (words[0].ToLower() == "false")
					this.renderCpNum = false;
			}
			Shell.Print(string.Format("checkpoint number display {0}", this.renderCpNum ? "enabled" : "disabled"));
		}


		private void Start()
		{
			this.gameLevel = -1;

			this.curColor = 0;
			this.curSpeedIter = 0;
			this.maxSpeedReached = 0.0f;
			this.prevCpNum = this.curCpNum = 0;

			this.renderCheckpoints = false;
			this.renderLoadingZone = false;
			this.renderSpeed = false;
			this.renderMenu = false;
			this.renderDebug = false;

			this.debugText = string.Empty;
			this.curLoadingZone = null;
			this.colorSwatch = new Color[8] { Color.black, Color.white, Color.red, new Color(1.0f, 0.549f, 0.0f), new Color(1.0f, 1.0f, 0.0f), Color.green, Color.blue, new Color(0.5f, 0.0f, 0.5f) }; // black, white, and colors of rainbow

			this.theStyle = new GUIStyle()
			{
				wordWrap = false,
				fontSize = 48,
				alignment = TextAnchor.UpperCenter
			};
			this.theStyle.normal.textColor = this.colorSwatch[0];

			this.cubePrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
			this.capsulePrimitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
			this.spherePrimitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			this.meshPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube); // dunno how to implement a mesh at the moment

			this.primitives = new GameObject[4];
			this.primitives[0] = this.cubePrimitive;
			this.primitives[1] = this.capsulePrimitive;
			this.primitives[2] = this.spherePrimitive;
			this.primitives[3] = this.meshPrimitive;

			this.checkpointVisuals = new GameObject[50];

			this.loadingZoneVisual = null;

			foreach (GameObject theObject in this.primitives)
			{
				Renderer objRenderer = theObject.GetComponent<Renderer>();
				StandardShaderUtils.ChangeRenderMode(objRenderer.material, StandardShaderUtils.BlendMode.Transparent);
				objRenderer.material.SetColor("_Color", new Color(0.0f, 1.0f, 0.0f, 0.33f)); // transparent green by default
				
				objRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // so that it doesn't cast shadows on things
				objRenderer.receiveShadows = false;

				theObject.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
				theObject.GetComponent<Collider>().enabled = false;
				DontDestroyOnLoad(theObject);
				theObject.SetActive(false);
			}

			RenderCheckpoints(false);
			RenderLoadingZone(false);
			ResetAverage();
			RefreshMenuMessage();

			// print mod info to console
			int strLength;
			strLength = System.Math.Max(System.Math.Max(modAuthor.Length, modGame.Length), modName.Length);
			string equalSigns = string.Empty;
			for (int i = 0; i < modName.Length + 4; i++)
				equalSigns += "=";

			Shell.Print("<#FF8B00>" + equalSigns);
			Shell.Print(modName);
			Shell.Print("for " + modGame);
			Shell.Print("Written by: " + modAuthor);
			Shell.Print(equalSigns + "</color>");

			Shell.RegisterCommand("showcheckpoints", new System.Action<string>(this.SetRenderCheckpoints), "showcheckpoints\r\nToggle visual for checkpoints");
			Shell.RegisterCommand("showcps", new System.Action<string>(this.SetRenderCheckpoints), null);
			Shell.RegisterCommand("showcp", new System.Action<string>(this.SetRenderCheckpoints), null);
			Shell.RegisterCommand("showloadingzones", new System.Action<string>(this.SetRenderLoadingZone), "showloadingzones\r\nToggle visual for loading zones");
			Shell.RegisterCommand("showlzs", new System.Action<string>(this.SetRenderLoadingZone), null);
			Shell.RegisterCommand("showlz", new System.Action<string>(this.SetRenderLoadingZone), null);
			Shell.RegisterCommand("speedometer", new System.Action<string>(this.SetDisplaySpeedometer), "speedometer\r\nToggle speedometer");
			Shell.RegisterCommand("checkpointnum", new System.Action<string>(this.SetDisplayCheckpointNum), "checkpointnum\r\nToggle UI display of current & previous checkpoint");

			DebugMessage("Press \"NumPad-Enter\" to bring up the SpeedTools menu.\r\nAlternatively type \"help\" in console to see new console commands.\r\nNOTE: INVALIDATES SPEEDRUN IF USED; RESTART GAME WHEN DONE WITH PRACTICE", 16, false);
		}

		private void OnGUI()
		{
			// debug message
			if (this.renderDebug)
				GUI.Label(new Rect(10, 30, Screen.width - 10, Screen.height - 30), this.debugText);
			if (this.renderSpeed)
				SpeedometerMessages();
			if (this.renderMenu)
				MenuMessages();
			else if (SpeedTools.CheatsActivated)
				DebugMessage("Cheats were activated during this instance of the game; restart to remove this message", 0, false);
			if (this.renderCpNum)
				ShowCheckpointNum();
		}

		private void Update()
		{
			if (this.curCpNum != Game.instance.currentCheckpointNumber)
				this.prevCpNum = this.curCpNum;
			this.curCpNum = Game.instance.currentCheckpointNumber;

			if (Input.GetKeyDown(KeyCode.KeypadEnter)) // toggle menu
			{
				SpeedTools.CheatsActivated = true;
				this.renderMenu = !this.renderMenu;
				if (!this.renderMenu)
					ClearMessage();
			}

			// temporary debug test to see if we can force workshop menu
			/*if (Input.GetKey(KeyCode.L))
			{
				LevelSelectMenu2.mode = LevelSelectMenuMode.Workshop;
				DebugMessage("Set LevelSelectMenu2.mode to Workshop");
			}*/

			if (this.renderMenu)
			{
				if (Input.GetKeyDown(KeyCode.Alpha0)) // exit menu
				{
					this.renderMenu = false;
					ClearMessage();
				}

				if (Input.GetKeyDown(KeyCode.Alpha1)) // toggle speedometer (and reset it)
				{
					this.renderSpeed = !this.renderSpeed;
					RefreshMenuMessage();
					if (!this.renderSpeed)
					{
						ResetAverage();
						this.maxSpeedReached = 0.0f;
						DebugMessage("Reset Speedometer");
					}
				}
				if (Input.GetKeyDown(KeyCode.Alpha2)) // render checkpoint hit-boxes
				{
					this.renderCheckpoints = !this.renderCheckpoints;
					RefreshMenuMessage();
					DebugMessage(string.Format("{0} view of checkpoints' hit-boxes", this.renderCheckpoints ? "Enabled" : "Disabled"));
					this.RenderCheckpoints(this.renderCheckpoints);
				}
				if (Input.GetKeyDown(KeyCode.Alpha3)) // render loading zone hit-boxes
				{
					this.renderLoadingZone = !this.renderLoadingZone;
					RefreshMenuMessage();
					DebugMessage(string.Format("{0} view of loading zone hitbox", this.renderLoadingZone ? "Enabled" : "Disabled"));
					this.RenderLoadingZone(this.renderLoadingZone);
				}
				if (Input.GetKeyDown(KeyCode.Alpha4)) // cycle through color swatch (for speedometer display)
				{
					this.curColor = (this.curColor + 1) % (this.colorSwatch.Length); // cycles from 0-size of colorSwatch and then repeats
					this.theStyle.normal.textColor = this.colorSwatch[this.curColor]; // properly change the color
					DebugMessage("Changed Speedometer text color");
				}
				if (Input.GetKeyDown(KeyCode.Alpha5)) // reset the speedometer
				{
					ResetAverage();
					this.maxSpeedReached = 0.0f;
					DebugMessage("Reset Speedometer");
				}
				if (Input.GetKeyDown(KeyCode.Alpha6)) // render current & previous checkpoint numbers
				{
					//this.renderCpNum = !this.renderCpNum;
					this.SetDisplayCheckpointNum("");
					RefreshMenuMessage();
					DebugMessage(string.Format("{0} display of checkpoint numbers", this.renderCpNum ? "Enabled" : "Disabled"));
				}
				if (Input.GetKeyDown(KeyCode.Backspace)) // cycle through framerate-cap swatch
				{
					QualitySettings.vSyncCount = 0;
					switch(Application.targetFrameRate)
					{
						case -1:
							Application.targetFrameRate = 60;
							break;
						case 60:
							Application.targetFrameRate = 120;
							break;
						case 120:
							Application.targetFrameRate = 144;
							break;
						case 144:
							Application.targetFrameRate = 500;
							break;
						case 500:
							Application.targetFrameRate = 1;
							break;
						case 1:
							Application.targetFrameRate = 5;
							break;
						case 5:
							Application.targetFrameRate = 10;
							break;
						case 10:
							Application.targetFrameRate = 20;
							break;
						case 20:
							Application.targetFrameRate = -1;
							break;

						default:
							Application.targetFrameRate = 60;
							break;
					}
					RefreshMenuMessage();
					DebugMessage("New target framerate = " + Application.targetFrameRate.ToString());
				}
			}

			//bool pauseSpeedTracker = (!this.renderSpeed || !Human.instance || Game.instance.state != GameState.PlayingLevel || !(Human.instance.state == HumanState.Walk || Human.instance.state == HumanState.Jump || Human.instance.state == HumanState.Slide || Human.instance.state == HumanState.Fall || Human.instance.state == HumanState.FreeFall));

			//DebugMessage(string.Format("Speed Tracker is currently {0}.", pauseSpeedTracker ? "paused" : "going"), 0, false);

			//if (!pauseSpeedTracker)
			if (this.renderSpeed)
			{
				// set current speed
				this.curSpeed = Mathf.Sqrt(Mathf.Pow(Human.instance.velocity.x, 2.0f) + Mathf.Pow(Human.instance.velocity.z, 2.0f));
				// set max speed
				this.maxSpeedReached = (this.curSpeed > this.maxSpeedReached) ? this.curSpeed : this.maxSpeedReached;
				// set/shift average speeds down the array by 1 step
				if (this.curSpeedIter == this.speedsOverTime.Length - 1)
				{
					for (int i = 0; i < this.speedsOverTime.Length - 1; i++) // don't want to do the last element in the array, hence the - 1
						this.speedsOverTime[i] = this.speedsOverTime[i + 1]; // effectively pushes out the oldest element of array and moves the placement of everything down by 1
				}
				this.speedsOverTime[this.curSpeedIter] = this.curSpeed; // adds current speed to the array
				this.curSpeedIter = (this.curSpeedIter < this.speedsOverTime.Length - 1) ? this.curSpeedIter + 1 : this.speedsOverTime.Length - 1; // increment until at the end of the array
			}

			//DebugMessage("Current level: " + this.gameLevel.ToString());
			if (this.gameLevel != Game.instance.currentLevelNumber)
			{
				this.RenderLoadingZone(this.renderLoadingZone);
				this.RenderCheckpoints(this.renderCheckpoints);

				this.gameLevel = Game.instance.currentLevelNumber;
			}

			if (SpeedTools.CheatsActivated)
			{
				//CheatCodes.cheatMode = false; // have to do this to make NotifyCheat work more than once
				//CheatCodes.NotifyCheat("SpeedTools"); // enable "cheating" notification in top-right of screen
				SubtitleManager.instance.SetProgress(string.Format(I2.Loc.ScriptLocalization.TUTORIAL.CHEAT, "SpeedTools")); // enable "cheating" notification in top-right of screen
			}
		}
	}
	
	// shoutouts to https://answers.unity.com/users/977869/bellicapax.html for the below code!
	public static class StandardShaderUtils
	{
		public enum BlendMode
		{
			Opaque,
			Cutout,
			Fade,
			Transparent
		}

		public static void ChangeRenderMode(Material standardShaderMaterial, BlendMode blendMode)
		{
			switch (blendMode)
			{
				case BlendMode.Opaque:
					standardShaderMaterial.SetFloat("_Mode", 0.0f);
					standardShaderMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
					standardShaderMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
					standardShaderMaterial.SetInt("_ZWrite", 1);
					standardShaderMaterial.DisableKeyword("_ALPHATEST_ON");
					standardShaderMaterial.DisableKeyword("_ALPHABLEND_ON");
					standardShaderMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
					standardShaderMaterial.renderQueue = -1;
					break;
				case BlendMode.Cutout:
					standardShaderMaterial.SetFloat("_Mode", 1.0f);
					standardShaderMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
					standardShaderMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
					standardShaderMaterial.SetInt("_ZWrite", 1);
					standardShaderMaterial.EnableKeyword("_ALPHATEST_ON");
					standardShaderMaterial.DisableKeyword("_ALPHABLEND_ON");
					standardShaderMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
					standardShaderMaterial.renderQueue = 2450;
					break;
				case BlendMode.Fade:
					standardShaderMaterial.SetFloat("_Mode", 2.0f);
					standardShaderMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
					standardShaderMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					standardShaderMaterial.SetInt("_ZWrite", 0);
					standardShaderMaterial.DisableKeyword("_ALPHATEST_ON");
					standardShaderMaterial.EnableKeyword("_ALPHABLEND_ON");
					standardShaderMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
					standardShaderMaterial.renderQueue = 3000;
					break;
				case BlendMode.Transparent:
					standardShaderMaterial.SetFloat("_Mode", 3.0f);
					standardShaderMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
					standardShaderMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					standardShaderMaterial.SetInt("_ZWrite", 0);
					standardShaderMaterial.DisableKeyword("_ALPHATEST_ON");
					standardShaderMaterial.DisableKeyword("_ALPHABLEND_ON");
					standardShaderMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
					standardShaderMaterial.renderQueue = 3000;
					break;
			}
		}
	}
}
