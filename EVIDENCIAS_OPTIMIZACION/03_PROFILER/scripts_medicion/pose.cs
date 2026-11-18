var cam = UnityEngine.Camera.main;
var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>(); if (brain) brain.enabled = false;
var t = UnityEngine.Terrain.activeTerrain;
var p = new UnityEngine.Vector3(100f, 0f, 104f); p.y = t.SampleHeight(p) + t.transform.position.y + 1.7f;
cam.transform.SetPositionAndRotation(p, UnityEngine.Quaternion.Euler(8f, 350f, 0f));
UnityEngine.QualitySettings.vSyncCount = 0; UnityEngine.Application.targetFrameRate = -1;
return "cam at " + p + " brain=" + (brain? "disabled":"none") + " fov=" + cam.fieldOfView;
