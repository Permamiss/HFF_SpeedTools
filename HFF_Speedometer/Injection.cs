namespace HFF_SpeedTools
{
    using UnityEngine;

    public class Injection
    {
        public static void Inject()
        {
            if (GameObject.Find("SpeedTools"))
            {
                foreach (Object toDestroy in SpeedTools.UndestroyedObjects)
                    Object.Destroy(toDestroy);
                //Object.Destroy(GameObject.Find("SpeedTools").GetComponent<SpeedTools>());
                Object.Destroy(GameObject.Find("SpeedTools").gameObject);
            }

            GameObject newSpeedometer = new GameObject("SpeedTools");
            Object.DontDestroyOnLoad(newSpeedometer);
            newSpeedometer.AddComponent<SpeedTools>();
        }
    }
}
