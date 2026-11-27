var names = new System.Collections.Generic.Dictionary<string,string>{
 {"Main Thread","Internal"},{"CPU Main Thread Frame Time","Render"},{"CPU Render Thread Frame Time","Render"},{"GPU Frame Time","Render"},{"CPU Total Frame Time","Render"},
 {"SetPass Calls Count","Render"},{"Triangles Count","Render"},{"Instanced Batches Count","Render"},{"Instanced Batched Draw Calls Count","Render"},{"Static Batches Count","Render"},{"Dynamic Batches Count","Render"},
 {"GC Allocated In Frame","Memory"},{"GC Allocation In Frame Count","Memory"},{"System Used Memory","Memory"},{"Total Used Memory","Memory"},{"Gfx Used Memory","Memory"},{"GC Used Memory","Memory"}};
var recs = new System.Collections.Generic.Dictionary<string, Unity.Profiling.ProfilerRecorder>();
foreach (var kv in names) recs[kv.Key] = Unity.Profiling.ProfilerRecorder.StartNew(new Unity.Profiling.ProfilerCategory(kv.Value), kv.Key, 4096);
var stats = new System.Collections.Generic.List<int[]>();
UnityEditor.EditorApplication.CallbackFunction cb = () => { stats.Add(new[]{ UnityEditor.UnityStats.drawCalls, UnityEditor.UnityStats.setPassCalls, UnityEditor.UnityStats.srpBatcherDrawCalls, UnityEditor.UnityStats.standardDrawCalls, UnityEditor.UnityStats.standardInstancedDrawCalls, UnityEditor.UnityStats.standardInstancedInstances, UnityEditor.UnityStats.instancedBatches, UnityEditor.UnityStats.instancedBatchedDrawCalls, UnityEditor.UnityStats.staticBatches, UnityEditor.UnityStats.staticBatchedDrawCalls, UnityEditor.UnityStats.instances, UnityEditor.UnityStats.triangles, UnityEditor.UnityStats.vertices, UnityEditor.UnityStats.shadowCasters }); };
UnityEditor.EditorApplication.update += cb;
System.AppDomain.CurrentDomain.SetData("optm", new object[]{ recs, stats, cb, UnityEngine.Time.frameCount, UnityEngine.Time.realtimeSinceStartupAsDouble });
return "started f=" + UnityEngine.Time.frameCount;
