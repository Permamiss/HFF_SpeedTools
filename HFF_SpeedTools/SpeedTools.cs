/* SpeedTools Mod for Human: Fall Flat
 * Written by Permamiss | http://twitter.com/Permamiss | https://steamcommunity.com/id/Permamiss | https://www.twitch.tv/Permamiss
 * Feel free to contact me if you have any ideas or suggestions. Thanks!
 */

using BepInEx;

namespace HFF_SpeedTools
{
	using HumanAPI;
	using System.Linq;
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
        private bool renderLoadingZone;
		private bool renderAllHitboxes;
        private bool renderDebug;
		private bool renderSpeed;
		private bool renderCpNum;

		private string debugText;
		private string menuMessage;
		private GameObject[] primitives;
		private GameObject cubePrimitive, capsulePrimitive, spherePrimitive, meshPrimitive;
		private GameObject[] checkpointVisuals;
		private System.Collections.Generic.List<GameObject> hitboxVisuals;
		private System.Collections.Generic.List<bool> rendererEnabledForHitboxes;
		private GameObject loadingZoneVisual;
		private LevelPassTrigger curLoadingZone;

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

				renderDebug = true;
				debugText = theText;

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

				renderDebug = true;
				debugText = theText;

				StartCoroutine("ClearMessage", amtSeconds);
			}
		}

		private System.Collections.IEnumerator ClearMessage(float seconds)
		{
			yield return new WaitForSeconds(seconds);
			renderDebug = false;
		}

		private void ClearMessage()
		{
			renderDebug = false;
		}

		private static new void DontDestroyOnLoad(UnityEngine.Object target)
		{
			UnityEngine.Object.DontDestroyOnLoad(target as GameObject);
			UndestroyedObjects.Add(target);
		}

		private void ResetAverage() // set average back to 0 and start over
		{
			curSpeedIter = 0;

			for (int i = 0; i < speedsOverTime.Length; i++)
			{
				speedsOverTime[i] = 0.0f;
			}
		}

		private void RefreshMenuMessage()
		{
			menuMessage = " =" + modName + " Menu= \n";
			//menuMessage += string.Format("  Page {0} of {1}", curPage + 1, (int)System.Math.Ceiling((curPage + 1) / 7.0));
			for (int i = 0; i < (" =" + modName + " Menu= ").Length; i++)
				menuMessage += "-";
			menuMessage += "\n" +
				string.Format("1) Toggle Speedometer [{0}]\n" +
				"2) Toggle hitbox Visual for Checkpoints [{1}]\n" +
				"3) Toggle hitbox Visual for Loading Zones [{2}]\n" +
				"4) Change Speedometer text color\n" +
				"5) Reset Speedometer\n" +
				//"6) Toggle firework launcher [{3}]"
				"6) Toggle Checkpoint number display [{3}]\n" +
                "7) Toggle Toggle hitbox Visual for all objects [{4}]\n" +

                '\u2190' + ") Change framerate cap [{5}]"
                , renderSpeed ? "ENABLED" : "DISABLED", renderCheckpoints ? "ENABLED" : "DISABLED", renderLoadingZone ? "ENABLED" : "DISABLED", renderCpNum ? "ENABLED" : "DISABLED", renderAllHitboxes ? "ENABLED" : "DISABLED", Application.targetFrameRate.ToString()) +
				//(Fireworks.instance && Fireworks.instance.enableWeapons) ? "ENABLED" : "DISABLED") +

				"\n" +
				"0) Exit";
		}

		private void MenuMessages()
		{
			DebugMessage(menuMessage, 0, false);
		}

		private void SpeedometerMessages()
		{
			float averageSpeed = 0.0f;
			foreach (float newSpeed in speedsOverTime)
			{
				averageSpeed += newSpeed;
			}
			averageSpeed /= (curSpeedIter + 1); // divide sum of existing speeds by amount to obtain TRUE average

			GUI.Label(new Rect(Screen.width / 2, Screen.height - 150, 0, 50), string.Format("Current Speed: {0:0.0}", Human.instance ? Mathf.Sqrt(Mathf.Pow(Human.instance.velocity.x, 2.0f) + Mathf.Pow(Human.instance.velocity.z, 2.0f)) : 0.0f), theStyle);
			GUI.Label(new Rect(Screen.width / 2, Screen.height - 100, 0, 50), string.Format("Average Speed: {0:0.0}", averageSpeed), theStyle);
			GUI.Label(new Rect(Screen.width / 2, Screen.height - 50, 0, 50), string.Format("Maximum Speed: {0:0.0}", maxSpeedReached), theStyle);
		}

		private void ShowCheckpointNum()
		{
			GUI.Label(new Rect(Screen.width - 100, 30, 100, 50), "Checkpoint: " + Game.instance.currentCheckpointNumber + " (Previous: " + prevCpNum + ")");
		}

		private GameObject generateHitboxVisual(GameObject sourceObject)
		{
			/*
			* 0 = box/cube
			* 1 = capsule
			* 2 = sphere
			* 3 = mesh
			*/

			Collider collider = sourceObject.gameObject.GetComponent<Collider>();
			GameObject hitboxVisual = null;

			//if (boxCol)
			if (collider)
			{
				if (collider is BoxCollider boxCol)
				{
					hitboxVisual = Instantiate(cubePrimitive);
					hitboxVisual.transform.localScale = Vector3.Scale(boxCol.size, sourceObject.transform.localScale);
					hitboxVisual.transform.SetParent(sourceObject.transform, false);
					hitboxVisual.transform.position = boxCol.bounds.center;
				}
				else if (collider is CapsuleCollider capsuleCol)
				{
					DebugMessage("Collider type is CapsuleCollider");
					hitboxVisual = Instantiate(capsulePrimitive);
					hitboxVisual.transform.localScale = new Vector3(capsuleCol.radius * 2 * capsuleCol.transform.localScale.x, capsuleCol.height * capsuleCol.transform.localScale.y, capsuleCol.radius * 2 * capsuleCol.transform.localScale.z);
					hitboxVisual.transform.SetParent(capsuleCol.transform, false);
				}
				else if (collider is SphereCollider sphereCol)
				{
					DebugMessage("Collider type is SphereCollider");
					hitboxVisual = Instantiate(spherePrimitive);
					hitboxVisual.transform.localScale = Vector3.Scale(new Vector3(sphereCol.radius, sphereCol.radius, sphereCol.radius) * 2, sphereCol.transform.localScale);
					hitboxVisual.transform.SetParent(sphereCol.transform, false);
				}
				else if (collider is MeshCollider meshCol)
				{

					hitboxVisual = Instantiate(cubePrimitive);
					Mesh ogMesh = meshCol.sharedMesh;
					Mesh newMesh = new Mesh();
					newMesh.vertices = ogMesh.vertices;
					newMesh.triangles = ogMesh.triangles;
					newMesh.uv = ogMesh.uv;
					newMesh.normals = ogMesh.normals;
					newMesh.colors = ogMesh.colors;
					newMesh.tangents = ogMesh.tangents;
					hitboxVisual.GetComponent<MeshFilter>().mesh = newMesh;

					//hitboxVisual.transform.localScale = sourceObject.transform.localScale;
					hitboxVisual.transform.SetParent(sourceObject.transform, false);
					//AssetDatabase.CreateAsset(newMesh, AssetDatabase.GetAssetPath(ogMesh) + "_HitboxVisual.asset");

					//hitboxVisual = Instantiate(sourceObject.gameObject);
					//hitboxVisual.transform.localScale *= 1.1f;
				}
				else
				{
					DebugMessage("Collider type is MeshCollider or something else or nonexistant");
					hitboxVisual = Instantiate(cubePrimitive);
					hitboxVisual.transform.localScale = sourceObject.transform.localScale;
					hitboxVisual.transform.SetParent(sourceObject.transform, false);
				}

				hitboxVisual.name = "HitboxVisual";
			}

			return hitboxVisual;
		}

        private void RenderAllHitboxes(bool active)
        {
			//Shell.Print("about to check 'if active' in RenderHitboxVisuals");
            //Shell.Print(string.Format("RenderAllHitboxes called with active={0}", active));
            if (active)
			{
				DebugMessage("RenderAllHitboxVisuals activated");
				System.Collections.Generic.List<GameObject> levelObjects = FindObjectsOfType<GameObject>().ToList();
				DebugMessage(string.Format("amount of GameObjects: {0}", levelObjects.Count));

				for (int i = 0; i < levelObjects.Count; i++)
				{
					//DebugMessage(string.Format("iteration #{0}", i));
					if (levelObjects[i].name == "HitboxVisual")
					{
						DebugMessage(string.Format("removing HitboxVisual {0} from list of hitboxes to make", i));
						levelObjects.RemoveAt(i);
					}
				}
                
				DebugMessage(string.Format("going to render {0} hitboxes", levelObjects.Count));
				for (int i = 0; i < levelObjects.Count; i++)
				{
					DebugMessage("generating hitbox visual");
					hitboxVisuals.Add(generateHitboxVisual(levelObjects[i]));
					GameObject hitboxVisual = hitboxVisuals.Last();
                    DebugMessage("getting renderer of hitboxVisual and setting color");
					if (hitboxVisual)
					{
						float transparency = 1f;
						Collider collider = hitboxVisual.GetComponent<Collider>();
						if (collider != null)
							transparency = collider.isTrigger ? 0.33f : transparency;

                        Renderer objRenderer = hitboxVisual.GetComponent<Renderer>();
                        if (transparency == 1f)
						{
							StandardShaderUtils.ChangeRenderMode(objRenderer.material, StandardShaderUtils.BlendMode.Opaque);

							//objRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; // so that it doesn't cast shadows on things
							//objRenderer.receiveShadows = true;
						}
                        objRenderer.material.SetColor("_Color", new Color(0.3f, 0.3f, 0.8f, transparency)); // dark-ish blue
						hitboxVisual.SetActive(true);

						DebugMessage("getting renderer of levelObject and getting renderer to store renderEnabled state");
						objRenderer = levelObjects[i].GetComponent<Renderer>();
						if (objRenderer != null)
						{
							rendererEnabledForHitboxes.Add(objRenderer.enabled);
							objRenderer.enabled = false;
						}
						else
						{
							Destroy(hitboxVisual);
							hitboxVisuals.RemoveAt(hitboxVisuals.Count - 1);
						}
					}
					else
					{
						DebugMessage(string.Format("hitboxVisual #{0} did not generate properly!", i));
						Destroy(hitboxVisual);
						hitboxVisuals.RemoveAt(hitboxVisuals.Count - 1);
					}
				}
			}
			else
			{
				DebugMessage(string.Format("Size of hitboxVisuals should be same size as rendererEnabledForHitboxes ({0} = {1})", hitboxVisuals.Count, rendererEnabledForHitboxes.Count));
				for (int i = 0; i < hitboxVisuals.Count; i++)
				{
					//if (hitboxVisuals[i])
					//{
					if (hitboxVisuals[i].transform.parent != null)
					{
						GameObject anObject = hitboxVisuals[i].transform.parent.gameObject;
						if (anObject != null)
						{
							Renderer theRenderer = anObject.GetComponent<Renderer>();
							if (theRenderer != null)
								theRenderer.enabled = rendererEnabledForHitboxes[i];
						}
					}
				}

                foreach (GameObject go in hitboxVisuals)
                {
                    Destroy(go);
                }
                Shell.Print("Destroyed all previous hitbox visuals");

                hitboxVisuals.Clear();
				rendererEnabledForHitboxes.Clear();
            }
        }

        private void RenderCheckpoints(bool active)
		{
            CheatsActivated = active || CheatsActivated;
            Checkpoint[] lvlCPs = FindObjectsOfType<Checkpoint>();
			// get rid of previous checkpoint visuals
			foreach (GameObject go in checkpointVisuals)
			{
				Destroy(go);
			}
			checkpointVisuals.Initialize(); // reset array
			//DebugMessage("Reset checkpointVisuals array");
			if (active)
			{
				for (int i = 0; i < lvlCPs.Length; i++)
				{
					if (lvlCPs[i].number != 0) // only generate visual if it's not the "first" checkpoint (where you spawn)
					{
						checkpointVisuals[i] = generateHitboxVisual(lvlCPs[i].gameObject);
						checkpointVisuals[i].GetComponent<Renderer>().material.SetColor("_Color", new Color(1.0f, 0.66f, 0.0f, 0.33f)); // orange for checkpoints :)
						checkpointVisuals[i].SetActive(true);
					}
				}
			}
		}

		private void RenderLoadingZone(bool active)
		{
            CheatsActivated = active || CheatsActivated;
            curLoadingZone = FindObjectOfType<LevelPassTrigger>();
			// get rid of previous loading zone visual
			if (loadingZoneVisual != null)
				Destroy(loadingZoneVisual);
			if (active && curLoadingZone)
			{
				loadingZoneVisual = generateHitboxVisual(curLoadingZone.gameObject);
				loadingZoneVisual.SetActive(true);
			}
		}

        private void SetRenderAllHitboxes(string txt)
        {
            if (string.IsNullOrEmpty(txt))
                renderAllHitboxes = !renderAllHitboxes; // toggles if no words after command
            else
            {
                string[] words = txt.Split(new char[]
                {
                    ' '
                }, System.StringSplitOptions.RemoveEmptyEntries);

                if (words.Length != 1)
                    return;

                if (int.TryParse(words[0], out int num))
                    renderAllHitboxes = num != 0;
                else if (words[0].ToLower() == "true")
                    renderAllHitboxes = true;
                else if (words[0].ToLower() == "false")
                    renderAllHitboxes = false;
            }
            RenderAllHitboxes(renderAllHitboxes);
            Shell.Print(string.Format("hitbox renderer {0}", renderAllHitboxes ? "enabled" : "disabled"));
        }

        private void SetRenderCheckpoints(string txt)
		{
			// NOTE TO SELF:
			//	should make it so that CheatsActivated turns ON only if one of the cheats is actually on
			//	should make it so that CheatsActivated turns OFF if you restart/reach a new level with none of the cheats on
			if (string.IsNullOrEmpty(txt))
				renderCheckpoints = !renderCheckpoints; // toggles if no words after command
			else
			{
				string[] words = txt.Split(new char[]
				{
					' '
				}, System.StringSplitOptions.RemoveEmptyEntries);

				if (words.Length != 1)
					return;

				if (int.TryParse(words[0], out int num))
					renderCheckpoints = num != 0;
				else if (words[0].ToLower() == "true")
					renderCheckpoints = true;
				else if (words[0].ToLower() == "false")
					renderCheckpoints = false;
			}
			RenderCheckpoints(renderCheckpoints);
			Shell.Print(string.Format("checkpoint hitbox renderer {0}", renderCheckpoints ? "enabled" : "disabled"));
		}

		private void SetRenderLoadingZone(string txt)
		{
			if (string.IsNullOrEmpty(txt))
				renderLoadingZone = !renderLoadingZone; // toggles if no words after command
			else
			{
				string[] words = txt.Split(new char[]
				{
					' '
				}, System.StringSplitOptions.RemoveEmptyEntries);

				if (words.Length != 1)
					return;

				if (int.TryParse(words[0], out int num))
					renderLoadingZone = num != 0;
				else if (words[0].ToLower() == "true")
					renderLoadingZone = true;
				else if (words[0].ToLower() == "false")
					renderLoadingZone = false;
			}
			RenderLoadingZone(renderLoadingZone);
			Shell.Print(string.Format("loading zone hitbox renderer {0}", renderLoadingZone ? "enabled" : "disabled"));
		}

        private void SetDisplaySpeedometer(string txt)
		{
			if (string.IsNullOrEmpty(txt))
				renderSpeed = !renderSpeed; // toggles if no words after command
			else
			{
				string[] words = txt.Split(new char[]
				{
					' '
				}, System.StringSplitOptions.RemoveEmptyEntries);

				if (words.Length != 1)
					return;

				if (int.TryParse(words[0], out int num))
					renderSpeed = num != 0;
				else if (words[0].ToLower() == "true")
					renderSpeed = true;
				else if (words[0].ToLower() == "false")
					renderSpeed = false;

				CheatsActivated = renderSpeed || CheatsActivated;
			}
			Shell.Print(string.Format("speedometer {0}", renderSpeed ? "enabled" : "disabled"));
		}

		private void SetDisplayCheckpointNum(string txt)
		{
			if (string.IsNullOrEmpty(txt))
				renderCpNum = !renderCpNum; // toggles if no words after command
			else
			{
				string[] words = txt.Split(new char[]
				{
					' '
				}, System.StringSplitOptions.RemoveEmptyEntries);

				if (words.Length != 1)
					return;

				if (int.TryParse(words[0], out int num))
					renderSpeed = num != 0;
				else if (words[0].ToLower() == "true")
					renderCpNum = true;
				else if (words[0].ToLower() == "false")
					renderCpNum = false;

				CheatsActivated = renderCpNum || CheatsActivated;
			}
			Shell.Print(string.Format("checkpoint number display {0}", renderCpNum ? "enabled" : "disabled"));
		}


		private void Start()
		{
			gameLevel = -1;

			curColor = 0;
			curSpeedIter = 0;
			maxSpeedReached = 0.0f;
			prevCpNum = curCpNum = 0;

			renderCheckpoints = false;
			renderLoadingZone = false;
			renderAllHitboxes = false;
			renderSpeed = false;
			renderMenu = false;
			renderDebug = false;

			debugText = string.Empty;
			curLoadingZone = null;
			colorSwatch = new Color[8] { Color.black, Color.white, Color.red, new Color(1.0f, 0.549f, 0.0f), new Color(1.0f, 1.0f, 0.0f), Color.green, Color.blue, new Color(0.5f, 0.0f, 0.5f) }; // black, white, and colors of rainbow

			theStyle = new GUIStyle()
			{
				wordWrap = false,
				fontSize = 48,
				alignment = TextAnchor.UpperCenter
			};
			theStyle.normal.textColor = colorSwatch[0];

			cubePrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
			capsulePrimitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
			spherePrimitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			meshPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube); // dunno how to implement a mesh at the moment

			primitives = new GameObject[4];
			primitives[0] = cubePrimitive;
			primitives[1] = capsulePrimitive;
			primitives[2] = spherePrimitive;
			primitives[3] = meshPrimitive;

			checkpointVisuals = new GameObject[50];
			hitboxVisuals = new System.Collections.Generic.List<GameObject>();
			rendererEnabledForHitboxes = new System.Collections.Generic.List<bool>();

			loadingZoneVisual = null;

			foreach (GameObject theObject in primitives)
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
			RenderAllHitboxes(false);
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

			Shell.RegisterCommand("showcheckpoints", new System.Action<string>(SetRenderCheckpoints), "showcheckpoints\r\nToggle visual for checkpoints");
			Shell.RegisterCommand("showcps", new System.Action<string>(SetRenderCheckpoints), null);
			Shell.RegisterCommand("showcp", new System.Action<string>(SetRenderCheckpoints), null);
			Shell.RegisterCommand("showloadingzones", new System.Action<string>(SetRenderLoadingZone), "showloadingzones\r\nToggle visual for loading zones");
			Shell.RegisterCommand("showlzs", new System.Action<string>(SetRenderLoadingZone), null);
			Shell.RegisterCommand("showlz", new System.Action<string>(SetRenderLoadingZone), null);
			Shell.RegisterCommand("showhitboxes", new System.Action<string>(SetRenderAllHitboxes), "showhitboxes\r\nToggle hitbox for all objects");
			Shell.RegisterCommand("speedometer", new System.Action<string>(SetDisplaySpeedometer), "speedometer\r\nToggle speedometer");
			Shell.RegisterCommand("checkpointnum", new System.Action<string>(SetDisplayCheckpointNum), "checkpointnum\r\nToggle UI display of current & previous checkpoint");

			//DebugMessage("Press \"NumPad-Enter\" to bring up the SpeedTools menu.\r\nAlternatively type \"help\" in console to see new console commands.\r\nNOTE: INVALIDATES SPEEDRUN IF USED; RESTART GAME WHEN DONE WITH PRACTICE", 16, false);
			//SubtitleManager.instance.SetSubtitle("Press \"NumPad-Enter\" to bring up the SpeedTools menu.\nAlternatively type \"help\" in console to see new console commands.\nNOTE: INVALIDATES SPEEDRUN IF USED; RESTART GAME OR TURN OFF USED TOOLS THEN RESTART LEVEL WHEN DONE WITH PRACTICE", 16);

		}

		private void OnGUI()
		{
			// debug message
			if (renderDebug)
				GUI.Label(new Rect(10, 30, Screen.width - 10, Screen.height - 30), debugText);
			if (renderSpeed)
				SpeedometerMessages();
			if (renderMenu)
				MenuMessages();
			/*
			else if (CheatsActivated)
				DebugMessage("Cheats were activated during this instance of the game; restart to remove this message", 0, false);
			*/
			if (renderCpNum)
				ShowCheckpointNum();
		}

		private void Update()
		{
			if (curCpNum != Game.instance.currentCheckpointNumber)
				prevCpNum = curCpNum;
			curCpNum = Game.instance.currentCheckpointNumber;

			if (Input.GetKeyDown(KeyCode.KeypadEnter)) // toggle menu
			{
				//CheatsActivated = true;
				renderMenu = !renderMenu;
				if (!renderMenu)
					ClearMessage();
			}

			if (renderMenu)
			{
				if (Input.GetKeyDown(KeyCode.Alpha0)) // exit menu
				{
					renderMenu = false;
					ClearMessage();
				}

				if (Input.GetKeyDown(KeyCode.Alpha1)) // toggle speedometer (and reset it)
				{
					renderSpeed = !renderSpeed;
					RefreshMenuMessage();
					if (!renderSpeed)
					{
						ResetAverage();
						maxSpeedReached = 0.0f;
						DebugMessage("Reset Speedometer");
					}
				}
				if (Input.GetKeyDown(KeyCode.Alpha2)) // render checkpoint hitboxes
				{
					renderCheckpoints = !renderCheckpoints;
					RefreshMenuMessage();
					DebugMessage(string.Format("{0} view of checkpoints' hitboxes", renderCheckpoints ? "Enabled" : "Disabled"));
					RenderCheckpoints(renderCheckpoints);
				}
				if (Input.GetKeyDown(KeyCode.Alpha3)) // render loading zone hitboxes
				{
					renderLoadingZone = !renderLoadingZone;
					RefreshMenuMessage();
					DebugMessage(string.Format("{0} view of loading zone hitbox", renderLoadingZone ? "Enabled" : "Disabled"));
					RenderLoadingZone(renderLoadingZone);
				}
				if (Input.GetKeyDown(KeyCode.Alpha4)) // cycle through color swatch (for speedometer display)
				{
					curColor = (curColor + 1) % (colorSwatch.Length); // cycles from 0-size of colorSwatch and then repeats
					theStyle.normal.textColor = colorSwatch[curColor]; // properly change the color
					DebugMessage("Changed Speedometer text color");
				}
				if (Input.GetKeyDown(KeyCode.Alpha5)) // reset the speedometer
				{
					ResetAverage();
					maxSpeedReached = 0.0f;
					DebugMessage("Reset Speedometer");
				}
				if (Input.GetKeyDown(KeyCode.Alpha6)) // render current & previous checkpoint numbers
				{
					SetDisplayCheckpointNum("");
					RefreshMenuMessage();
					DebugMessage(string.Format("{0} display of checkpoint numbers", renderCpNum ? "Enabled" : "Disabled"));
				}
				if (Input.GetKeyDown(KeyCode.Alpha7)) // render all hitboxes
				{
					renderAllHitboxes = !renderAllHitboxes;
					RefreshMenuMessage();
					DebugMessage(string.Format("{0} view of all hitboxes", renderAllHitboxes ? "Enabled" : "Disabled"));
					RenderAllHitboxes(renderAllHitboxes);
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

			//bool pauseSpeedTracker = (!renderSpeed || !Human.instance || Game.instance.state != GameState.PlayingLevel || !(Human.instance.state == HumanState.Walk || Human.instance.state == HumanState.Jump || Human.instance.state == HumanState.Slide || Human.instance.state == HumanState.Fall || Human.instance.state == HumanState.FreeFall));

			//DebugMessage(string.Format("Speed Tracker is currently {0}.", pauseSpeedTracker ? "paused" : "going"), 0, false);

			//if (!pauseSpeedTracker)
			if (renderSpeed)
			{
				// set current speed
				curSpeed = Mathf.Sqrt(Mathf.Pow(Human.instance.velocity.x, 2.0f) + Mathf.Pow(Human.instance.velocity.z, 2.0f));
				// set max speed
				maxSpeedReached = (curSpeed > maxSpeedReached) ? curSpeed : maxSpeedReached;
				// set/shift average speeds down the array by 1 step
				if (curSpeedIter == speedsOverTime.Length - 1)
				{
					for (int i = 0; i < speedsOverTime.Length - 1; i++) // don't want to do the last element in the array, hence the - 1
						speedsOverTime[i] = speedsOverTime[i + 1]; // effectively pushes out the oldest element of array and moves the placement of everything down by 1
				}
				speedsOverTime[curSpeedIter] = curSpeed; // adds current speed to the array
				curSpeedIter = (curSpeedIter < speedsOverTime.Length - 1) ? curSpeedIter + 1 : speedsOverTime.Length - 1; // increment until at the end of the array
			}

			//DebugMessage("Current level: " + gameLevel.ToString());
			if (gameLevel != Game.instance.currentLevelNumber)
			{
				RenderLoadingZone(renderLoadingZone);
				RenderCheckpoints(renderCheckpoints);

				gameLevel = Game.instance.currentLevelNumber;
			}

			if (CheatsActivated)
			{
				// put message on-screen saying that using these tools during a speedrun will disqualify the run, or something similar
				//SubtitleManager.instance.SetProgress(string.Format(I2.Loc.ScriptLocalization.TUTORIAL.CHEAT, "SpeedTools")); // enable "cheating" notification in top-right of screen
				SubtitleManager.instance.SetSubtitle("While this message is on-screen, speedruns will be considered invalid.\nThis message appeared from enabling one of the Speedrun Tools.\nYou can get rid of this message by turning off the tools you enabled,\nthen restarting the level or proceeding to the next level.");
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
